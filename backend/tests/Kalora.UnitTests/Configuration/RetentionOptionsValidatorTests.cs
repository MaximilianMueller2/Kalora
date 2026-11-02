using Kalora.Application.Configuration;

namespace Kalora.UnitTests.Configuration;

public class RetentionOptionsValidatorTests
{
    private readonly RetentionOptionsValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid_and_keep_mail_content_and_links_for_thirty_days()
    {
        var options = new RetentionOptions();

        Assert.True(_validator.Validate(null, options).Succeeded);
        Assert.Equal((true, 24, 30, 30), (options.Enabled, options.IntervalHours, options.MailContentDays, options.TokenDays));
    }

    [Theory]
    [InlineData(0, 100, 30, 30, "IntervalHours")]
    [InlineData(24, 0, 30, 30, "BatchSize")]
    [InlineData(24, 100, 0, 30, "MailContentDays")]
    [InlineData(24, 100, 30, 0, "TokenDays")]
    public void Nonsense_values_are_refused_by_name(int interval, int batch, int mailDays, int tokenDays, string named)
    {
        var result = _validator.Validate(null, new RetentionOptions { IntervalHours = interval, BatchSize = batch, MailContentDays = mailDays, TokenDays = tokenDays });

        Assert.True(result.Failed);
        Assert.Contains(named, result.FailureMessage);
    }
}
