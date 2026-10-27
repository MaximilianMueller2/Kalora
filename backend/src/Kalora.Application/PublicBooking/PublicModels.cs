using Kalora.Application.Salons;
using Kalora.Domain.Appointments;

namespace Kalora.Application.PublicBooking;

/// <summary>What the public booking page shows about a salon. Nothing here is personal data of a customer.</summary>
public sealed record PublicSalonDto(
    string Name,
    string Slug,
    string Street,
    string PostalCode,
    string City,
    string Phone,
    string TimeZone,
    string DefaultLanguage,
    IReadOnlyList<string> Languages,
    IReadOnlyList<OpeningDay> OpeningHours,
    string? ImprintText,
    string? PrivacyPolicyUrl);

/// <summary>Buffers and internal flags are not shown to customers.</summary>
public sealed record PublicServiceDto(Guid Id, string Name, string? Description, int PriceCents, bool PriceIsFrom, string Currency, int DurationMinutes);

public sealed record PublicCategoryDto(string Name, IReadOnlyList<PublicServiceDto> Services);

/// <summary>A person a customer may choose: name and calendar color only.</summary>
public sealed record PublicStaffDto(Guid Id, string DisplayName, string Color);

/// <param name="Month">"yyyy-MM".</param>
/// <param name="Days">Days of the month that have at least one free start time.</param>
public sealed record PublicDaysDto(string Month, IReadOnlyList<DateOnly> Days);

/// <param name="Time">The start in the salon's local time ("09:30"), so clients need not convert.</param>
public sealed record PublicSlotDto(DateTimeOffset StartUtc, string Time);

public sealed record PublicSlotsDto(DateOnly Date, IReadOnlyList<PublicSlotDto> Slots);

/// <param name="StartUtc">One of the start times returned by the slots endpoint.</param>
/// <param name="StaffId">The wished person; null for "anyone".</param>
/// <param name="Language">Language of the confirmation e-mails; the salon's default if empty.</param>
/// <param name="Website">Honeypot: a hidden form field that humans leave empty. Bots fill it.</param>
public sealed record CreateAppointmentRequest(
    Guid ServiceId,
    DateTimeOffset StartUtc,
    Guid? StaffId,
    string Name,
    string Email,
    string? Phone,
    string? Note,
    string? Language,
    string? Website);

/// <summary>The customer's receipt: only their own booking, plus the secret for their management link.</summary>
/// <param name="Status"><c>Requested</c> if the salon still has to confirm, <c>Confirmed</c> with automatic confirmation.</param>
/// <param name="ManageToken">Part of the link in the confirmation e-mail; only its hash is stored.</param>
/// <param name="RespondByUtc">Until when the salon may answer a request; afterwards it expires.</param>
public sealed record BookingReceipt(
    AppointmentStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ServiceName,
    string StaffName,
    string ManageToken,
    DateTimeOffset? RespondByUtc);
