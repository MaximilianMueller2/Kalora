using Kalora.Application.Configuration;
using Kalora.Application.Email;
using MimeKit;

namespace Kalora.Infrastructure.Mail;

/// <summary>Builds the MIME message: HTML with a text alternative, UTF-8, marked as automatic so out-of-office replies stay away.</summary>
public static class MailMessageFactory
{
    public static MimeMessage Create(MailOptions options, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Headers.Add("Auto-Submitted", "auto-generated");
        mime.Headers.Add("X-Auto-Response-Suppress", "OOF, AutoReply");
        mime.Body = new BodyBuilder { TextBody = message.Text, HtmlBody = message.Html }.ToMessageBody();
        return mime;
    }
}
