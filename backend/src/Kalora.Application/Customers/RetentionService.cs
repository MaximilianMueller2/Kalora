using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Customers;

/// <summary>The appointments, customers, invitations and mails of all salons are touched here on purpose (listed in the architecture test): the job has no tenant.</summary>
internal sealed partial class RetentionService(
    IAppDbContext db,
    CustomerAnonymizer anonymizer,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionService> logger) : IRetentionService
{
    public async Task<RetentionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var customers = await AnonymizeCustomersAsync(now, cancellationToken);
        var links = await RemoveLinksAsync(now, cancellationToken);
        var invitations = await DeleteInvitationsAsync(now, cancellationToken);
        var mails = await EmptyMailsAsync(now, cancellationToken);
        var result = new RetentionResult(customers, links, invitations, mails);
        if (result.Total > 0)
        {
            LogRun(logger, customers, links, invitations, mails);
        }

        return result;
    }

    /// <summary>
    /// A customer whose last appointment ended before the salon's retention period (or who never had one and was created that long
    /// ago) is anonymized. Anyone with an appointment since then, including a future one, is not touched.
    /// </summary>
    private async Task<int> AnonymizeCustomersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var done = 0;
        var periods = await db.AllSalons().Select(s => s.RetentionMonths).Distinct().ToListAsync(cancellationToken);
        foreach (var months in periods)
        {
            var cutoff = now.AddMonths(-months);
            while (true)
            {
                var batch = await (
                    from customer in db.AllTenants<Customer>()
                    join salon in db.AllSalons() on customer.SalonId equals salon.Id
                    where salon.RetentionMonths == months && customer.AnonymizedAtUtc == null
                    let last = db.AllTenants<Appointment>().Where(a => a.CustomerId == customer.Id).Max(a => (DateTimeOffset?)a.EndUtc)
                    where (last ?? customer.CreatedAtUtc) < cutoff
                    select customer).Take(options.Value.BatchSize).ToListAsync(cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var customer in batch)
                {
                    await anonymizer.AnonymizeAsync(customer, now, cancellationToken);
                    db.AuditLogs.Add(new AuditLog
                    {
                        SalonId = customer.SalonId,
                        ActorUserId = null,
                        Action = "customer.anonymized",
                        EntityType = nameof(Customer),
                        EntityId = customer.Id,
                        Details = JsonSerializer.Serialize(new { reason = "retention", months }),
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
                done += batch.Count;
            }
        }

        return done;
    }

    private Task<int> RemoveLinksAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.Value.TokenDays);
        return db.AllTenants<Appointment>().Where(a => a.ManageTokenHash != null && a.EndUtc < cutoff)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.ManageTokenHash, (string?)null), cancellationToken);
    }

    private Task<int> DeleteInvitationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.Value.TokenDays);
        return db.AllTenants<StaffInvitation>()
            .Where(i => (i.AcceptedAtUtc != null && i.AcceptedAtUtc < cutoff) || i.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<int> EmptyMailsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.Value.MailContentDays);
        return db.AllTenants<EmailOutbox>()
            .Where(m => (m.Status == EmailStatus.Sent || m.Status == EmailStatus.Failed) && m.CreatedAtUtc < cutoff
                && (m.BodyHtml != null || m.BodyText != null || m.PayloadJson != null || m.Subject != string.Empty || m.ToEmail != string.Empty))
            .ExecuteUpdateAsync(
                set => set.SetProperty(m => m.BodyHtml, (string?)null).SetProperty(m => m.BodyText, (string?)null)
                    .SetProperty(m => m.PayloadJson, (string?)null).SetProperty(m => m.Subject, string.Empty).SetProperty(m => m.ToEmail, string.Empty),
                cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention: {Customers} customer(s) anonymized, {Links} link(s) removed, {Invitations} invitation(s) deleted, {Mails} mail(s) emptied")]
    private static partial void LogRun(ILogger logger, int customers, int links, int invitations, int mails);
}
