using Kalora.Application.Ping;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.UnitTests.Ping;

public class PingServiceTests
{
    [Fact]
    public void Ping_returns_ok_with_current_utc_time()
    {
        var now = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);
        var service = new PingService(new FakeTimeProvider(now));

        var result = service.Ping();

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value.Status);
        Assert.Equal(now, result.Value.TimeUtc);
    }
}
