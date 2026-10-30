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
    ICalendarFileService files,
    IOptions<AppOptions> appOptions,
    IOptions<MailOptions> mailOptions) : IAppointmentCalendarService
{
    private const int MaxTokenLength = 200;

    public async Task<Result<CalendarFile>> GetFileAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return Error.NotFound("appointment.not_found");
        }

        var hash = TokenHasher.Hash(token);
        var appointment = await db.AllTenants<Appointment>().AsNoTracking().FirstOrDefaultAsync(a => a.ManageTokenHash == hash, cancellationToken);
        var salon = appointment is null
            ? null
            : await db.AllSalons().AsNoTracking().FirstOrDefaultAsync(
                s => s.Id == appointment.SalonId && s.Status != SalonStatus.Suspended && s.Status != SalonStatus.Cancelled, cancellationToken);
        if (appointment is null || salon is null)
        {
            return Error.NotFound("appointment.not_found");
        }

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
            AppLinks.ManageAppointment(appOptions.Value.PublicBaseUrl, salon.Slug, token),
            appointment.Language,
            customer?.Name);

        var entry = CalendarEntryFactory.Create(source, method.Value, mailOptions.Value.FromAddress, customer?.Email);
        return new CalendarFile(CalendarAttachments.FileName, files.Create(entry));
    }
}
