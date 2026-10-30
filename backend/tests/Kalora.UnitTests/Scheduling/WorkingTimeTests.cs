using Kalora.Domain.Scheduling;

namespace Kalora.UnitTests.Scheduling;

public class WorkingTimeTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateOnly Wednesday = new(2026, 11, 4);

    private static WorkingBlock Work(DayOfWeek day, int from, int to, DateOnly? validFrom = null, DateOnly? validTo = null) =>
        new(day, new TimeOnly(from, 0), new TimeOnly(to, 0), validFrom ?? new DateOnly(2026, 1, 1), validTo);

    private static WeekBlock Open(DayOfWeek day, int from, int to) => new(day, new TimeOnly(from, 0), new TimeOnly(to, 0));

    private static DateTimeOffset Utc(int day, int hour, int month = 11) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Working_time_is_read_in_the_salons_zone_and_returned_as_instants()
    {
        var spans = WorkingTime.Spans([Work(DayOfWeek.Wednesday, 9, 17)], [Open(DayOfWeek.Wednesday, 8, 18)], new HashSet<DateOnly>(), Wednesday, Wednesday, Berlin);

        var span = Assert.Single(spans);
        Assert.Equal((Utc(4, 8), Utc(4, 16)), (span.Start, span.End));
    }

    [Fact]
    public void Summer_time_moves_the_instants()
    {
        var july = new DateOnly(2026, 7, 1);

        var span = Assert.Single(WorkingTime.Spans([Work(DayOfWeek.Wednesday, 9, 17)], [Open(DayOfWeek.Wednesday, 8, 18)], new HashSet<DateOnly>(), july, july, Berlin));

        Assert.Equal((Utc(1, 7, 7), Utc(1, 15, 7)), (span.Start, span.End));
    }

    [Fact]
    public void Only_the_part_inside_the_opening_hours_counts()
    {
        var span = Assert.Single(WorkingTime.Spans([Work(DayOfWeek.Wednesday, 7, 20)], [Open(DayOfWeek.Wednesday, 9, 13)], new HashSet<DateOnly>(), Wednesday, Wednesday, Berlin));

        Assert.Equal((Utc(4, 8), Utc(4, 12)), (span.Start, span.End));
    }

    [Fact]
    public void A_closed_day_or_a_day_without_opening_hours_has_none()
    {
        Assert.Empty(WorkingTime.Spans([Work(DayOfWeek.Wednesday, 9, 17)], [Open(DayOfWeek.Wednesday, 8, 18)], new HashSet<DateOnly> { Wednesday }, Wednesday, Wednesday, Berlin));
        Assert.Empty(WorkingTime.Spans([Work(DayOfWeek.Wednesday, 9, 17)], [Open(DayOfWeek.Thursday, 8, 18)], new HashSet<DateOnly>(), Wednesday, Wednesday, Berlin));
    }

    [Fact]
    public void A_plan_applies_only_between_its_first_and_last_day()
    {
        var plan = Work(DayOfWeek.Wednesday, 9, 17, validFrom: new DateOnly(2026, 11, 5), validTo: new DateOnly(2026, 11, 18));
        var opening = new[] { Open(DayOfWeek.Wednesday, 8, 18) };

        var spans = WorkingTime.Spans([plan], opening, new HashSet<DateOnly>(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Berlin);

        Assert.Equal([Utc(11, 8), Utc(18, 8)], spans.Select(s => s.Start));
    }

    [Fact]
    public void Several_blocks_in_a_day_stay_separate_and_a_week_lists_each_day()
    {
        var blocks = new[] { Work(DayOfWeek.Wednesday, 9, 12), Work(DayOfWeek.Wednesday, 14, 17), Work(DayOfWeek.Thursday, 9, 12) };
        var opening = new[] { Open(DayOfWeek.Wednesday, 8, 18), Open(DayOfWeek.Thursday, 8, 18) };

        var spans = WorkingTime.Spans(blocks, opening, new HashSet<DateOnly>(), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), Berlin);

        Assert.Equal([(Utc(4, 8), Utc(4, 11)), (Utc(4, 13), Utc(4, 16)), (Utc(5, 8), Utc(5, 11))], spans.Select(s => (s.Start, s.End)));
    }
}
