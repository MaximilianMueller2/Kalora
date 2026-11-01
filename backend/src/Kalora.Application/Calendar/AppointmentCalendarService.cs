using Kalora.Application.Appointments;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Calendar;

/// <summary>
/// Serves the calendar file behind the customer's management link. The URL has no salon in it, so the appointment is found
/// by the hash of its token across all salons (the lookup is the credential check); nothing else is read except what the file contains.
/// </summary>
internal sealed class AppointmentCalendarService(
    IAppDbContext db,
    ManageTokenLookup lookup,
    ICalendarFileService files,
    IOptions<AppOptions> appOptions,
    IOptions<MailOptions> mailOptions) : IAppointmentCalendarService
{
    public async Task<Result<CalendarFile>> GetFileAsync(string token, CancellationToken cancellationToken = default)
    {
        var found = await lookup.FindAsync(token, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (appointment, salon) = (found.Value.Appointment, found.Value.Salon);
        var method = appointment.Status switch
        {
            AppointmentStatus.Confirmed or AppointmentStatus.Completed or AppointmentStatus.NoShow => CalendarMethod.Publish,
            AppointmentStatus.Cancelled => CalendarMethod.Cancel,
            _ => (CalendarMethod?)null,
        };
        if (method is null)
        {
            return Error.Conflict("appointment.calendar_unavailable");
        }

        var staffName = await db.AllTenants<StaffMember>().Where(s => s.Id == appointment.StaffId).Select(s => s.DisplayName).FirstOrDefaultAsync(cancellationToken);
        var customer = await db.AllTenants<Customer>().Where(c => c.Id == appointment.CustomerId).Select(c => new { c.Name, c.Email }).FirstOrDefaultAsync(cancellationToken);
        var source = new CalendarSource(
            appointment.Id,
            appointment.CalendarSequence,
            appointment.StartUtc,
            appointment.EndUtc,
            salon.TimeZone,
            appointment.UpdatedAtUtc,
            salon.Name,
            SalonAddressText.Format(salon),
            appointment.ServiceName,
            staffName,
            AppLinks.ManageAppointment(appOptions.Value.PublicBaseUrl, token),
            appointment.Language,
            customer?.Name);

        var entry = CalendarEntryFactory.Create(source, method.Value, mailOptions.Value.FromAddress, customer?.Email);
        return new CalendarFile(CalendarAttachments.FileName, files.Create(entry));
    }
}
