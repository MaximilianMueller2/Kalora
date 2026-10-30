namespace Kalora.Application.Email;

/// <param name="Sent">Delivered to the mail server.</param>
/// <param name="Retried">Failed this time and scheduled for another attempt.</param>
/// <param name="Failed">Given up: a permanent error, or the last allowed attempt failed.</param>
public sealed record ProcessResult(int Sent, int Retried, int Failed)
{
    public int Total => Sent + Retried + Failed;
}

/// <summary>Works through the queued e-mails of all salons.</summary>
public interface IEmailOutboxProcessor
{
    /// <summary>Sends the mails that are due (at most one batch). Called again and again by the background worker.</summary>
    Task<ProcessResult> ProcessDueAsync(CancellationToken cancellationToken = default);
}
