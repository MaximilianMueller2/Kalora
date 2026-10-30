using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Availability;

internal sealed class AvailabilityService(IAppDbContext db, TimeProvider timeProvider) : IAvailabilityService
{
    public const int MaxDays = 62;

    private const int InternalHorizonDays = 3650;

    /// <summary>The salon's own staff may also enter what already began today (a walk-in at 10:00, entered at 10:07).</summary>
    private const int InternalLeadTimeMinutes = -24 * 60;

    private static readonly AppointmentStatus[] BlockingStatuses = [AppointmentStatus.Requested, AppointmentStatus.Confirmed];

    public async Task<Result<IReadOnlyList<DaySlots>>> GetSlotsAsync(AvailabilityQuery query, CancellationToken cancellationToken = default)
    {
        var days = await CalculateAsync(query, cancellationToken);
        return days.IsSuccess ? days.Value.ToList() : days.Error;
    }

    public async Task<Result<FreeStaff>> CheckSlotAsync(SlotCheckQuery query, CancellationToken cancellationToken = default)
    {
        var zoneId = await db.Salons.Select(s => s.TimeZone).FirstOrDefaultAsync(cancellationToken);
        if (zoneId is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var date = SalonTime.LocalDate(query.StartUtc, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        var days = await CalculateAsync(new AvailabilityQuery(query.ServiceId, query.StaffId, date, date, query.Audience, query.IgnoreAppointmentId), cancellationToken);
        if (!days.IsSuccess)
        {
            return days.Error;
        }

        var slot = days.Value.SelectMany(d => d.Slots).FirstOrDefault(s => s.StartUtc == query.StartUtc.ToUniversalTime());
        var chosen = slot is null
            ? null
            : query.StaffId is { } staffId ? slot.Staff.FirstOrDefault(s => s.StaffId == staffId) : slot.BestStaff;
        return chosen is null ? Error.Conflict("booking.slot_unavailable") : chosen;
    }

    private async Task<Result<List<DaySlots>>> CalculateAsync(AvailabilityQuery query, CancellationToken cancellationToken)
    {
        if (query.ToDate < query.FromDate || query.ToDate.DayNumber - query.FromDate.DayNumber >= MaxDays)
        {
            return Error.Validation("availability.range_invalid", "toDate");
        }

        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var forCustomers = query.Audience == AvailabilityAudience.Customer;
        var service = await db.Services
            .Where(s => s.Id == query.ServiceId && s.IsActive && (!forCustomers || s.IsBookableOnline))
            .FirstOrDefaultAsync(cancellationToken);
        if (service is null)
        {
            return Error.NotFound("service.not_found", "serviceId");
        }

        var people = await db.StaffServices
            .Where(link => link.ServiceId == service.Id)
            .Join(db.StaffMembers.Where(s => s.IsActive && (!forCustomers || s.IsBookableOnline)), link => link.StaffId, staff => staff.Id,
                (link, staff) => new { staff.Id, staff.SortOrder, link.DurationOverrideMinutes })
            .Where(p => query.StaffId == null || p.Id == query.StaffId)
            .ToListAsync(cancellationToken);
        if (people.Count == 0)
        {
            return query.StaffId is null ? new List<DaySlots>() : Error.NotFound("staff.not_found", "staffId");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(salon.TimeZone);
        // A little margin on both sides so buffers and appointments that reach across midnight are included.
        var from = SalonTime.StartOfDayUtc(query.FromDate, zone).AddDays(-1);
        var to = SalonTime.StartOfDayUtc(query.ToDate.AddDays(1), zone).AddDays(1);
        var ids = people.Select(p => p.Id).ToList();

        var opening = await db.SalonOpeningHours.ToListAsync(cancellationToken);
        var closed = (await db.SalonClosures.Where(c => c.Date >= query.FromDate && c.Date <= query.ToDate).Select(c => c.Date).ToListAsync(cancellationToken)).ToHashSet();
        var working = await db.StaffWorkingHours
            .Where(h => ids.Contains(h.StaffId) && h.ValidFrom <= query.ToDate && (h.ValidTo == null || h.ValidTo >= query.FromDate))
            .ToListAsync(cancellationToken);
        var absences = await db.StaffAbsences
            .Where(a => ids.Contains(a.StaffId) && a.StartUtc < to && a.EndUtc > from)
            .Select(a => new { a.StaffId, a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);
        var appointments = await db.Appointments
            .Where(a => ids.Contains(a.StaffId) && BlockingStatuses.Contains(a.Status) && a.Id != query.IgnoreAppointmentId && a.BlockedStartUtc < to && a.BlockedEndUtc > from)
            .Select(a => new { a.StaffId, a.StartUtc, a.BlockedStartUtc, a.BlockedEndUtc })
            .ToListAsync(cancellationToken);
        var blockers = await db.TimeBlocks
            .Where(b => ids.Contains(b.StaffId) && b.StartUtc < to && b.EndUtc > from)
            .Select(b => new { b.StaffId, b.StartUtc, b.EndUtc })
            .ToListAsync(cancellationToken);

        var candidates = people.Select(person => new Candidate(
            person.Id,
            person.SortOrder,
            working.Where(h => h.StaffId == person.Id).Select(h => new WorkingBlock(h.Weekday, h.Start, h.End, h.ValidFrom, h.ValidTo)).ToList(),
            absences.Where(a => a.StaffId == person.Id).Select(a => new Interval(a.StartUtc, a.EndUtc)).ToList(),
            appointments.Where(a => a.StaffId == person.Id).Select(a => new Interval(a.BlockedStartUtc, a.BlockedEndUtc))
                .Concat(blockers.Where(b => b.StaffId == person.Id).Select(b => new Interval(b.StartUtc, b.EndUtc))).ToList(),
            appointments.Where(a => a.StaffId == person.Id).Select(a => a.StartUtc).ToList(),
            person.DurationOverrideMinutes)).ToList();

        var rules = forCustomers
            ? new SchedulingRules(zone, salon.SlotIntervalMinutes, salon.MinLeadTimeMinutes, salon.BookingHorizonDays)
            : new SchedulingRules(zone, salon.SlotIntervalMinutes, InternalLeadTimeMinutes, InternalHorizonDays);
        var request = new AvailabilityRequest(
            rules,
            timeProvider.GetUtcNow(),
            query.FromDate,
            query.ToDate,
            opening.Select(h => new WeekBlock(h.Weekday, h.Start, h.End)).ToList(),
            closed,
            new ServiceTiming(service.DurationMinutes, service.BufferBeforeMinutes, service.BufferAfterMinutes),
            candidates);

        return AvailabilityCalculator.Calculate(request).ToList();
    }
}
