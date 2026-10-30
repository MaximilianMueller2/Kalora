using Kalora.Application.Configuration;

namespace Kalora.UnitTests.Configuration;

public class SchedulerOptionsValidatorTests
{
    private readonly SchedulerOptionsValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid_and_run_every_five_minutes()
    {
        var options = new SchedulerOptions();

        Assert.True(_validator.Validate(null, options).Succeeded);
        Assert.Equal((true, 5, 24), (options.Enabled, options.IntervalMinutes, options.ReminderHoursBefore));
    }

    [Theory]
    [InlineData(0, 100, 24, "IntervalMinutes")]
    [InlineData(5, 0, 24, "BatchSize")]
    [InlineData(5, 100, 0, "ReminderHoursBefore")]
    public void Nonsense_values_are_refused_by_name(int interval, int batch, int hours, string named)
    {
        var result = _validator.Validate(null, new SchedulerOptions { IntervalMinutes = interval, BatchSize = batch, ReminderHoursBefore = hours });

        Assert.True(result.Failed);
        Assert.Contains(named, result.FailureMessage);
    }
}
