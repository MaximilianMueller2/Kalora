using Kalora.Domain.Appointments;

namespace Kalora.Application.Appointments;

public sealed record AppointmentStaffDto(Guid Id, string DisplayName, string Color);

/// <summary>Contact details are for the salon's own screens only; the public pages never get this type.</summary>
public sealed record AppointmentCustomerDto(string Name, string? Email, string? Phone);

/// <param name="BlockedStartUtc">Start and end including the service's buffers: the time that is really taken in the calendar.</param>
/// <param name="ExpiresAtUtc">Until when an open request may be answered.</param>
/// <param name="NextStates">What the status rules allow next; clients show only these actions.</param>
public sealed record AppointmentDto(
    Guid Id,
    AppointmentStatus Status,
    AppointmentSource Source,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset BlockedStartUtc,
    DateTimeOffset BlockedEndUtc,
    Guid ServiceId,
    string ServiceName,
    int PriceCents,
    bool PriceIsFrom,
    string Currency,
    AppointmentStaffDto Staff,
    Guid CustomerId,
    AppointmentCustomerDto Customer,
    string? CustomerNote,
    string? InternalNote,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<AppointmentStatus> NextStates);

/// <param name="Reason">Optional explanation shown to the customer in the mail.</param>
public sealed record ReasonRequest(string? Reason);

public sealed record OpenRequestCount(int Count);
