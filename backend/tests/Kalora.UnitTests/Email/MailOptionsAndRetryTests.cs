using Kalora.Application.Configuration;
using Kalora.Application.Email;

namespace Kalora.UnitTests.Email;

public class MailOptionsAndRetryTests
{
    private static MailOptions Valid() => new()
    {
        FromAddress = "noreply@kalora.app",
        Smtp = { Host = "smtp.example.com", Port = 587 },
    };

    private readonly MailOptionsValidator _validator = new();

    [Fact]
    public void Complete_settings_are_valid() => Assert.True(_validator.Validate(null, Valid()).Succeeded);

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    public void The_sender_address_must_be_an_address(string from)
    {
        var options = Valid();
        options.FromAddress = from;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void A_running_worker_needs_a_server_and_a_sane_port_and_batch()
    {
        var noHost = Valid();
        noHost.Smtp.Host = " ";
        var badPort = Valid();
        badPort.Smtp.Port = 70000;
        var noBatch = Valid();
        noBatch.Worker.BatchSize = 0;

        Assert.True(_validator.Validate(null, noHost).Failed);
        Assert.True(_validator.Validate(null, badPort).Failed);
        Assert.True(_validator.Validate(null, noBatch).Failed);
    }

    [Fact]
    public void Without_a_worker_the_server_is_not_needed()
    {
        var options = Valid();
        options.Worker.Enabled = false;
        options.Smtp.Host = string.Empty;

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 120)]
    public void A_failed_attempt_is_repeated_after_a_growing_pause(int attempts, int minutes) =>
        Assert.Equal(Now.AddMinutes(minutes), RetryPolicy.NextAttempt(Now, attempts));

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(100)]
    public void After_the_fifth_attempt_it_is_over(int attempts) => Assert.Null(RetryPolicy.NextAttempt(Now, attempts));

    [Fact]
    public void Five_attempts_are_the_limit() => Assert.Equal(5, RetryPolicy.MaxAttempts);

    [Fact]
    public void The_pause_never_shrinks()
    {
        var pauses = Enumerable.Range(1, 4).Select(attempt => RetryPolicy.NextAttempt(Now, attempt)!.Value - Now).ToList();

        Assert.Equal(pauses.Order(), pauses);
    }
}
