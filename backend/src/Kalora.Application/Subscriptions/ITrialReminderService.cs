namespace Kalora.Application.Subscriptions;

/// <summary>Tells the owners that the trial is about to end: one mail about a week before and one about a day before.</summary>
public interface ITrialReminderService
{
    Task<TrialReminderResult> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>How many salons got which mail in one run.</summary>
public sealed record TrialReminderResult(int Week, int Day)
{
    public int Total => Week + Day;
}
