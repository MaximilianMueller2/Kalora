using System.Text.RegularExpressions;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Email;

/// <summary>
/// Sends queued mails with retries: a failed attempt is repeated later (see <see cref="RetryPolicy"/>), a permanent error
/// or the fifth failure ends in <see cref="EmailStatus.Failed"/>. The outbox of all salons is read here on purpose, because
/// the sender is a background job without a tenant (listed in the architecture test).
/// </summary>
internal sealed partial class EmailOutboxProcessor(
    IAppDbContext db,
    IEmailRenderer renderer,
    IEmailSender sender,
    IOptions<MailOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxProcessor> logger) : IEmailOutboxProcessor
{
    /// <summary>A mail still "sending" after this long belongs to a process that died; it is queued again.</summary>
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(10);

    private const int MaxErrorLength = 500;

    public async Task<ProcessResult> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await RequeueStuckAsync(now, cancellationToken);

        var due = await db.AllTenants<EmailOutbox>()
            .Where(mail => mail.Status == EmailStatus.Pending && (mail.NextAttemptAtUtc == null || mail.NextAttemptAtUtc <= now))
            .OrderBy(mail => mail.NextAttemptAtUtc)
            .Take(options.Value.Worker.BatchSize)
            .ToListAsync(cancellationToken);

        int sent = 0, retried = 0, failed = 0;
        foreach (var mail in due)
        {
            // Claim the mail first: if this process dies while sending, it is found again as "stuck".
            mail.Status = EmailStatus.Sending;
            mail.Attempts++;
            await db.SaveChangesAsync(cancellationToken);

            var outcome = await DeliverAsync(mail, cancellationToken);
            Apply(mail, outcome, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);

            switch (mail.Status)
            {
                case EmailStatus.Sent:
                    sent++;
                    LogSent(logger, mail.Id, mail.Type, mail.Attempts);
                    break;
                case EmailStatus.Pending:
                    retried++;
                    LogRetry(logger, mail.Id, mail.Type, mail.Attempts, mail.LastError);
                    break;
                default:
                    failed++;
                    LogFailed(logger, mail.Id, mail.Type, mail.Attempts, mail.LastError);
                    break;
            }
        }

        return new ProcessResult(sent, retried, failed);
    }

    private async Task RequeueStuckAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stuck = await db.AllTenants<EmailOutbox>()
            .Where(mail => mail.Status == EmailStatus.Sending && mail.UpdatedAtUtc < now - StuckAfter)
            .ToListAsync(cancellationToken);
        foreach (var mail in stuck)
        {
            mail.Status = EmailStatus.Pending;
            mail.NextAttemptAtUtc = now;
        }

        if (stuck.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<SendOutcome> DeliverAsync(EmailOutbox mail, CancellationToken cancellationToken)
    {
        RenderedEmail rendered;
        try
        {
            rendered = renderer.Render(mail.Type, mail.Language, mail.PayloadJson);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            // A template or payload that cannot be rendered will not get better by waiting.
            return SendOutcome.Permanent($"Template error: {exception.GetType().Name}");
        }

        mail.Subject = rendered.Subject;
        var outcome = await sender.SendAsync(new EmailMessage(mail.ToEmail, rendered.Subject, rendered.Html, rendered.Text), cancellationToken);
        if (outcome.Succeeded)
        {
            mail.BodyHtml = rendered.Html;
            mail.BodyText = rendered.Text;
        }

        return outcome;
    }

    private static void Apply(EmailOutbox mail, SendOutcome outcome, DateTimeOffset now)
    {
        if (outcome.Succeeded)
        {
            mail.Status = EmailStatus.Sent;
            mail.SentAtUtc = now;
            mail.NextAttemptAtUtc = null;
            mail.LastError = null;
            return;
        }

        mail.LastError = Sanitize(outcome.Error);
        var next = outcome.IsPermanent ? null : RetryPolicy.NextAttempt(now, mail.Attempts);
        if (next is null)
        {
            mail.Status = EmailStatus.Failed;
            mail.NextAttemptAtUtc = null;
        }
        else
        {
            mail.Status = EmailStatus.Pending;
            mail.NextAttemptAtUtc = next;
        }
    }

    /// <summary>Server messages like "550 5.1.1 someone@example.com does not exist" must not put addresses into logs or the error column.</summary>
    internal static string Sanitize(string? error)
    {
        var text = AddressPattern().Replace(error ?? "unknown error", "<address>");
        return text.Length <= MaxErrorLength ? text : text[..MaxErrorLength];
    }

    [GeneratedRegex(@"[^\s<>""',;()]+@[^\s<>""',;()]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex AddressPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Mail {MailId} ({Type}) sent after {Attempts} attempt(s)")]
    private static partial void LogSent(ILogger logger, Guid mailId, string type, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail {MailId} ({Type}) attempt {Attempts} failed, will retry: {Reason}")]
    private static partial void LogRetry(ILogger logger, Guid mailId, string type, int attempts, string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail {MailId} ({Type}) failed for good after {Attempts} attempt(s): {Reason}")]
    private static partial void LogFailed(ILogger logger, Guid mailId, string type, int attempts, string? reason);
}
