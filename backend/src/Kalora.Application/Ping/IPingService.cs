using Kalora.Application.Common;

namespace Kalora.Application.Ping;

public interface IPingService
{
    Result<PingResponse> Ping();
}
