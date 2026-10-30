namespace Kalora.Application.Appointments;

/// <param name="Expired">Open requests that ran out of time and were told to the customer.</param>
/// <param name="Reminders">Reminder mails queued for confirmed appointments that are about to begin.</param>
public sealed record SchedulerResult(int Expired, int Reminders)
{
    public int Total => Expired + Reminders;
}

/// <summary>The recurring housekeeping of appointments, for all salons. Called every few minutes by the background job.</summary>
public interface IAppointmentScheduler
{
    Task<SchedulerResult> RunAsync(CancellationToken cancellationToken = default);
}
