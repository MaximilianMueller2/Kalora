using FluentValidation;
using Kalora.Application.Common;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Salons;

internal sealed class OpeningHoursService(
    IAppDbContext db,
    TimeProvider timeProvider,
    IValidator<OpeningHoursPlan> planValidator,
    IValidator<CreateClosureRequest> closureValidator) : IOpeningHoursService
{
    private static readonly DayOfWeek[] WeekFromMonday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public async Task<Result<OpeningHoursPlan>> GetPlanAsync(CancellationToken cancellationToken = default) =>
        ToPlan(await db.SalonOpeningHours.OrderBy(h => h.Start).ToListAsync(cancellationToken));

    public async Task<Result<OpeningHoursPlan>> ReplacePlanAsync(OpeningHoursPlan plan, CancellationToken cancellationToken = default)
    {
        var validation = planValidator.Validate(plan);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var existing = await db.SalonOpeningHours.ToListAsync(cancellationToken);
        db.SalonOpeningHours.RemoveRange(existing);
        var added = plan.Days
            .SelectMany(day => day.Blocks.Select(block => new SalonOpeningHours { Weekday = day.Weekday, Start = block.Start, End = block.End }))
            .ToList();
        db.SalonOpeningHours.AddRange(added);
        await db.SaveChangesAsync(cancellationToken);
        return ToPlan(added);
    }

    public async Task<Result<IReadOnlyList<Closure>>> ListClosuresAsync(CancellationToken cancellationToken = default)
    {
        var today = await TodayAsync(cancellationToken);
        if (today is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var closures = await db.SalonClosures.Where(c => c.Date >= today).OrderBy(c => c.Date).ToListAsync(cancellationToken);
        return closures.Select(ToClosure).ToList();
    }

    public async Task<Result<IReadOnlyList<Closure>>> CreateClosuresAsync(CreateClosureRequest request, CancellationToken cancellationToken = default)
    {
        var validation = closureValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var today = await TodayAsync(cancellationToken);
        if (today is null)
        {
            return Error.NotFound("salon.not_found");
        }

        if (request.Date < today)
        {
            return Error.Validation("closure.date_in_past", "date");
        }

        var last = request.EndDate ?? request.Date;
        var taken = (await db.SalonClosures.Where(c => c.Date >= request.Date && c.Date <= last).Select(c => c.Date).ToListAsync(cancellationToken)).ToHashSet();
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        var created = Enumerable.Range(0, last.DayNumber - request.Date.DayNumber + 1)
            .Select(offset => request.Date.AddDays(offset))
            .Where(date => !taken.Contains(date))
            .Select(date => new SalonClosure { Date = date, Reason = reason })
            .ToList();
        if (created.Count == 0)
        {
            return Error.Conflict("closure.already_exists", "date");
        }

        db.SalonClosures.AddRange(created);
        await db.SaveChangesAsync(cancellationToken);
        return created.Select(ToClosure).ToList();
    }

    public async Task<Result> DeleteClosureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var closure = await db.SalonClosures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (closure is null)
        {
            return Error.NotFound("closure.not_found");
        }

        db.SalonClosures.Remove(closure);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>"Today" is the calendar day in the salon's own time zone, not in UTC.</summary>
    private async Task<DateOnly?> TodayAsync(CancellationToken cancellationToken)
    {
        var timeZoneId = await db.Salons.Select(s => s.TimeZone).FirstOrDefaultAsync(cancellationToken);
        if (timeZoneId is null)
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }

    private static OpeningHoursPlan ToPlan(IEnumerable<SalonOpeningHours> hours)
    {
        var byDay = hours.ToLookup(h => h.Weekday);
        var days = WeekFromMonday
            .Select(day => new OpeningDay(day, byDay[day].OrderBy(h => h.Start).Select(h => new OpeningBlock(h.Start, h.End)).ToList()))
            .ToList();
        return new OpeningHoursPlan(days);
    }

    private static Closure ToClosure(SalonClosure closure) => new(closure.Id, closure.Date, closure.Reason);
}
