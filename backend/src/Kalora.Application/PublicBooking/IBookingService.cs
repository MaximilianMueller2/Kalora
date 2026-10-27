using Kalora.Application.Common;

namespace Kalora.Application.PublicBooking;

public interface IBookingService
{
    /// <summary>
    /// A customer asks for an appointment, without an account. Re-checks the start time inside a transaction, picks
    /// the person for "anyone", finds or creates the customer by e-mail, stores the appointment with a snapshot of the
    /// service, and queues the e-mails. Errors: <c>booking.slot_unavailable</c> (also when someone else took the time
    /// a moment earlier), <c>booking.too_many_open_requests</c>, <c>service.not_found</c>, <c>staff.not_found</c>, validation codes.
    /// </summary>
    Task<Result<BookingReceipt>> RequestAsync(CreateAppointmentRequest request, CancellationToken cancellationToken = default);
}
