using Kalora.Application.Configuration;

namespace Kalora.UnitTests.Configuration;

public class AppOptionsValidatorTests
{
    private const string Secret = "0123456789-0123456789-0123456789-ab";

    private readonly AppOptionsValidator _validator = new();

    [Theory]
    [InlineData("https://kalora.app")]
    [InlineData("http://localhost:4200")]
    public void Accepts_absolute_http_urls(string url) =>
        Assert.True(_validator.Validate(null, new AppOptions { PublicBaseUrl = url, ManageTokenSecret = Secret }).Succeeded);

    [Theory]
    [InlineData("")]
    [InlineData("kalora.app")]
    [InlineData("ftp://kalora.app")]
    public void Rejects_everything_else(string url) =>
        Assert.True(_validator.Validate(null, new AppOptions { PublicBaseUrl = url, ManageTokenSecret = Secret }).Failed);

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("0123456789-0123456789-012345")] // 29 characters
    public void Needs_a_long_secret_for_the_management_links(string secret)
    {
        var result = _validator.Validate(null, new AppOptions { PublicBaseUrl = "https://kalora.app", ManageTokenSecret = secret });

        Assert.True(result.Failed);
        Assert.Contains("ManageTokenSecret", result.FailureMessage);
    }

    [Fact]
    public void Accepts_a_secret_of_exactly_the_minimum_length() =>
        Assert.True(_validator.Validate(null, new AppOptions { PublicBaseUrl = "https://kalora.app", ManageTokenSecret = new string('x', AppOptions.MinSecretLength) }).Succeeded);

    [Fact]
    public void Reports_every_problem_at_once()
    {
        var result = _validator.Validate(null, new AppOptions { PublicBaseUrl = "nope", ManageTokenSecret = "short" });

        Assert.Contains("PublicBaseUrl", result.FailureMessage);
        Assert.Contains("ManageTokenSecret", result.FailureMessage);
    }
}
