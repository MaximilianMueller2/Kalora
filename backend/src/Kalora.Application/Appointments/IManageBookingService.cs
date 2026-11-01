using Kalora.Application.Common;

namespace Kalora.Application.Appointments;

/// <summary>A customer's own appointment, reached through the token in their link. No account, no other credential.</summary>
public interface IManageBookingService
{
    /// <summary>Errors: <c>appointment.not_found</c> (unknown token, switched-off salon, or link older than 30 days after the appointment).</summary>
    Task<Result<ManagedAppointmentDto>> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an open request or a confirmed appointment up to the cancellation period. The time is free again at once, the
    /// customer gets a confirmation (with the cancelling .ics) and the salon is told.
    /// Errors: <c>appointment.cancellation_deadline_passed</c>, <c>appointment.invalid_transition</c> (already declined, expired, cancelled or over).
    /// </summary>
    Task<Result<ManagedAppointmentDto>> CancelAsync(string token, CancellationToken cancellationToken = default);
}
