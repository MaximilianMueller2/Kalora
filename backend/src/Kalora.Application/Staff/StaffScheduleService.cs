using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Application.Salons;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Staff;

internal sealed class StaffScheduleService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<OpeningHoursPlan> planValidator,
    IValidator<CreateAbsenceRequest> absenceValidator) : IStaffScheduleService
{
    private static readonly DayOfWeek[] WeekFromMonday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static readonly AppointmentStatus[] OpenStatuses = [AppointmentStatus.Requested, AppointmentStatus.Confirmed];

    public async Task<Result<StaffSchedule>> GetScheduleAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var access = await CheckAccessAsync(staffId, managerOnly: false, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        return await LoadScheduleAsync(staffId, await TodayAsync(cancellationToken), cancellationToken);
    }

    public async Task<Result<StaffSchedule>> ReplaceScheduleAsync(Guid staffId, SaveStaffScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var access = await CheckAccessAsync(staffId, managerOnly: true, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var days = (request.Days ?? []).Select(day => day with { Blocks = day.Blocks ?? [] }).ToList();
        var validation = planValidator.Validate(new OpeningHoursPlan(days));
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        // A plan without any working time would silently turn the person unbookable; deactivate them instead.
        if (days.All(day => day.Blocks.Count == 0))
        {
            return Error.Validation("staff.schedule_empty", "days");
        }

        var today = await TodayAsync(cancellationToken);
        if (request.ValidFrom < today)
        {
            return Error.Validation("staff.schedule_date_in_past", "validFrom");
        }

        var rows = await db.StaffWorkingHours.Where(h => h.StaffId == staffId).ToListAsync(cancellationToken);
        // Everything from the cut-over date on is replaced; plans that started earlier end the day before.
        db.StaffWorkingHours.RemoveRange(rows.Where(h => h.ValidFrom >= request.ValidFrom));
        foreach (var row in rows.Where(h => h.ValidFrom < request.ValidFrom && (h.ValidTo is null || h.ValidTo >= request.ValidFrom)))
        {
            row.ValidTo = request.ValidFrom.AddDays(-1);
        }

        db.StaffWorkingHours.AddRange(days
            .SelectMany(day => day.Blocks.Select(block => new StaffWorkingHours
            {
                StaffId = staffId,
                Weekday = day.Weekday,
                Start = block.Start,
                End = block.End,
                ValidFrom = request.ValidFrom,
            })));
        await db.SaveChangesAsync(cancellationToken);
        return await LoadScheduleAsync(staffId, today, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<AbsenceDto>>> ListAbsencesAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var access = await CheckAccessAsync(staffId, managerOnly: false, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var zone = await ZoneAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var absences = await db.StaffAbsences.Where(a => a.StaffId == staffId && a.EndUtc > now).OrderBy(a => a.StartUtc).ToListAsync(cancellationToken);
        if (absences.Count == 0)
        {
            return new List<AbsenceDto>();
        }

        var first = absences.Min(a => a.StartUtc);
        var last = absences.Max(a => a.EndUtc);
        var appointments = await db.Appointments
            .Where(a => a.StaffId == staffId && OpenStatuses.Contains(a.Status) && a.StartUtc < last && a.EndUtc > first)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);

        return absences
            .Select(a => ToDto(a, zone, appointments.Count(p => p.StartUtc < a.EndUtc && p.EndUtc > a.StartUtc)))
            .ToList();
    }

    public async Task<Result<AbsenceCreated>> CreateAbsenceAsync(Guid staffId, CreateAbsenceRequest request, CancellationToken cancellationToken = default)
    {
        var access = await CheckAccessAsync(staffId, managerOnly: false, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var validation = absenceValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var zone = await ZoneAsync(cancellationToken);
        if (request.EndDate < SalonTime.LocalDate(timeProvider.GetUtcNow(), zone))
        {
            return Error.Validation("absence.date_in_past", "endDate");
        }

        var absence = new StaffAbsence
        {
            StaffId = staffId,
            StartUtc = SalonTime.StartOfDayUtc(request.StartDate, zone),
            EndUtc = SalonTime.StartOfDayUtc(request.EndDate.AddDays(1), zone),
            Type = request.Type,
        };
        db.StaffAbsences.Add(absence);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await (
            from a in db.Appointments
            join c in db.Customers on a.CustomerId equals c.Id
            where a.StaffId == staffId && OpenStatuses.Contains(a.Status) && a.StartUtc < absence.EndUtc && a.EndUtc > absence.StartUtc
            orderby a.StartUtc
            select new AffectedAppointment(a.Id, a.StartUtc, a.EndUtc, a.ServiceName, c.Name, a.Status)).ToListAsync(cancellationToken);
        return new AbsenceCreated(ToDto(absence, zone, affected.Count), affected);
    }

    public async Task<Result> DeleteAbsenceAsync(Guid staffId, Guid absenceId, CancellationToken cancellationToken = default)
    {
        var access = await CheckAccessAsync(staffId, managerOnly: false, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var absence = await db.StaffAbsences.FirstOrDefaultAsync(a => a.Id == absenceId && a.StaffId == staffId, cancellationToken);
        if (absence is null)
        {
            return Error.NotFound("absence.not_found");
        }

        db.StaffAbsences.Remove(absence);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private bool IsManager => currentUser.SalonRole is SalonRole.Owner or SalonRole.Manager;

    /// <summary>Managers may handle every staff member; everybody else only their own calendar column.</summary>
    private async Task<Result> CheckAccessAsync(Guid staffId, bool managerOnly, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.Where(s => s.Id == staffId).Select(s => new { s.UserId }).FirstOrDefaultAsync(cancellationToken);
        if (staff is null)
        {
            return Error.NotFound("staff.not_found");
        }

        var isSelf = staff.UserId is not null && staff.UserId == currentUser.UserId;
        return IsManager || (!managerOnly && isSelf) ? Result.Success() : Error.Forbidden();
    }

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken cancellationToken) =>
        TimeZoneInfo.FindSystemTimeZoneById(await db.Salons.Select(s => s.TimeZone).FirstAsync(cancellationToken));

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        SalonTime.LocalDate(timeProvider.GetUtcNow(), await ZoneAsync(cancellationToken));

    private async Task<StaffSchedule> LoadScheduleAsync(Guid staffId, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await db.StaffWorkingHours
            .Where(h => h.StaffId == staffId && (h.ValidTo == null || h.ValidTo >= today))
            .ToListAsync(cancellationToken);
        var versions = rows
            .GroupBy(h => h.ValidFrom)
            .OrderBy(g => g.Key)
            .Select(g => new StaffScheduleVersion(g.Key, g.Max(h => h.ValidTo), ToDays(g)))
            .ToList();
        return new StaffSchedule(versions);
    }

    private static IReadOnlyList<OpeningDay> ToDays(IEnumerable<StaffWorkingHours> rows)
    {
        var byDay = rows.ToLookup(h => h.Weekday);
        return WeekFromMonday
            .Select(day => new OpeningDay(day, byDay[day].OrderBy(h => h.Start).Select(h => new OpeningBlock(h.Start, h.End)).ToList()))
            .ToList();
    }

    private static AbsenceDto ToDto(StaffAbsence absence, TimeZoneInfo zone, int conflictCount) => new(
        absence.Id,
        SalonTime.LocalDate(absence.StartUtc, zone),
        SalonTime.LocalDate(absence.EndUtc.AddSeconds(-1), zone),
        absence.Type,
        conflictCount);
}
