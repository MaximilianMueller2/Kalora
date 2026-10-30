using Kalora.Application.Configuration;
using Kalora.Application.Email;
using Microsoft.Extensions.Options;

namespace Kalora.Api.Background;

/// <summary>
/// Sends the e-mail outbox. Runs in the API process (only one instance should have it enabled, see Mail:Worker:Enabled):
/// while there is work it keeps going, otherwise it waits for the poll interval.
/// </summary>
internal sealed partial class EmailOutboxWorker(IServiceScopeFactory scopes, IOptions<MailOptions> options, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ErrorPause = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(options.Value.Worker.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var pause = poll;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>().ProcessDueAsync(stoppingToken);
                if (result.Total >= options.Value.Worker.BatchSize)
                {
                    continue; // a full batch: there is probably more waiting
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database may be restarting; the worker must survive that.
                LogLoopFailed(logger, exception);
                pause = ErrorPause;
            }

            try
            {
                await Task.Delay(pause, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The mail worker failed; trying again shortly")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);
}
