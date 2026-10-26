using Kalora.Application.Common;

namespace Kalora.UnitTests.Staff;

public class SalonTimeTests
{
    private static TimeZoneInfo Zone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    [Theory]
    [InlineData("2026-11-04", "2026-11-03T23:00:00Z")] // winter time, UTC+1
    [InlineData("2026-07-01", "2026-06-30T22:00:00Z")] // summer time, UTC+2
    [InlineData("2026-03-29", "2026-03-28T23:00:00Z")] // day the clocks go forward: midnight is still UTC+1
    [InlineData("2026-10-25", "2026-10-24T22:00:00Z")] // day the clocks go back: midnight is still UTC+2
    public void A_day_starts_at_local_midnight_converted_to_utc(string date, string expectedUtc)
    {
        var start = SalonTime.StartOfDayUtc(DateOnly.Parse(date), Zone("Europe/Berlin"));

        Assert.Equal(DateTimeOffset.Parse(expectedUtc), start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
    }

    [Fact]
    public void A_day_whose_midnight_does_not_exist_starts_one_hour_later()
    {
        // Havana moves the clocks forward at 00:00 on 8 March 2026, so 00:00-01:00 never happens.
        var start = SalonTime.StartOfDayUtc(new DateOnly(2026, 3, 8), Zone("America/Havana"));

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void The_local_date_depends_on_the_zone()
    {
        var instant = new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 4), SalonTime.LocalDate(instant, Zone("Europe/Berlin")));
        Assert.Equal(new DateOnly(2026, 11, 3), SalonTime.LocalDate(instant, Zone("UTC")));
        Assert.Equal(new DateOnly(2026, 11, 3), SalonTime.LocalDate(instant, Zone("America/New_York")));
    }

    [Fact]
    public void A_range_of_whole_days_is_the_start_of_the_first_day_up_to_the_start_of_the_day_after_the_last()
    {
        var zone = Zone("Europe/Berlin");
        var start = SalonTime.StartOfDayUtc(new DateOnly(2026, 10, 24), zone);
        var end = SalonTime.StartOfDayUtc(new DateOnly(2026, 10, 27), zone);

        // 24-26 Oct spans the end of daylight saving time, so it has 73 hours.
        Assert.Equal(TimeSpan.FromHours(73), end - start);
    }
}
