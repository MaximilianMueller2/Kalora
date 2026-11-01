using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Appointments;

internal sealed class ManageBookingService(
    IAppDbContext db,
    ManageTokenLookup lookup,
    AppointmentMails mails,
    SalonMails salonMails,
    TimeProvider timeProvider) : IManageBookingService
{
    public async Task<Result<ManagedAppointmentDto>> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        var found = await lookup.FindAsync(token, cancellationToken);
        return found.IsSuccess ? ToDto(found.Value, timeProvider.GetUtcNow()) : found.Error;
    }

    public async Task<Result<ManagedAppointmentDto>> CancelAsync(string token, CancellationToken cancellationToken = default)
    {
        var found = await lookup.FindAsync(token, track: true, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (appointment, salon) = (found.Value.Appointment, found.Value.Salon);
        var now = timeProvider.GetUtcNow();
        var from = appointment.Status;
        if (!AppointmentStatusRules.CanMove(from, AppointmentStatus.Cancelled))
        {
            return Error.Conflict("appointment.invalid_transition");
        }

        if (now > CancelUntil(appointment, salon.CancellationDeadlineHours))
        {
            return Error.Conflict("appointment.cancellation_deadline_passed");
        }

        appointment.TryMoveTo(AppointmentStatus.Cancelled, now);
        db.AuditLogs.Add(new AuditLog
        {
            SalonId = salon.Id,
            ActorUserId = null,
            Action = "appointment.cancelled_by_customer",
            EntityType = nameof(Appointment),
            EntityId = appointment.Id,
            Details = JsonSerializer.Serialize(new { from = from.ToString(), to = nameof(AppointmentStatus.Cancelled) }),
        });

        var staffName = await db.AllTenants<StaffMember>().Where(s => s.Id == appointment.StaffId).Select(s => s.DisplayName).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var customer = await db.AllTenants<Customer>().FirstAsync(c => c.Id == appointment.CustomerId, cancellationToken);
        mails.QueueForCustomer(EmailTypes.BookingCancelledByCustomer, salon, appointment, staffName, customer);
        await salonMails.QueueAsync(EmailTypes.BookingCancellationForSalon, salon, appointment, staffName, customer, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The salon answered or cancelled in the same moment.
            db.ChangeTracker.Clear();
            return Error.Conflict("appointment.invalid_transition");
        }

        return ToDto(found.Value, now);
    }

    private static DateTimeOffset? CancelUntil(Appointment appointment, int deadlineHours) => appointment.Status switch
    {
        AppointmentStatus.Confirmed => appointment.StartUtc.AddHours(-deadlineHours),
        AppointmentStatus.Requested => appointment.StartUtc,
        _ => null,
    };

    private static ManagedAppointmentDto ToDto(ManagedAppointment found, DateTimeOffset now)
    {
        var (appointment, salon) = (found.Appointment, found.Salon);
        var until = CancelUntil(appointment, salon.CancellationDeadlineHours);
        return new ManagedAppointmentDto(
            appointment.Status,
            appointment.StartUtc,
            appointment.EndUtc,
            appointment.ServiceName,
            salon.Name,
            salon.Slug,
            salon.Phone,
            SalonAddressText.Format(salon),
            salon.TimeZone,
            until,
            until is not null && now <= until,
            appointment.Status == AppointmentStatus.Confirmed,
            appointment.Status is AppointmentStatus.Declined or AppointmentStatus.Expired or AppointmentStatus.Cancelled);
    }
}
