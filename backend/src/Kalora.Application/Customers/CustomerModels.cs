using Kalora.Domain.Appointments;

namespace Kalora.Application.Customers;

public sealed record CustomerListItemDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTimeOffset? LastVisitAtUtc,
    int Appointments,
    int NoShows);

/// <param name="HasMore">There are more customers than shown; narrow the search.</param>
public sealed record CustomerListDto(IReadOnlyList<CustomerListItemDto> Items, bool HasMore);

/// <summary>One appointment in a customer's history, with both notes (the customer's own and the salon's internal one).</summary>
public sealed record CustomerAppointmentDto(
    Guid Id,
    AppointmentStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ServiceName,
    string? StaffName,
    int PriceCents,
    bool PriceIsFrom,
    string Currency,
    string? CustomerNote,
    string? InternalNote);

public sealed record CustomerDetailDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string PreferredLanguage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastVisitAtUtc,
    int NoShows,
    DateTimeOffset? MarketingConsentAtUtc,
    bool Anonymized,
    IReadOnlyList<CustomerAppointmentDto> Appointments);
