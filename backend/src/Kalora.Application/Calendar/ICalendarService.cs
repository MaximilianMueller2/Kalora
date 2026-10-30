using Kalora.Application.Appointments;
using Kalora.Application.Common;

namespace Kalora.Application.Calendar;

/// <summary>
/// The team calendar: what is in the columns, and the salon's own ways to change it. Owners and managers work on every
/// column, everyone else only on their own.
/// </summary>
public interface ICalendarService
{
    /// <summary>Errors: <c>calendar.range_invalid</c>.</summary>
    Task<Result<CalendarDto>> GetAsync(CalendarQuery query, CancellationToken cancellationToken = default);

    /// <summary>Up to eight customers whose name, e-mail or phone contains the text (at least two characters).</summary>
    Task<Result<IReadOnlyList<CustomerHitDto>>> SearchCustomersAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// A phone call or a walk-in: confirmed at once, source Manual, checked like an online booking
    /// (<c>booking.slot_unavailable</c> if the person is not free). A customer with an e-mail address gets the confirmation.
    /// </summary>
    Task<Result<AppointmentDto>> CreateAppointmentAsync(CreateManualAppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a confirmed appointment to another time and/or person with the same check as a booking. The customer gets
    /// a mail with the updated .ics (same UID, higher sequence). Errors: <c>appointment.not_movable</c>, <c>booking.slot_unavailable</c>.
    /// </summary>
    Task<Result<AppointmentDto>> MoveAppointmentAsync(Guid id, MoveAppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Private time, a break or a walk-in in a person's column. <c>calendar.block_overlaps_appointment</c> if an appointment is there.</summary>
    Task<Result<CalendarBlockDto>> CreateBlockAsync(CreateBlockRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteBlockAsync(Guid id, CancellationToken cancellationToken = default);
}
