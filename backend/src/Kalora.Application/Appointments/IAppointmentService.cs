using Kalora.Application.Common;

namespace Kalora.Application.Appointments;

/// <summary>
/// The salon's work on appointments: answering requests, cancelling, marking what happened. Owners and managers see and change
/// every appointment of the salon, a staff member only their own (everything else is "not found" to them).
/// </summary>
public interface IAppointmentService
{
    /// <summary>Open requests, those that run out first at the top.</summary>
    Task<Result<IReadOnlyList<AppointmentDto>>> ListOpenRequestsAsync(CancellationToken cancellationToken = default);

    Task<Result<OpenRequestCount>> CountOpenRequestsAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirmed appointments that are over but not yet marked as attended or missed, oldest first.</summary>
    Task<Result<IReadOnlyList<AppointmentDto>>> ListAwaitingOutcomeAsync(CancellationToken cancellationToken = default);

    Task<Result<AppointmentDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Accepts a request. <c>appointment.request_expired</c> once its time to answer is over.</summary>
    Task<Result<AppointmentDto>> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AppointmentDto>> DeclineAsync(Guid id, ReasonRequest request, CancellationToken cancellationToken = default);

    /// <summary>The salon cancels; the time is free again at once and the customer is told, with the reason if given.</summary>
    Task<Result<AppointmentDto>> CancelAsync(Guid id, ReasonRequest request, CancellationToken cancellationToken = default);

    /// <summary>The customer came. Only once the appointment has begun (<c>appointment.not_started</c>).</summary>
    Task<Result<AppointmentDto>> MarkAttendedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The customer did not come. Counts for the no-show rate.</summary>
    Task<Result<AppointmentDto>> MarkNoShowAsync(Guid id, CancellationToken cancellationToken = default);
}
