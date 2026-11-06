using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Admin;

/// <summary>
/// The salons of all tenants are read and changed here on purpose (listed in the architecture test). Only counts of staff and
/// appointments are looked at, never customers. Every change goes into the salon's audit log with the admin as actor and
/// without personal data.
/// </summary>
internal sealed class AdminSalonService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOptions<PlansOptions> plans,
    TimeProvider timeProvider) : IAdminSalonService
{
    private const int PageSize = 100;
    private const int MaxSearchLength = 100;
    private const int MaxTrialExtensionDays = 90;
    private const int MaxNoteLength = 2000;

    /// <summary>Appointments that hold their time: everything except declined, expired and cancelled.</summary>
    private static readonly AppointmentStatus[] CountedStatuses =
        [AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    public async Task<Result<AdminSalonList>> ListAsync(string? search, SalonStatus? status, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsPlatformAdmin)
        {
            return Error.Forbidden();
        }

        if (search is { Length: > MaxSearchLength })
        {
            return Error.Validation("validation.too_long", "q");
        }

        var query = db.AllSalons().AsNoTracking();
        if (status is { } wanted)
        {
            query = query.Where(s => s.Status == wanted);
        }

        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            var ownerSalonIds = db.AllTenants<SalonMember>()
                .Where(m => m.Role == SalonRole.Owner)
                .Join(db.Users.Where(u => u.Email != null && u.Email.ToLower().Contains(term)), m => m.UserId, u => u.Id, (m, _) => m.SalonId);
            query = query.Where(s => s.Name.ToLower().Contains(term) || s.Slug.Contains(term) || ownerSalonIds.Contains(s.Id));
        }

        var salons = await query.OrderByDescending(s => s.CreatedAtUtc).Take(PageSize + 1).ToListAsync(cancellationToken);
        var hasMore = salons.Count > PageSize;
        salons = salons.Take(PageSize).ToList();

        var figures = await FiguresAsync(salons.Select(s => s.Id).ToList(), cancellationToken);
        var now = timeProvider.GetUtcNow();
        var items = salons.Select(s =>
        {
            var figure = figures.GetValueOrDefault(s.Id);
            return new AdminSalonListItem(
                s.Id, s.Name, s.Slug, s.Status, s.PlanCode, s.AccessAt(now), figure.Staff, plans.Value.MaxStaff(s), figure.Appointments,
                s.TrialEndsAtUtc, s.CreatedAtUtc, figure.OwnerEmail, !string.IsNullOrWhiteSpace(s.AdminNote));
        }).ToList();
        return new AdminSalonList(items, hasMore);
    }

    public async Task<Result<AdminSalonDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsPlatformAdmin)
        {
            return Error.Forbidden();
        }

        var salon = await db.AllSalons().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return salon is null ? Error.NotFound("salon.not_found") : await DetailAsync(salon, cancellationToken);
    }

    public Task<Result<AdminSalonDetail>> SetPlanAsync(Guid id, SetPlanRequest request, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.plan_changed", salon =>
        {
            if (!Enum.IsDefined(request.PlanCode))
            {
                return Error.Validation("admin.plan_invalid", "planCode");
            }

            var from = salon.PlanCode;
            salon.PlanCode = request.PlanCode;
            return new { from = from.ToString(), to = request.PlanCode.ToString() };
        }, cancellationToken);

    public Task<Result<AdminSalonDetail>> ExtendTrialAsync(Guid id, ExtendTrialRequest request, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.trial_extended", salon =>
        {
            if (request.Days is < 1 or > MaxTrialExtensionDays)
            {
                return Error.Validation("admin.trial_days_invalid", "days");
            }

            if (salon.Status != SalonStatus.Trial)
            {
                return Error.Conflict("admin.not_in_trial");
            }

            var now = timeProvider.GetUtcNow();
            var from = salon.TrialEndsAtUtc;
            var start = from is { } end && end > now ? end : now;
            salon.TrialEndsAtUtc = start.AddDays(request.Days);
            salon.TrialWeekReminderSentAtUtc = null;
            salon.TrialDayReminderSentAtUtc = null;
            return new { days = request.Days, to = salon.TrialEndsAtUtc };
        }, cancellationToken);

    public Task<Result<AdminSalonDetail>> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.status_changed", salon => ChangeStatus(salon, request.Status), cancellationToken);

    public Task<Result<AdminSalonDetail>> SuspendAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.status_changed", salon => ChangeStatus(salon, SalonStatus.Suspended), cancellationToken);

    public Task<Result<AdminSalonDetail>> UnsuspendAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.status_changed", salon =>
        {
            if (salon.Status != SalonStatus.Suspended)
            {
                return Error.Conflict("admin.not_suspended");
            }

            var trialRuns = salon.TrialEndsAtUtc is { } end && end > timeProvider.GetUtcNow();
            return ChangeStatus(salon, trialRuns ? SalonStatus.Trial : SalonStatus.Active);
        }, cancellationToken);

    public Task<Result<AdminSalonDetail>> SetNoteAsync(Guid id, SetNoteRequest request, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, "admin.note_changed", salon =>
        {
            var note = request.Note?.Trim();
            if (note is { Length: > MaxNoteLength })
            {
                return Error.Validation("admin.note_too_long", "note");
            }

            salon.AdminNote = string.IsNullOrEmpty(note) ? null : note;
            // The note itself may name people, so the log only says that it changed.
            return new { changed = true };
        }, cancellationToken);

    private static Result<object> ChangeStatus(Salon salon, SalonStatus to)
    {
        var from = salon.Status;
        salon.Status = to;
        return new { from = from.ToString(), to = to.ToString() };
    }

    /// <summary>Loads the salon, applies <paramref name="change"/> (which returns what to log, or an <see cref="Error"/>), saves and logs it.</summary>
    private async Task<Result<AdminSalonDetail>> ChangeAsync(Guid id, string action, Func<Salon, Result<object>> change, CancellationToken cancellationToken)
    {
        if (!currentUser.IsPlatformAdmin)
        {
            return Error.Forbidden();
        }

        var salon = await db.AllSalons().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var outcome = change(salon);
        if (!outcome.IsSuccess)
        {
            return outcome.Error;
        }

        db.AuditLogs.Add(new AuditLog
        {
            SalonId = salon.Id,
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = nameof(Salon),
            EntityId = salon.Id,
            Details = JsonSerializer.Serialize(outcome.Value),
        });
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(salon, cancellationToken);
    }

    private async Task<AdminSalonDetail> DetailAsync(Salon salon, CancellationToken cancellationToken)
    {
        var figure = (await FiguresAsync([salon.Id], cancellationToken)).GetValueOrDefault(salon.Id);
        var ownerName = await (
            from member in db.AllTenants<SalonMember>()
            join user in db.Users on member.UserId equals user.Id
            where member.SalonId == salon.Id && member.Role == SalonRole.Owner
            select user.DisplayName).FirstOrDefaultAsync(cancellationToken);
        return new AdminSalonDetail(
            salon.Id, salon.Name, salon.Slug, salon.City, salon.Phone, salon.TimeZone, salon.Status, salon.PlanCode, salon.AccessAt(timeProvider.GetUtcNow()),
            figure.Staff, plans.Value.MaxStaff(salon), figure.Appointments, salon.TrialEndsAtUtc, salon.CreatedAtUtc, ownerName, figure.OwnerEmail, salon.AdminNote);
    }

    private readonly record struct Figures(int Staff, int Appointments, string? OwnerEmail);

    /// <summary>Active staff, appointments of the current month (UTC) and the owner's address, per salon, in three queries.</summary>
    private async Task<Dictionary<Guid, Figures>> FiguresAsync(IReadOnlyList<Guid> salonIds, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var staff = await db.AllTenants<StaffMember>().Where(s => s.IsActive && salonIds.Contains(s.SalonId))
            .GroupBy(s => s.SalonId).Select(g => new { SalonId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.SalonId, g => g.Count, cancellationToken);
        var appointments = await db.AllTenants<Appointment>()
            .Where(a => salonIds.Contains(a.SalonId) && a.StartUtc >= monthStart && a.StartUtc < monthEnd && CountedStatuses.Contains(a.Status))
            .GroupBy(a => a.SalonId).Select(g => new { SalonId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.SalonId, g => g.Count, cancellationToken);
        var owners = await (
            from member in db.AllTenants<SalonMember>()
            join user in db.Users on member.UserId equals user.Id
            where member.Role == SalonRole.Owner && salonIds.Contains(member.SalonId)
            select new { member.SalonId, user.Email }).ToListAsync(cancellationToken);
        var ownerBySalon = owners.GroupBy(o => o.SalonId).ToDictionary(g => g.Key, g => g.First().Email);

        return salonIds.ToDictionary(id => id, id => new Figures(staff.GetValueOrDefault(id), appointments.GetValueOrDefault(id), ownerBySalon.GetValueOrDefault(id)));
    }
}
