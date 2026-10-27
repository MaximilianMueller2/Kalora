namespace Kalora.Domain.Scheduling;

/// <summary>The salon's booking rules that shape which start times are offered.</summary>
/// <param name="TimeZone">The salon's zone; opening hours, working hours and the grid are local times in it.</param>
/// <param name="SlotIntervalMinutes">Start times are multiples of this since local midnight (09:00, 09:15, ...).</param>
/// <param name="MinLeadTimeMinutes">A start must be at least this long after "now".</param>
/// <param name="BookingHorizonDays">The last bookable day is today plus this many days.</param>
public sealed record SchedulingRules(TimeZoneInfo TimeZone, int SlotIntervalMinutes, int MinLeadTimeMinutes, int BookingHorizonDays);

/// <summary>A block of local time on a weekday, e.g. the salon is open Monday 09:00-18:00.</summary>
public sealed record WeekBlock(DayOfWeek Weekday, TimeOnly Start, TimeOnly End);

/// <summary>A working block of a person; it applies on days from <paramref name="ValidFrom"/> up to and including <paramref name="ValidTo"/>.</summary>
public sealed record WorkingBlock(DayOfWeek Weekday, TimeOnly Start, TimeOnly End, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>What the service needs. Buffers occupy the calendar but are not part of the time the customer is there.</summary>
public sealed record ServiceTiming(int DurationMinutes, int BufferBeforeMinutes, int BufferAfterMinutes);

/// <summary>A person who might perform the service, with everything that limits their time.</summary>
/// <param name="SortOrder">Order of the calendar columns; breaks ties when picking a person for "anyone".</param>
/// <param name="DurationOverrideMinutes">This person needs a different time for the service.</param>
/// <param name="Absences">Vacation, sick leave and the like.</param>
/// <param name="Busy">Occupied time including buffers: open and confirmed appointments and blockers.</param>
/// <param name="AppointmentStarts">Start of every open or confirmed appointment, for counting the load per day.</param>
public sealed record Candidate(
    Guid StaffId,
    int SortOrder,
    IReadOnlyList<WorkingBlock> WorkingHours,
    IReadOnlyList<Interval> Absences,
    IReadOnlyList<Interval> Busy,
    IReadOnlyList<DateTimeOffset> AppointmentStarts,
    int? DurationOverrideMinutes = null);

/// <param name="Now">The current instant (from a <see cref="TimeProvider"/>), the basis for lead time and horizon.</param>
/// <param name="FromDate">First local day to calculate, inclusive.</param>
/// <param name="ToDate">Last local day to calculate, inclusive.</param>
/// <param name="ClosedDates">Days on which the salon is closed.</param>
public sealed record AvailabilityRequest(
    SchedulingRules Rules,
    DateTimeOffset Now,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<WeekBlock> OpeningHours,
    IReadOnlySet<DateOnly> ClosedDates,
    ServiceTiming Service,
    IReadOnlyList<Candidate> Candidates);

/// <summary>A person who is free at a start time. Internal: customers never see who it is.</summary>
public sealed record FreeStaff(Guid StaffId, int DurationMinutes, int AppointmentsThatDay, int SortOrder);

/// <param name="StartUtc">When the customer's service begins.</param>
/// <param name="Staff">Everybody who is free to perform it, never empty.</param>
public sealed record Slot(DateTimeOffset StartUtc, IReadOnlyList<FreeStaff> Staff)
{
    /// <summary>The person to take for "anyone": the one with the fewest appointments that day.</summary>
    public FreeStaff BestStaff => StaffPicker.Pick(Staff);
}

/// <summary>The start times of one local day, earliest first. Days without any start time are not listed.</summary>
public sealed record DaySlots(DateOnly Date, IReadOnlyList<Slot> Slots);
