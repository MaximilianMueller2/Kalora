using Kalora.Api.Http;

namespace Kalora.UnitTests.Http;

public class LogPathsTests
{
    [Theory]
    [InlineData("/api/public/appointments/AbCd-1234_secret", "/api/public/appointments/{token}")]
    [InlineData("/api/public/appointments/AbCd-1234_secret/cancel", "/api/public/appointments/{token}/cancel")]
    [InlineData("/api/public/appointments/AbCd-1234_secret/calendar.ics", "/api/public/appointments/{token}/calendar.ics")]
    [InlineData("/api/public/invitations/tok3n/accept", "/api/public/invitations/{token}/accept")]
    [InlineData("/API/PUBLIC/INVITATIONS/tok3n", "/API/PUBLIC/INVITATIONS/{token}")]
    public void The_secret_in_a_path_is_replaced(string path, string expected)
    {
        Assert.Equal(expected, LogPaths.Redact(path));
    }

    [Theory]
    [InlineData("/api/public/salons/haarwerk/services")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/customers")]
    [InlineData("/health")]
    public void Other_paths_are_logged_as_they_are(string path)
    {
        Assert.Equal(path, LogPaths.Redact(path));
    }
}
