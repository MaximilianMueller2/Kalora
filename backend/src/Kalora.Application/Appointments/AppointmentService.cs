using System.Text.Json;
using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Appointments;

internal sealed class AppointmentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    AppointmentMails mails,
    IValidator<ReasonRequest> reasonValidator,
    TimeProvider timeProvider) : IAppointmentService
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> ListOpenRequestsAsync(CancellationToken cancellationToken = default) =>
        await ListAsync(a => a.Status == AppointmentStatus.Requested, q => q.OrderBy(a => a.ExpiresAtUtc).ThenBy(a => a.StartUtc), cancellationToken);

    public async Task<Result<OpenRequestCount>> CountOpenRequestsAsync(CancellationToken cancellationToken = default) =>
        new OpenRequestCount(await Visible().CountAsync(a => a.Status == AppointmentStatus.Requested, cancellationToken));

    public async Task<Result<IReadOnlyList<AppointmentDto>>> ListAwaitingOutcomeAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await ListAsync(a => a.Status == AppointmentStatus.Confirmed && a.EndUtc <= now, q => q.OrderBy(a => a.EndUtc), cancellationToken);
    }

    public async Task<Result<AppointmentDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await Project(Visible().Where(a => a.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return dto is null ? Error.NotFound("appointment.not_found") : WithNextStates(dto);
    }

    public Task<Result<AppointmentDto>> ConfirmAsync(Guid id, CancellationToken cancellationToken = default) =>
        MoveAsync(id, AppointmentStatus.Confirmed, "appointment.confirmed", EmailTypes.BookingConfirmed, reason: null, cancellationToken);

    public async Task<Result<AppointmentDto>> DeclineAsync(Guid id, ReasonRequest request, CancellationToken cancellationToken = default) =>
        reasonValidator.Validate(request) is { IsValid: false } invalid
            ? invalid.ToError()
            : await MoveAsync(id, AppointmentStatus.Declined, "appointment.declined", EmailTypes.BookingDeclined, request.Reason, cancellationToken);

    public async Task<Result<AppointmentDto>> CancelAsync(Guid id, ReasonRequest request, CancellationToken cancellationToken = default) =>
        reasonValidator.Validate(request) is { IsValid: false } invalid
            ? invalid.ToError()
            : await MoveAsync(id, AppointmentStatus.Cancelled, "appointment.cancelled", EmailTypes.BookingCancelledBySalon, request.Reason, cancellationToken);

    public Task<Result<AppointmentDto>> MarkAttendedAsync(Guid id, CancellationToken cancellationToken = default) =>
        MoveAsync(id, AppointmentStatus.Completed, "appointment.completed", mailType: null, reason: null, cancellationToken);

    public Task<Result<AppointmentDto>> MarkNoShowAsync(Guid id, CancellationToken cancellationToken = default) =>
        MoveAsync(id, AppointmentStatus.NoShow, "appointment.no_show", mailType: null, reason: null, cancellationToken);

    private async Task<Result<AppointmentDto>> MoveAsync(
        Guid id, AppointmentStatus to, string auditAction, string? mailType, string? reason, CancellationToken cancellationToken)
    {
        var appointment = await Visible().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null)
        {
            return Error.NotFound("appointment.not_found");
        }

        var now = timeProvider.GetUtcNow();
        var from = appointment.Status;
        if (!AppointmentStatusRules.CanMove(from, to))
        {
            return Error.Conflict("appointment.invalid_transition");
        }

        if (to == AppointmentStatus.Confirmed && appointment.ExpiresAtUtc <= now)
        {
            return Error.Conflict("appointment.request_expired");
        }

        if (to is AppointmentStatus.Completed or AppointmentStatus.NoShow && appointment.StartUtc > now)
        {
            return Error.Unprocessable("appointment.not_started");
        }

        appointment.TryMoveTo(to, now);
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId,
            Action = auditAction,
            EntityType = nameof(Appointment),
            EntityId = appointment.Id,
            // Ids and states only; the reason is free text and may name people.
            Details = JsonSerializer.Serialize(new { from = from.ToString(), to = to.ToString(), hasReason = !string.IsNullOrWhiteSpace(reason) }),
        });

        if (mailType is not null)
        {
            await QueueCustomerMailAsync(mailType, appointment, reason, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else (or the scheduler) changed it in the same moment; the first answer wins.
            db.ChangeTracker.Clear();
            return Error.Conflict("appointment.invalid_transition");
        }

        return await GetAsync(id, cancellationToken);
    }

    private async Task QueueCustomerMailAsync(string type, Appointment appointment, string? reason, CancellationToken cancellationToken)
    {
        var salon = await db.Salons.FirstAsync(cancellationToken);
        var staffName = await db.StaffMembers.Where(s => s.Id == appointment.StaffId).Select(s => s.DisplayName).FirstAsync(cancellationToken);
        var customer = await db.Customers.FirstAsync(c => c.Id == appointment.CustomerId, cancellationToken);
        mails.QueueForCustomer(type, salon, appointment, staffName, customer, reason);
    }

    private async Task<Result<IReadOnlyList<AppointmentDto>>> ListAsync(
        System.Linq.Expressions.Expression<Func<Appointment, bool>> filter,
        Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>> order,
        CancellationToken cancellationToken) =>
        (await Project(order(Visible().Where(filter))).ToListAsync(cancellationToken)).ConvertAll(WithNextStates);

    private static AppointmentDto WithNextStates(AppointmentDto dto) => dto with { NextStates = AppointmentStatusRules.NextStates(dto.Status) };

    /// <summary>Owners and managers see the salon's appointments, everyone else only those with their own staff profile.</summary>
    private IQueryable<Appointment> Visible()
    {
        if (currentUser.SalonRole is SalonRole.Owner or SalonRole.Manager)
        {
            return db.Appointments;
        }

        var userId = currentUser.UserId;
        return db.Appointments.Where(a => db.StaffMembers.Any(s => s.Id == a.StaffId && s.UserId == userId));
    }

    private IQueryable<AppointmentDto> Project(IQueryable<Appointment> appointments) =>
        from a in appointments
        join s in db.StaffMembers on a.StaffId equals s.Id
        join c in db.Customers on a.CustomerId equals c.Id
        select new AppointmentDto(
            a.Id,
            a.Status,
            a.Source,
            a.StartUtc,
            a.EndUtc,
            a.ServiceName,
            a.PriceCents,
            a.PriceIsFrom,
            a.Currency,
            new AppointmentStaffDto(s.Id, s.DisplayName, s.Color),
            new AppointmentCustomerDto(c.Name, c.Email, c.Phone),
            a.CustomerNote,
            a.InternalNote,
            a.ExpiresAtUtc,
            a.CreatedAtUtc,
            Array.Empty<AppointmentStatus>());
}
