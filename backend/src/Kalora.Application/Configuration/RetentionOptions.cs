using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

/// <summary>The background job that keeps personal data only as long as it is needed.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Turn off in tests and in instances that must not run it: only one instance should have it enabled.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalHours { get; set; } = 24;

    /// <summary>Customers handled per run and salon group; a full batch is followed by the next at once.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Sent and failed mails lose their content (text, data, address) after this many days.</summary>
    public int MailContentDays { get; set; } = 30;

    /// <summary>Links for customers stop working, and used or expired invitations are deleted, this many days after.</summary>
    public int TokenDays { get; set; } = 30;
}

internal sealed class RetentionOptionsValidator : IValidateOptions<RetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, RetentionOptions options)
    {
        var errors = new List<string>();
        if (options.IntervalHours < 1)
        {
            errors.Add($"{RetentionOptions.SectionName}:IntervalHours must be at least 1.");
        }

        if (options.BatchSize < 1)
        {
            errors.Add($"{RetentionOptions.SectionName}:BatchSize must be at least 1.");
        }

        if (options.MailContentDays < 1 || options.TokenDays < 1)
        {
            errors.Add($"{RetentionOptions.SectionName}:MailContentDays and TokenDays must be at least 1.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
