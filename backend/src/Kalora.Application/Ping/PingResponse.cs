namespace Kalora.Application.Ping;

public sealed record PingResponse(string Status, DateTimeOffset TimeUtc);
