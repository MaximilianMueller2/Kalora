using Kalora.Application.Appointments;
using Kalora.Domain.Staff;

namespace Kalora.Application.Calendar;

/// <param name="From">First local day (salon time zone), inclusive.</param>
/// <param name="To">Last local day, inclusive; at most <see cref="CalendarService.MaxDays"/> days.</param>
/// <param name="StaffIds">Only these columns; null or empty for everybody the caller may see.</param>
public sealed record CalendarQuery(DateOnly From, DateOnly To, IReadOnlyList<Guid>? StaffIds);

public sealed record CalendarSpanDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed record CalendarAbsenceDto(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, AbsenceType Type);

public sealed record CalendarBlockDto(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Label);

/// <param name="Working">The person's working time inside the opening hours; everything else in the column is "off".</param>
/// <param name="Appointments">Open, confirmed and finished appointments (cancelled, declined and expired ones are not shown).</param>
public sealed record CalendarColumnDto(
    Guid StaffId,
    string DisplayName,
    string Color,
    IReadOnlyList<CalendarSpanDto> Working,
    IReadOnlyList<CalendarAbsenceDto> Absences,
    IReadOnlyList<CalendarBlockDto> Blocks,
    IReadOnlyList<AppointmentDto> Appointments);

/// <param name="SlotMinutes">The grid of start times; the calendar snaps to it.</param>
public sealed record CalendarDto(DateOnly From, DateOnly To, string TimeZone, int SlotMinutes, IReadOnlyList<CalendarColumnDto> Columns);

public sealed record CustomerHitDto(Guid Id, string Name, string? Email, string? Phone);

/// <param name="CustomerId">An existing customer; otherwise <paramref name="CustomerName"/> (and optionally e-mail and phone) makes or finds one.</param>
/// <param name="Note">Internal note; the customer never sees it.</param>
public sealed record CreateManualAppointmentRequest(
    Guid ServiceId,
    Guid StaffId,
    DateTimeOffset StartUtc,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Note);

/// <param name="StaffId">Another person's column; null keeps the person.</param>
/// <param name="Reason">Optional explanation for the customer's mail.</param>
public sealed record MoveAppointmentRequest(DateTimeOffset StartUtc, Guid? StaffId, string? Reason);

public sealed record CreateBlockRequest(Guid StaffId, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Label);
