using Kalora.Application.Appointments;
using Kalora.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Kalora.Api.Background;

/// <summary>
/// Runs the appointment scheduler every few minutes (Scheduler:IntervalMinutes). Only one instance should have it enabled,
/// see Scheduler:Enabled. A failing run is logged and tried again at the next interval.
/// </summary>
internal sealed partial class AppointmentSchedulerWorker(IServiceScopeFactory scopes, IOptions<SchedulerOptions> options, ILogger<AppointmentSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.IntervalMinutes);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IAppointmentScheduler>();
                SchedulerResult result;
                do
                {
                    result = await scheduler.RunAsync(stoppingToken);
                }
                while (result.Expired >= options.Value.BatchSize || result.Reminders >= options.Value.BatchSize); // a full batch: more is waiting
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database may be restarting; the job must survive that.
                LogRunFailed(logger, exception);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The appointment scheduler failed; trying again at the next interval")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
