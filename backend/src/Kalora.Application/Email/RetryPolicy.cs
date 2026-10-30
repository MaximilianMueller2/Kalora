namespace Kalora.Application.Email;

/// <summary>When a mail that could not be delivered is tried again, and when to give up.</summary>
public static class RetryPolicy
{
    public const int MaxAttempts = 5;

    /// <summary>Wait after the 1st, 2nd, 3rd and 4th failed attempt; a 5th failure is final.</summary>
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

    /// <summary>The next attempt after <paramref name="attempts"/> failed ones, or null if the mail has used them all.</summary>
    public static DateTimeOffset? NextAttempt(DateTimeOffset now, int attempts) =>
        attempts >= MaxAttempts ? null : now + Backoff[Math.Max(attempts, 1) - 1];
}
