using Kalora.Application.Configuration;

namespace Kalora.UnitTests.Configuration;

public class AppOptionsValidatorTests
{
    private readonly AppOptionsValidator _validator = new();

    [Theory]
    [InlineData("https://kalora.app")]
    [InlineData("http://localhost:4200")]
    public void Accepts_absolute_http_urls(string url) =>
        Assert.True(_validator.Validate(null, new AppOptions { PublicBaseUrl = url }).Succeeded);

    [Theory]
    [InlineData("")]
    [InlineData("kalora.app")]
    [InlineData("ftp://kalora.app")]
    public void Rejects_everything_else(string url) =>
        Assert.True(_validator.Validate(null, new AppOptions { PublicBaseUrl = url }).Failed);
}
