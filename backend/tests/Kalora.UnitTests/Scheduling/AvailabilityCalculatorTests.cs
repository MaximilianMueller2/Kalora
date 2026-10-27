using Kalora.Domain.Scheduling;

namespace Kalora.UnitTests.Scheduling;

public class AvailabilityCalculatorTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Monday 2 November 2026, 06:00 UTC = 07:00 in Berlin (winter time).
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static TimeOnly T(string time) => TimeOnly.Parse(time);

    /// <summary>A local time in Berlin on a date, as a UTC instant.</summary>
    private static DateTimeOffset Utc(DateOnly date, string time) =>
        new DateTimeOffset(date.ToDateTime(T(time)), Berlin.GetUtcOffset(date.ToDateTime(T(time)))).ToUniversalTime();

    private static Interval Span(DateOnly date, string from, string to) => new(Utc(date, from), Utc(date, to));

    private static WeekBlock Open(DayOfWeek day, string from, string to) => new(day, T(from), T(to));

    private static WorkingBlock Works(DayOfWeek day, string from, string to, DateOnly? validFrom = null, DateOnly? validTo = null) =>
        new(day, T(from), T(to), validFrom ?? new DateOnly(2026, 1, 1), validTo);

    private static Candidate Person(Guid id, IEnumerable<WorkingBlock> hours, int sortOrder = 0, IEnumerable<Interval>? absences = null,
        IEnumerable<Interval>? busy = null, IEnumerable<DateTimeOffset>? appointmentStarts = null, int? duration = null) =>
        new(id, sortOrder, hours.ToList(), (absences ?? []).ToList(), (busy ?? []).ToList(), (appointmentStarts ?? []).ToList(), duration);

    private static AvailabilityRequest Request(
        IEnumerable<WeekBlock> opening,
        IEnumerable<Candidate> people,
        int duration = 60,
        int before = 0,
        int after = 0,
        int grid = 15,
        int lead = 0,
        int horizon = 60,
        DateOnly? from = null,
        DateOnly? to = null,
        DateTimeOffset? now = null,
        IEnumerable<DateOnly>? closed = null) =>
        new(new SchedulingRules(Berlin, grid, lead, horizon), now ?? Now, from ?? Monday, to ?? from ?? Monday,
            opening.ToList(), (closed ?? []).ToHashSet(), new ServiceTiming(duration, before, after), people.ToList());

    private static List<string> LocalStarts(IReadOnlyList<DaySlots> days, DateOnly date) =>
        (days.FirstOrDefault(d => d.Date == date)?.Slots ?? [])
        .Select(s => TimeZoneInfo.ConvertTime(s.StartUtc, Berlin).ToString("HH:mm"))
        .ToList();

    private static List<string> MondayStarts(AvailabilityRequest request) => LocalStarts(AvailabilityCalculator.Calculate(request), Monday);

    private static readonly WeekBlock[] MondayNineToTwelve = [new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))];
    private static readonly WorkingBlock[] WorksNineToTwelve = [new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), new DateOnly(2026, 1, 1), null)];

    [Fact]
    public void Offers_every_grid_time_in_which_the_service_fits()
    {
        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], duration: 60, grid: 30));

        Assert.Equal(["09:00", "09:30", "10:00", "10:30", "11:00"], starts);
    }

    [Fact]
    public void A_break_in_the_working_hours_cannot_be_overlapped()
    {
        var people = Person(A, [Works(DayOfWeek.Monday, "09:00", "12:00"), Works(DayOfWeek.Monday, "13:00", "17:00")]);

        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "08:00", "18:00")], [people], duration: 60, grid: 30));

        Assert.Equal(["09:00", "09:30", "10:00", "10:30", "11:00", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00"], starts);
    }

    [Fact]
    public void Working_blocks_that_touch_count_as_one_stretch()
    {
        var people = Person(A, [Works(DayOfWeek.Monday, "09:00", "13:00"), Works(DayOfWeek.Monday, "13:00", "17:00")]);

        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "08:00", "18:00")], [people], duration: 60, grid: 30));

        Assert.Contains("12:30", starts);
        Assert.Contains("12:00", starts);
    }

    [Fact]
    public void Buffers_before_and_after_must_fit_too()
    {
        // 15 minutes before and after: the first start is 09:15 and the last must end (plus buffer) by 12:00.
        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], duration: 60, before: 15, after: 15, grid: 15));

        Assert.Equal(["09:15", "09:30", "09:45", "10:00", "10:15", "10:30", "10:45"], starts);
    }

    [Fact]
    public void A_service_that_ends_exactly_at_closing_time_is_offered()
    {
        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], duration: 60, grid: 60));

        Assert.Equal(["09:00", "10:00", "11:00"], starts);
    }

    [Fact]
    public void A_service_that_would_end_one_grid_step_after_closing_is_not_offered()
    {
        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], duration: 75, grid: 15));

        Assert.Equal("10:45", starts[^1]);
    }

    [Fact]
    public void A_slot_exactly_between_two_appointments_is_offered_and_nothing_else()
    {
        var busy = new[] { Span(Monday, "09:00", "10:00"), Span(Monday, "11:00", "12:00") };

        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, busy: busy)], duration: 60, grid: 15));

        Assert.Equal(["10:00"], starts);
    }

    [Fact]
    public void A_gap_one_minute_too_short_offers_nothing()
    {
        var busy = new[] { Span(Monday, "09:00", "10:00"), Span(Monday, "10:59", "12:00") };

        Assert.Empty(MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, busy: busy)], duration: 60, grid: 1)));
    }

    [Fact]
    public void The_buffer_of_an_existing_appointment_blocks_the_following_start()
    {
        // The appointment itself ends at 11:00 but its buffer occupies the calendar until 11:15.
        var busy = new[] { Span(Monday, "10:00", "11:15") };

        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "09:00", "13:00")], [Person(A, [Works(DayOfWeek.Monday, "09:00", "13:00")], busy: busy)], duration: 30, grid: 15));

        Assert.DoesNotContain("10:45", starts);
        Assert.DoesNotContain("11:00", starts);
        Assert.Contains("11:15", starts);
        Assert.Contains("09:30", starts);
        Assert.DoesNotContain("09:45", starts);
    }

    [Fact]
    public void Absences_remove_the_time_they_cover()
    {
        var absences = new[] { Span(Monday, "10:00", "11:30") };

        var starts = MondayStarts(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, absences: absences)], duration: 30, grid: 30));

        Assert.Equal(["09:00", "09:30", "11:30"], starts);
    }

    [Fact]
    public void A_day_long_absence_removes_the_person_for_that_day_only()
    {
        var absences = new[] { new Interval(Utc(Monday, "00:00"), Utc(Tuesday, "00:00")) };
        var work = new[] { Works(DayOfWeek.Monday, "09:00", "12:00"), Works(DayOfWeek.Tuesday, "09:00", "12:00") };
        var opening = new[] { Open(DayOfWeek.Monday, "09:00", "12:00"), Open(DayOfWeek.Tuesday, "09:00", "12:00") };

        var days = AvailabilityCalculator.Calculate(Request(opening, [Person(A, work, absences: absences)], from: Monday, to: Tuesday));

        Assert.Empty(LocalStarts(days, Monday));
        Assert.Equal(["09:00", "09:15", "09:30", "09:45", "10:00", "10:15", "10:30", "10:45", "11:00"], LocalStarts(days, Tuesday));
    }

    [Fact]
    public void A_closure_removes_the_whole_day_for_everybody()
    {
        var days = AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve), Person(B, WorksNineToTwelve)], closed: [Monday]));

        Assert.Empty(days);
    }

    [Fact]
    public void Opening_hours_limit_the_working_hours()
    {
        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "10:00", "12:00")], [Person(A, [Works(DayOfWeek.Monday, "08:00", "18:00")])], duration: 60, grid: 60));

        Assert.Equal(["10:00", "11:00"], starts);
    }

    [Fact]
    public void Working_hours_limit_the_opening_hours()
    {
        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "08:00", "18:00")], [Person(A, [Works(DayOfWeek.Monday, "14:00", "16:00")])], duration: 60, grid: 60));

        Assert.Equal(["14:00", "15:00"], starts);
    }

    [Fact]
    public void A_salon_break_in_the_opening_hours_is_respected()
    {
        var opening = new[] { Open(DayOfWeek.Monday, "09:00", "12:00"), Open(DayOfWeek.Monday, "14:00", "16:00") };

        var starts = MondayStarts(Request(opening, [Person(A, [Works(DayOfWeek.Monday, "08:00", "18:00")])], duration: 60, grid: 60));

        Assert.Equal(["09:00", "10:00", "11:00", "14:00", "15:00"], starts);
    }

    [Fact]
    public void A_weekday_without_opening_hours_has_no_slots()
    {
        var days = AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], from: Tuesday));

        Assert.Empty(days);
    }

    [Fact]
    public void The_lead_time_removes_start_times_that_are_too_soon_and_keeps_the_boundary()
    {
        // 09:00 local now, two hours of lead time: 11:00 is the first start that is allowed.
        var now = Utc(Monday, "09:00");

        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "09:00", "13:00")], [Person(A, [Works(DayOfWeek.Monday, "09:00", "13:00")])], duration: 30, grid: 30, lead: 120, now: now));

        Assert.Equal(["11:00", "11:30", "12:00", "12:30"], starts);
    }

    [Fact]
    public void A_start_one_minute_before_the_lead_time_ends_is_not_offered()
    {
        var now = Utc(Monday, "09:01");

        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "09:00", "13:00")], [Person(A, [Works(DayOfWeek.Monday, "09:00", "13:00")])], duration: 30, grid: 30, lead: 120, now: now));

        Assert.Equal("11:30", starts[0]);
    }

    [Fact]
    public void The_booking_horizon_ends_after_the_last_day()
    {
        var opening = Enumerable.Range(0, 7).Select(d => Open((DayOfWeek)d, "09:00", "10:00")).ToList();
        var work = Enumerable.Range(0, 7).Select(d => Works((DayOfWeek)d, "09:00", "10:00")).ToList();

        var days = AvailabilityCalculator.Calculate(Request(opening, [Person(A, work)], duration: 60, horizon: 2, from: Monday, to: Monday.AddDays(10)));

        Assert.Equal([Monday, Monday.AddDays(1), Monday.AddDays(2)], days.Select(d => d.Date));
    }

    [Fact]
    public void Days_in_the_past_are_never_offered()
    {
        var opening = Enumerable.Range(0, 7).Select(d => Open((DayOfWeek)d, "09:00", "10:00")).ToList();
        var work = Enumerable.Range(0, 7).Select(d => Works((DayOfWeek)d, "09:00", "10:00")).ToList();

        var days = AvailabilityCalculator.Calculate(Request(opening, [Person(A, work)], duration: 60, from: Monday.AddDays(-5), to: Monday.AddDays(1)));

        Assert.Equal([Monday, Tuesday], days.Select(d => d.Date));
    }

    [Fact]
    public void Several_people_are_free_in_parallel_and_busy_ones_are_left_out()
    {
        var busy = new[] { Span(Monday, "09:00", "10:00") };
        var request = Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, busy: busy), Person(B, WorksNineToTwelve, sortOrder: 1)], duration: 60, grid: 60);

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal([B], slots[0].Staff.Select(s => s.StaffId));
        Assert.Equal([A, B], slots[1].Staff.Select(s => s.StaffId).Order());
        Assert.Equal(3, slots.Count);
    }

    [Fact]
    public void A_start_is_offered_as_long_as_one_person_is_free()
    {
        var absences = new[] { Span(Monday, "09:00", "12:00") };
        var request = Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, absences: absences), Person(B, WorksNineToTwelve)], duration: 60, grid: 60);

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.All(slots, slot => Assert.Equal([B], slot.Staff.Select(s => s.StaffId)));
    }

    [Fact]
    public void A_person_with_an_individual_duration_needs_that_much_time()
    {
        // Window 09:00-10:30. A needs the default 60 minutes, B 90.
        var window = new[] { Open(DayOfWeek.Monday, "09:00", "10:30") };
        var work = new[] { Works(DayOfWeek.Monday, "09:00", "10:30") };
        var request = Request(window, [Person(A, work), Person(B, work, sortOrder: 1, duration: 90)], duration: 60, grid: 30);

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal(2, slots.Count);
        Assert.Equal([(A, 60), (B, 90)], slots[0].Staff.OrderBy(s => s.SortOrder).Select(s => (s.StaffId, s.DurationMinutes)));
        Assert.Equal([(A, 60)], slots[1].Staff.Select(s => (s.StaffId, s.DurationMinutes)));
    }

    [Fact]
    public void An_individual_duration_can_also_be_shorter()
    {
        var work = new[] { Works(DayOfWeek.Monday, "09:00", "10:00") };
        var request = Request([Open(DayOfWeek.Monday, "09:00", "10:00")], [Person(A, work, duration: 30)], duration: 60, grid: 30);

        Assert.Equal(["09:00", "09:30"], MondayStarts(request));
    }

    [Fact]
    public void A_working_plan_applies_only_from_its_start_date_and_until_its_end_date()
    {
        var work = new[]
        {
            Works(DayOfWeek.Monday, "09:00", "10:00", validTo: new DateOnly(2026, 11, 8)),
            Works(DayOfWeek.Monday, "14:00", "15:00", validFrom: new DateOnly(2026, 11, 9)),
        };
        var opening = new[] { Open(DayOfWeek.Monday, "08:00", "18:00") };
        var nextMonday = Monday.AddDays(7);

        var days = AvailabilityCalculator.Calculate(Request(opening, [Person(A, work)], duration: 60, grid: 60, from: Monday, to: nextMonday));

        Assert.Equal(["09:00"], LocalStarts(days, Monday));
        Assert.Equal(["14:00"], LocalStarts(days, nextMonday));
    }

    [Fact]
    public void The_grid_follows_the_local_clock_not_the_opening_time()
    {
        var starts = MondayStarts(Request([Open(DayOfWeek.Monday, "09:10", "10:00")], [Person(A, [Works(DayOfWeek.Monday, "09:00", "10:00")])], duration: 15, grid: 15));

        Assert.Equal(["09:15", "09:30", "09:45"], starts);
    }

    [Fact]
    public void A_service_longer_than_any_window_is_never_offered()
    {
        Assert.Empty(AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve)], duration: 240)));
    }

    [Fact]
    public void Without_candidates_or_working_hours_nothing_is_offered()
    {
        Assert.Empty(AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [])));
        Assert.Empty(AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [Person(A, [])])));
    }

    [Fact]
    public void Slots_are_ordered_by_day_and_time_and_empty_days_are_left_out()
    {
        var opening = new[] { Open(DayOfWeek.Monday, "09:00", "10:00"), Open(DayOfWeek.Wednesday, "09:00", "10:00") };
        var work = new[] { Works(DayOfWeek.Monday, "09:00", "10:00"), Works(DayOfWeek.Wednesday, "09:00", "10:00") };

        var days = AvailabilityCalculator.Calculate(Request(opening, [Person(A, work)], duration: 30, grid: 30, from: Monday, to: Monday.AddDays(4)));

        Assert.Equal([Monday, Monday.AddDays(2)], days.Select(d => d.Date));
        Assert.All(days, day => Assert.Equal(day.Slots.OrderBy(s => s.StartUtc).ToList(), day.Slots));
    }

    [Fact]
    public void An_invalid_grid_or_duration_is_a_programming_error()
    {
        Assert.Throws<ArgumentException>(() => AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [], grid: 0)));
        Assert.Throws<ArgumentException>(() => AvailabilityCalculator.Calculate(Request(MondayNineToTwelve, [], duration: 0)));
    }

    // ------------------------------------------------------------------ daylight saving time

    [Theory]
    [InlineData("2026-11-04", "09:00", "2026-11-04T08:00:00Z")] // winter time, UTC+1
    [InlineData("2026-07-01", "09:00", "2026-07-01T07:00:00Z")] // summer time, UTC+2
    [InlineData("2026-03-30", "09:00", "2026-03-30T07:00:00Z")] // the day after the clocks went forward
    [InlineData("2026-10-26", "09:00", "2026-10-26T08:00:00Z")] // the day after the clocks went back
    public void Start_times_are_the_correct_UTC_instant_in_winter_and_summer_time(string date, string localTime, string expectedUtc)
    {
        var day = DateOnly.Parse(date);
        var request = Request(
            [Open(day.DayOfWeek, "09:00", "10:00")],
            [Person(A, [Works(day.DayOfWeek, "09:00", "10:00")])],
            duration: 60,
            grid: 60,
            from: day,
            now: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            horizon: 365);

        var slot = Assert.Single(AvailabilityCalculator.Calculate(request).Single().Slots);

        Assert.Equal(DateTimeOffset.Parse(expectedUtc), slot.StartUtc);
        Assert.Equal(localTime, TimeZoneInfo.ConvertTime(slot.StartUtc, Berlin).ToString("HH:mm"));
    }

    [Fact]
    public void On_the_day_the_clocks_go_forward_the_skipped_hour_has_no_start_times()
    {
        // 29 March 2026: 02:00 becomes 03:00. Open 01:00-04:00 local is only two real hours (00:00Z-02:00Z).
        var sunday = new DateOnly(2026, 3, 29);
        var request = Request(
            [Open(DayOfWeek.Sunday, "01:00", "04:00")],
            [Person(A, [Works(DayOfWeek.Sunday, "01:00", "04:00")])],
            duration: 60,
            grid: 30,
            from: sunday,
            now: new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal(["01:00", "01:30", "03:00"], slots.Select(s => TimeZoneInfo.ConvertTime(s.StartUtc, Berlin).ToString("HH:mm")));
        Assert.Equal(
            [new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero)],
            slots.Select(s => s.StartUtc));
    }

    [Fact]
    public void On_the_day_the_clocks_go_back_the_repeated_hour_offers_both_instants()
    {
        // 25 October 2026: 03:00 becomes 02:00. Open 01:00-04:00 local is four real hours (23:00Z-03:00Z).
        var sunday = new DateOnly(2026, 10, 25);
        var request = Request(
            [Open(DayOfWeek.Sunday, "01:00", "04:00")],
            [Person(A, [Works(DayOfWeek.Sunday, "01:00", "04:00")])],
            duration: 60,
            grid: 60,
            from: sunday,
            now: new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal(
            [
                new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero),
            ],
            slots.Select(s => s.StartUtc));
    }

    [Fact]
    public void An_opening_block_that_ends_in_the_repeated_hour_covers_the_whole_wall_clock_span()
    {
        // 01:00-02:30 on 25 October: 02:30 happens twice, the block ends at the later one (01:30Z), so it is 2.5 real hours long.
        var sunday = new DateOnly(2026, 10, 25);
        var request = Request(
            [Open(DayOfWeek.Sunday, "01:00", "02:30")],
            [Person(A, [Works(DayOfWeek.Sunday, "01:00", "02:30")])],
            duration: 30,
            grid: 30,
            from: sunday,
            now: new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal(5, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero), slots[^1].StartUtc);
    }

    [Fact]
    public void An_opening_block_that_starts_in_the_skipped_hour_starts_right_after_it()
    {
        // 02:30 does not exist on 29 March; the block 02:30-04:00 is read as 03:30-04:00.
        var sunday = new DateOnly(2026, 3, 29);
        var request = Request(
            [Open(DayOfWeek.Sunday, "02:30", "04:00")],
            [Person(A, [Works(DayOfWeek.Sunday, "02:00", "05:00")])],
            duration: 30,
            grid: 30,
            from: sunday,
            now: new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal(["03:30"], slots.Select(s => TimeZoneInfo.ConvertTime(s.StartUtc, Berlin).ToString("HH:mm")));
    }

    [Fact]
    public void A_service_across_the_changeover_takes_its_real_duration()
    {
        // 90 minutes starting 01:30 local on 25 October lasts until 02:00 local the second time, i.e. real 90 minutes.
        var sunday = new DateOnly(2026, 10, 25);
        var request = Request(
            [Open(DayOfWeek.Sunday, "01:00", "03:00")],
            [Person(A, [Works(DayOfWeek.Sunday, "01:00", "03:00")])],
            duration: 90,
            grid: 30,
            from: sunday,
            now: new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        // The window 01:00-03:00 local is 23:00Z-02:00Z (three real hours): 90-minute starts until 00:30Z.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), slots[^1].StartUtc);
    }

    [Fact]
    public void An_appointment_before_the_changeover_blocks_by_real_time()
    {
        var sunday = new DateOnly(2026, 10, 25);
        var busy = new[] { new Interval(new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero)) };
        var request = Request(
            [Open(DayOfWeek.Sunday, "01:00", "04:00")],
            [Person(A, [Works(DayOfWeek.Sunday, "01:00", "04:00")], busy: busy)],
            duration: 60,
            grid: 60,
            from: sunday,
            now: new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero));

        var slots = AvailabilityCalculator.Calculate(request).Single().Slots;

        Assert.Equal([new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero)], slots.Select(s => s.StartUtc));
    }

    // ------------------------------------------------------------------ best person for "anyone"

    [Fact]
    public void The_best_person_is_the_one_with_the_fewest_appointments_that_day()
    {
        var busyDay = new[] { Utc(Monday, "13:00"), Utc(Monday, "14:00") };
        var request = Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, sortOrder: 0, appointmentStarts: busyDay), Person(B, WorksNineToTwelve, sortOrder: 1)], duration: 60, grid: 60);

        var slot = AvailabilityCalculator.Calculate(request).Single().Slots[0];

        Assert.Equal(B, slot.BestStaff.StaffId);
        Assert.Equal(2, slot.Staff.Single(s => s.StaffId == A).AppointmentsThatDay);
    }

    [Fact]
    public void On_a_tie_the_earlier_calendar_column_wins()
    {
        var request = Request(MondayNineToTwelve, [Person(B, WorksNineToTwelve, sortOrder: 0), Person(A, WorksNineToTwelve, sortOrder: 1)], duration: 60, grid: 60);

        Assert.Equal(B, AvailabilityCalculator.Calculate(request).Single().Slots[0].BestStaff.StaffId);
    }

    [Fact]
    public void Only_appointments_of_the_same_local_day_count()
    {
        // 23:30 UTC on Sunday is already Monday 00:30 in Berlin and counts for Monday; 22:30 UTC does not.
        var starts = new[] { new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 22, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 2, 23, 30, 0, TimeSpan.Zero) };
        var request = Request(MondayNineToTwelve, [Person(A, WorksNineToTwelve, appointmentStarts: starts)], duration: 60, grid: 60);

        var staff = AvailabilityCalculator.Calculate(request).Single().Slots[0].Staff.Single();

        Assert.Equal(1, staff.AppointmentsThatDay);
    }

    [Fact]
    public void Picking_does_not_depend_on_the_order_of_the_list()
    {
        FreeStaff[] list = [new(B, 60, 1, 0), new(A, 60, 1, 0)];

        Assert.Equal(A, StaffPicker.Pick(list).StaffId);
        Assert.Equal(A, StaffPicker.Pick(list.Reverse()).StaffId);
    }
}
