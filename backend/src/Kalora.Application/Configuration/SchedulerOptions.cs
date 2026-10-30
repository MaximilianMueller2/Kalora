using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

/// <summary>The background job that expires unanswered requests and sends reminders.</summary>
public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>Turn off in tests and in instances that must not run it: only one instance should have it enabled.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 5;

    /// <summary>How many appointments one run handles per task; a full batch is followed by the next run at once.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>The reminder goes out this long before the start.</summary>
    public int ReminderHoursBefore { get; set; } = 24;
}

internal sealed class SchedulerOptionsValidator : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? name, SchedulerOptions options)
    {
        var errors = new List<string>();
        if (options.IntervalMinutes < 1)
        {
            errors.Add($"{SchedulerOptions.SectionName}:IntervalMinutes must be at least 1.");
        }

        if (options.BatchSize < 1)
        {
            errors.Add($"{SchedulerOptions.SectionName}:BatchSize must be at least 1.");
        }

        if (options.ReminderHoursBefore < 1)
        {
            errors.Add($"{SchedulerOptions.SectionName}:ReminderHoursBefore must be at least 1.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
