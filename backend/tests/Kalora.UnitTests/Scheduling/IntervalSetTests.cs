using Kalora.Domain.Scheduling;

namespace Kalora.UnitTests.Scheduling;

public class IntervalSetTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    private static Interval I(int fromHour, int toHour) => new(Day.AddHours(fromHour), Day.AddHours(toHour));

    private static string Show(IEnumerable<Interval> set) =>
        string.Join(",", set.Select(i => $"{(i.Start - Day).TotalHours}-{(i.End - Day).TotalHours}"));

    [Fact]
    public void Union_merges_overlapping_and_touching_intervals_and_drops_empty_ones()
    {
        var result = IntervalSet.Union([I(14, 16), I(9, 13), I(13, 15), I(20, 20), I(18, 19)]);

        Assert.Equal("9-16,18-19", Show(result));
    }

    [Fact]
    public void Union_of_nothing_is_nothing()
    {
        Assert.Empty(IntervalSet.Union([]));
    }

    [Fact]
    public void Intersect_keeps_only_the_shared_time()
    {
        var result = IntervalSet.Intersect([I(8, 12), I(14, 18)], [I(10, 15), I(17, 20)]);

        Assert.Equal("10-12,14-15,17-18", Show(result));
    }

    [Fact]
    public void Intersect_of_intervals_that_only_touch_is_empty()
    {
        Assert.Empty(IntervalSet.Intersect([I(8, 12)], [I(12, 14)]));
    }

    [Fact]
    public void Subtract_cuts_holes_trims_edges_and_can_remove_everything()
    {
        Assert.Equal("9-10,11-12", Show(IntervalSet.Subtract([I(9, 12)], [I(10, 11)])));
        Assert.Equal("10-12", Show(IntervalSet.Subtract([I(9, 12)], [I(8, 10)])));
        Assert.Equal("9-10", Show(IntervalSet.Subtract([I(9, 12)], [I(10, 13)])));
        Assert.Empty(IntervalSet.Subtract([I(9, 12)], [I(8, 13)]));
        Assert.Equal("9-12", Show(IntervalSet.Subtract([I(9, 12)], [I(12, 13), I(7, 9)])));
    }

    [Fact]
    public void Subtract_handles_several_intervals_on_both_sides()
    {
        var result = IntervalSet.Subtract([I(8, 12), I(13, 18)], [I(9, 10), I(11, 14), I(17, 19)]);

        Assert.Equal("8-9,10-11,14-17", Show(result));
    }

    [Fact]
    public void Covers_needs_one_interval_that_contains_the_whole_span()
    {
        var set = IntervalSet.Union([I(9, 12), I(13, 18)]);

        Assert.True(IntervalSet.Covers(set, I(9, 12)));
        Assert.True(IntervalSet.Covers(set, I(14, 15)));
        Assert.False(IntervalSet.Covers(set, I(11, 14)));
        Assert.False(IntervalSet.Covers(set, I(8, 10)));
        Assert.False(IntervalSet.Covers(set, I(17, 19)));
        Assert.False(IntervalSet.Covers([], I(9, 10)));
    }

    [Fact]
    public void An_interval_cannot_end_before_it_starts()
    {
        Assert.Throws<ArgumentException>(() => new Interval(Day.AddHours(2), Day.AddHours(1)));
    }

    [Fact]
    public void Intervals_overlap_only_when_they_share_time()
    {
        Assert.True(I(9, 12).Overlaps(I(11, 13)));
        Assert.False(I(9, 12).Overlaps(I(12, 13)));
        Assert.True(I(9, 12).Covers(I(9, 12)));
    }

    [Fact]
    public void Offsets_are_normalized_to_UTC()
    {
        var local = new Interval(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(1)), new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal(TimeSpan.Zero, local.Start.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), local.Start);
    }
}
