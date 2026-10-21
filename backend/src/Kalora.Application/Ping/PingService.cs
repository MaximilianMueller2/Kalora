using Kalora.Application.Common;

namespace Kalora.Application.Ping;

internal sealed class PingService(TimeProvider timeProvider) : IPingService
{
    public Result<PingResponse> Ping() => new PingResponse("ok", timeProvider.GetUtcNow());
}
