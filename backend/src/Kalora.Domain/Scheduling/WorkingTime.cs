namespace Kalora.Domain.Scheduling;

/// <summary>When a person works, as instants: their working blocks inside the salon's opening hours, on days the salon is open.</summary>
public static class WorkingTime
{
    /// <summary>The working blocks that apply on <paramref name="date"/> (weekday and validity range), as UTC instants.</summary>
    public static IReadOnlyList<Interval> OfDay(IEnumerable<WorkingBlock> blocks, DateOnly date, TimeZoneInfo zone) =>
        IntervalSet.Union(blocks
            .Where(block => block.Weekday == date.DayOfWeek
                && block.End > block.Start
                && block.ValidFrom <= date
                && (block.ValidTo is null || block.ValidTo >= date))
            .Select(block => new Interval(ZoneClock.BlockStart(date, block.Start, zone), ZoneClock.BlockEnd(date, block.End, zone))));

    /// <summary>Opening hours of a day as instants; empty on days the salon is closed.</summary>
    public static IReadOnlyList<Interval> OpeningOfDay(IEnumerable<WeekBlock> opening, IReadOnlySet<DateOnly> closedDates, DateOnly date, TimeZoneInfo zone) =>
        closedDates.Contains(date)
            ? []
            : IntervalSet.Union(opening
                .Where(block => block.Weekday == date.DayOfWeek && block.End > block.Start)
                .Select(block => new Interval(ZoneClock.BlockStart(date, block.Start, zone), ZoneClock.BlockEnd(date, block.End, zone))));

    /// <summary>Working time of a person over several days, inside the opening hours. Absences are not subtracted: they are shown on their own.</summary>
    public static IReadOnlyList<Interval> Spans(
        IReadOnlyList<WorkingBlock> blocks,
        IReadOnlyList<WeekBlock> opening,
        IReadOnlySet<DateOnly> closedDates,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo zone)
    {
        var spans = new List<Interval>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            spans.AddRange(IntervalSet.Intersect(OfDay(blocks, date, zone), OpeningOfDay(opening, closedDates, date, zone)));
        }

        return IntervalSet.Union(spans);
    }
}
