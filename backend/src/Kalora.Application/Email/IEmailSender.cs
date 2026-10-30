namespace Kalora.Application.Email;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <param name="To">A single recipient.</param>
public sealed record EmailMessage(string To, string Subject, string Html, string Text, IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>What happened to one attempt to deliver a mail.</summary>
/// <param name="IsPermanent">Trying again will not help (the address does not exist, the message is invalid).</param>
public sealed record SendOutcome(bool Succeeded, bool IsPermanent, string? Error)
{
    public static SendOutcome Sent { get; } = new(true, false, null);

    public static SendOutcome Temporary(string error) => new(false, false, error);

    public static SendOutcome Permanent(string error) => new(false, true, error);
}

/// <summary>Delivers one mail to the mail server. Implemented by Infrastructure.</summary>
public interface IEmailSender
{
    /// <summary>Never throws for delivery problems; they come back as a <see cref="SendOutcome"/>. Cancellation still throws.</summary>
    Task<SendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
