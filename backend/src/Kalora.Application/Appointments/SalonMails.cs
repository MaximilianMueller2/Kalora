using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Appointments;

/// <summary>
/// Tells the salon about an appointment: the owners and managers, and the person the appointment is with if they have a login.
/// The members are looked up for the salon of the appointment, so this also works for customers who act without a signed-in salon
/// (listed in the architecture test).
/// </summary>
internal sealed class SalonMails(IAppDbContext db, IEmailOutbox outbox, IOptions<AppOptions> appOptions)
{
    public async Task QueueAsync(string type, Salon salon, Appointment appointment, string staffName, Customer customer, CancellationToken cancellationToken)
    {
        var staffUserId = await db.AllTenants<StaffMember>().Where(s => s.Id == appointment.StaffId).Select(s => s.UserId).FirstOrDefaultAsync(cancellationToken);
        var recipients = await (
            from member in db.AllTenants<SalonMember>()
            join user in db.Users on member.UserId equals user.Id
            where member.SalonId == salon.Id && (member.Role == SalonRole.Owner || member.Role == SalonRole.Manager || member.UserId == staffUserId)
            select user.Email).Distinct().ToListAsync(cancellationToken);

        var baseUrl = appOptions.Value.PublicBaseUrl;
        foreach (var recipient in recipients.Where(email => !string.IsNullOrEmpty(email)))
        {
            outbox.Enqueue(salon.Id, recipient!, type, salon.DefaultLanguage, new
            {
                SalonName = salon.Name,
                appointment.ServiceName,
                StaffName = staffName,
                appointment.StartUtc,
                appointment.EndUtc,
                salon.TimeZone,
                Status = appointment.Status.ToString(),
                CustomerName = customer.Name,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                appointment.CustomerNote,
                RespondByUtc = appointment.ExpiresAtUtc,
                AppointmentId = appointment.Id,
                RequestUrl = AppLinks.Request(baseUrl, appointment.Id),
                CalendarUrl = AppLinks.Calendar(baseUrl),
            });
        }
    }
}
