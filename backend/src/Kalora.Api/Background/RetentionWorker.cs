using Kalora.Application.Configuration;
using Kalora.Application.Customers;
using Kalora.Application.Common;
using Microsoft.Extensions.Options;

namespace Kalora.Api.Background;

/// <summary>
/// Runs the retention job (see <see cref="IRetentionService"/>) at start and then every Retention:IntervalHours. Only one instance
/// should have it enabled, see Retention:Enabled. A failing run is logged and tried again at the next interval.
/// </summary>
internal sealed partial class RetentionWorker(IServiceScopeFactory scopes, IOptions<RetentionOptions> options, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.IntervalHours));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRetentionService>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogRunFailed(logger, PersonalData.ForLog(exception));
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

    [LoggerMessage(Level = LogLevel.Error, Message = "The retention job failed; trying again at the next interval")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
