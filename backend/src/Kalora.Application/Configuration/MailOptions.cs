using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

public enum SmtpSecurity
{
    /// <summary>No encryption: only for a local mail catcher such as Mailpit.</summary>
    None,

    /// <summary>Connect plain, then upgrade with STARTTLS (usually port 587).</summary>
    StartTls,

    /// <summary>TLS from the first byte (usually port 465).</summary>
    SslOnConnect,
}

/// <summary>Sender, SMTP server and background worker of the e-mail outbox. Secrets come from the environment, never from the repository.</summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Kalora";

    public SmtpOptions Smtp { get; set; } = new();

    public WorkerOptions Worker { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

        /// <summary>Optional; without a user name the server is used without signing in.</summary>
        public string? User { get; set; }

        public string? Password { get; set; }

        public int TimeoutSeconds { get; set; } = 30;
    }

    public sealed class WorkerOptions
    {
        /// <summary>Turn off in tests and in instances that must not send (only one instance should run the worker).</summary>
        public bool Enabled { get; set; } = true;

        public int PollSeconds { get; set; } = 10;

        public int BatchSize { get; set; } = 20;
    }
}

internal sealed class MailOptionsValidator : IValidateOptions<MailOptions>
{
    public ValidateOptionsResult Validate(string? name, MailOptions options)
    {
        var errors = new List<string>();
        if (!System.Net.Mail.MailAddress.TryCreate(options.FromAddress, out _))
        {
            errors.Add($"{MailOptions.SectionName}:{nameof(MailOptions.FromAddress)} must be an e-mail address.");
        }

        if (options.Worker.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.Smtp.Host))
            {
                errors.Add($"{MailOptions.SectionName}:Smtp:Host is required while the mail worker is enabled.");
            }

            if (options.Smtp.Port is < 1 or > 65535)
            {
                errors.Add($"{MailOptions.SectionName}:Smtp:Port must be between 1 and 65535.");
            }

            if (options.Worker.PollSeconds < 1 || options.Worker.BatchSize < 1)
            {
                errors.Add($"{MailOptions.SectionName}:Worker:PollSeconds and BatchSize must be at least 1.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
