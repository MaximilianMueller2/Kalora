using Kalora.Domain.Common;

namespace Kalora.Domain.Messaging;

/// <summary>
/// Every e-mail is stored first and sent by a background service, so nothing is lost when the mail server is down.
/// </summary>
public class EmailOutbox : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public string ToEmail { get; set; } = string.Empty;

    /// <summary>Template key, e.g. "appointment.requested".</summary>
    public string Type { get; set; } = string.Empty;

    public string Language { get; set; } = "de";

    public string Subject { get; set; } = string.Empty;

    /// <summary>Cleared after the retention period (data minimization).</summary>
    public string? BodyHtml { get; set; }

    public string? BodyText { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    public string? LastError { get; set; }
}
