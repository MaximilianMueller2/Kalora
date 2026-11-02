using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Customers;

/// <summary>
/// Cuts every link between a customer and their data, for the salon's "delete customer" and for the retention job. It reads and
/// changes rows of one salon without a signed-in salon (listed in the architecture test). Saving is up to the caller.
/// </summary>
internal sealed class CustomerAnonymizer(IAppDbContext db)
{
    public async Task AnonymizeAsync(Customer customer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var email = customer.Email;
        customer.Anonymize(now);

        var appointments = await db.AllTenants<Appointment>().Where(a => a.CustomerId == customer.Id).ToListAsync(cancellationToken);
        foreach (var appointment in appointments)
        {
            appointment.CustomerNote = null;
            appointment.InternalNote = null;
            appointment.ManageTokenHash = null;
        }

        await EmptyMailsAsync(customer.SalonId, email, appointments.Select(a => a.Id), cancellationToken);
    }

    /// <summary>
    /// Mails to the customer and mails to the salon about their appointments lose their content. Those still waiting to be sent
    /// are given up, because their data is gone.
    /// </summary>
    private async Task EmptyMailsAsync(Guid salonId, string? email, IEnumerable<Guid> appointmentIds, CancellationToken cancellationToken)
    {
        var queries = new List<IQueryable<EmailOutbox>>();
        if (!string.IsNullOrEmpty(email))
        {
            queries.Add(db.AllTenants<EmailOutbox>().Where(m => m.SalonId == salonId && m.ToEmail == email));
        }

        // The payload is JSON text with the appointment's ID in it; the IDs are unique, so a text match is exact.
        queries.AddRange(appointmentIds.Select(id => id.ToString()).Select(id =>
            db.AllTenants<EmailOutbox>().Where(m => m.SalonId == salonId && m.PayloadJson != null && m.PayloadJson.Contains(id))));

        foreach (var query in queries)
        {
            await query.Where(m => m.Status == EmailStatus.Pending || m.Status == EmailStatus.Sending).ExecuteUpdateAsync(
                set => set.SetProperty(m => m.Status, EmailStatus.Failed).SetProperty(m => m.NextAttemptAtUtc, (DateTimeOffset?)null).SetProperty(m => m.LastError, "Customer anonymized"),
                cancellationToken);
            await query.ExecuteUpdateAsync(
                set => set.SetProperty(m => m.PayloadJson, (string?)null).SetProperty(m => m.BodyHtml, (string?)null).SetProperty(m => m.BodyText, (string?)null).SetProperty(m => m.Subject, string.Empty),
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(email))
        {
            await db.AllTenants<EmailOutbox>().Where(m => m.SalonId == salonId && m.ToEmail == email)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.ToEmail, string.Empty), cancellationToken);
        }
    }
}
