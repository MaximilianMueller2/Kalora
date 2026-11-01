using Kalora.Domain.Appointments;

namespace Kalora.Application.Appointments;

/// <summary>
/// What the customer sees on their own page: the salon, the service, the time and the status, and what they may do. Nothing
/// else (no person, no notes, nothing about other customers).
/// </summary>
/// <param name="CancelUntilUtc">The last moment to cancel online: for a confirmed appointment the start minus the salon's cancellation period, for an open request the start.</param>
/// <param name="CanCancel">Still before <paramref name="CancelUntilUtc"/>. Afterwards the salon's phone number is what helps.</param>
/// <param name="CanAddToCalendar">Confirmed, so the .ics file and the calendar links make sense.</param>
/// <param name="CanRebook">Declined, expired or cancelled: the customer may ask for a new appointment on the salon's page.</param>
public sealed record ManagedAppointmentDto(
    AppointmentStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ServiceName,
    string SalonName,
    string SalonSlug,
    string SalonPhone,
    string SalonAddress,
    string TimeZone,
    DateTimeOffset? CancelUntilUtc,
    bool CanCancel,
    bool CanAddToCalendar,
    bool CanRebook);
