namespace Kalora.Application.Customers;

/// <param name="Customers">Customers without a visit for the salon's retention period, now anonymized.</param>
/// <param name="Links">Management links of appointments that ended long ago, now removed.</param>
/// <param name="Invitations">Used or expired invitations, now deleted.</param>
/// <param name="Mails">Sent or failed mails whose content was emptied.</param>
public sealed record RetentionResult(int Customers, int Links, int Invitations, int Mails)
{
    public int Total => Customers + Links + Invitations + Mails;
}

/// <summary>
/// Keeps personal data only as long as it is needed, for all salons: anonymizes customers who did not come for a long time, removes
/// old links and invitations and empties old mails. Called every day by the background job.
/// </summary>
public interface IRetentionService
{
    Task<RetentionResult> RunAsync(CancellationToken cancellationToken = default);
}
