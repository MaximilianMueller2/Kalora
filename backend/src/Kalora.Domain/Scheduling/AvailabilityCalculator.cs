namespace Kalora.Domain.Scheduling;

/// <summary>
/// Works out which start times can be offered for a service. Pure logic: no database, no clock (the caller passes
/// "now"). Local salon time is used to read opening hours, working hours and the grid; everything is compared in UTC.
/// </summary>
public static class AvailabilityCalculator
{
    public static IReadOnlyList<DaySlots> Calculate(AvailabilityRequest request)
    {
        var rules = request.Rules;
        if (rules.SlotIntervalMinutes <= 0 || request.Service.DurationMinutes <= 0)
        {
            throw new ArgumentException("The slot interval and the duration must be positive.", nameof(request));
        }

        var zone = rules.TimeZone;
        var today = ZoneClock.LocalDate(request.Now, zone);
        var first = request.FromDate > today ? request.FromDate : today;
        var lastBookable = today.AddDays(rules.BookingHorizonDays);
        var last = request.ToDate < lastBookable ? request.ToDate : lastBookable;
        var earliestStart = request.Now.AddMinutes(rules.MinLeadTimeMinutes);

        var people = request.Candidates.Select(c => new PreparedCandidate(c, zone)).ToList();
        var days = new List<DaySlots>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var slots = SlotsOfDay(request, people, date, earliestStart);
            if (slots.Count > 0)
            {
                days.Add(new DaySlots(date, slots));
            }
        }

        return days;
    }

    private static List<Slot> SlotsOfDay(AvailabilityRequest request, List<PreparedCandidate> people, DateOnly date, DateTimeOffset earliestStart)
    {
        var zone = request.Rules.TimeZone;
        var opening = WorkingTime.OpeningOfDay(request.OpeningHours, request.ClosedDates, date, zone);
        if (opening.Count == 0)
        {
            return [];
        }

        var free = people.Select(person => person.FreeTime(date, opening)).ToList();
        var slots = new List<Slot>();
        foreach (var start in GridOfDay(date, request.Rules).Where(start => start >= earliestStart))
        {
            var staff = new List<FreeStaff>();
            for (var index = 0; index < people.Count; index++)
            {
                var person = people[index];
                var duration = person.Source.DurationOverrideMinutes ?? request.Service.DurationMinutes;
                var blocked = new Interval(
                    start.AddMinutes(-request.Service.BufferBeforeMinutes),
                    start.AddMinutes(duration + request.Service.BufferAfterMinutes));
                if (IntervalSet.Covers(free[index], blocked))
                {
                    staff.Add(new FreeStaff(person.Source.StaffId, duration, person.AppointmentsOn(date), person.Source.SortOrder));
                }
            }

            if (staff.Count > 0)
            {
                slots.Add(new Slot(start, staff));
            }
        }

        return slots;
    }

    /// <summary>The start times of a local day: multiples of the interval since local midnight, as instants, earliest first.</summary>
    private static IEnumerable<DateTimeOffset> GridOfDay(DateOnly date, SchedulingRules rules)
    {
        var instants = new List<DateTimeOffset>();
        for (var minutes = 0; minutes < 24 * 60; minutes += rules.SlotIntervalMinutes)
        {
            instants.AddRange(ZoneClock.Instants(date, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)), rules.TimeZone));
        }

        return instants.Distinct().Order();
    }

    /// <summary>A candidate with their absences and busy time normalized once, not once per day.</summary>
    private sealed class PreparedCandidate
    {
        private readonly TimeZoneInfo _zone;
        private readonly IReadOnlyList<Interval> _absences;
        private readonly IReadOnlyList<Interval> _busy;
        private readonly Dictionary<DateOnly, int> _appointmentsPerDay;

        public PreparedCandidate(Candidate source, TimeZoneInfo zone)
        {
            Source = source;
            _zone = zone;
            _absences = IntervalSet.Union(source.Absences);
            _busy = IntervalSet.Union(source.Busy);
            _appointmentsPerDay = source.AppointmentStarts
                .GroupBy(start => ZoneClock.LocalDate(start, zone))
                .ToDictionary(group => group.Key, group => group.Count());
        }

        public Candidate Source { get; }

        public int AppointmentsOn(DateOnly date) => _appointmentsPerDay.GetValueOrDefault(date);

        /// <summary>Working time inside the opening hours, minus absences, minus what is already booked.</summary>
        public IReadOnlyList<Interval> FreeTime(DateOnly date, IReadOnlyList<Interval> opening)
        {
            var working = WorkingTime.OfDay(Source.WorkingHours, date, _zone);

            var window = IntervalSet.Subtract(IntervalSet.Intersect(working, opening), _absences);
            return IntervalSet.Subtract(window, _busy);
        }
    }
}
