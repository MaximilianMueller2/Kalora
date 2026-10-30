using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Appointments;

/// <summary>
/// Queues the mails a customer gets about an appointment. One place writes the data the templates need (and the .ics file is
/// made from), so a booking, an answer of the salon and a reminder all describe the appointment the same way.
/// </summary>
internal sealed class AppointmentMails(IEmailOutbox outbox, IManageTokens manageTokens, IOptions<AppOptions> appOptions, TimeProvider timeProvider)
{
    public void QueueForCustomer(string type, Salon salon, Appointment appointment, string staffName, Customer customer, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        var baseUrl = appOptions.Value.PublicBaseUrl;
        outbox.Enqueue(salon.Id, customer.Email, type, appointment.Language, new
        {
            AppointmentId = appointment.Id,
            appointment.CalendarSequence,
            StampUtc = timeProvider.GetUtcNow(),
            SalonName = salon.Name,
            SalonAddress = SalonAddressText.Format(salon),
            SalonPhone = salon.Phone,
            appointment.ServiceName,
            StaffName = staffName,
            appointment.StartUtc,
            appointment.EndUtc,
            salon.TimeZone,
            CustomerName = customer.Name,
            RespondByUtc = appointment.ExpiresAtUtc,
            ManageUrl = AppLinks.ManageAppointment(baseUrl, salon.Slug, manageTokens.Create(appointment.Id)),
            BookingUrl = AppLinks.Booking(baseUrl, salon.Slug),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        });
    }
}
