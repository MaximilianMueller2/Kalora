using Kalora.Application.Configuration;
using Kalora.Application.Email;
using Kalora.Infrastructure.Mail;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;

namespace Kalora.UnitTests.Email;

public class MailMessageTests
{
    private static readonly MailOptions Options = new() { FromAddress = "noreply@kalora.app", FromName = "Kalora" };

    private static readonly EmailMessage Message = new("kim@example.com", "Dein Termin bei Müller & Söhne ist bestätigt", "<p>Hallo <b>Kim</b></p>", "Hallo Kim");

    [Fact]
    public void The_message_has_sender_recipient_and_subject()
    {
        var mime = MailMessageFactory.Create(Options, Message);

        Assert.Equal("noreply@kalora.app", mime.From.Mailboxes.Single().Address);
        Assert.Equal("Kalora", mime.From.Mailboxes.Single().Name);
        Assert.Equal("kim@example.com", mime.To.Mailboxes.Single().Address);
        Assert.Equal("Dein Termin bei Müller & Söhne ist bestätigt", mime.Subject);
    }

    [Fact]
    public void Text_and_html_travel_as_alternatives_with_the_plain_text_first()
    {
        var mime = MailMessageFactory.Create(Options, Message);

        var alternative = Assert.IsType<MultipartAlternative>(mime.Body);
        Assert.Equal(["text/plain", "text/html"], alternative.OfType<TextPart>().Select(p => p.ContentType.MimeType));
        Assert.Equal("Hallo Kim", mime.TextBody?.TrimEnd());
        Assert.Contains("<b>Kim</b>", mime.HtmlBody);
    }

    [Fact]
    public void Umlauts_survive_the_trip_through_the_wire_format()
    {
        var mime = MailMessageFactory.Create(Options, Message);
        using var stream = new MemoryStream();
        mime.WriteTo(stream);
        stream.Position = 0;

        var parsed = MimeMessage.Load(stream);

        Assert.Equal(Message.Subject, parsed.Subject);
        Assert.Contains("Hallo Kim", parsed.TextBody);
    }

    [Fact]
    public void A_calendar_file_is_attached_so_that_the_phone_can_open_it_directly()
    {
        var ics = System.Text.Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        var mime = MailMessageFactory.Create(Options, Message with { Attachments = [new EmailAttachment("appointment.ics", "text/calendar; method=PUBLISH; charset=utf-8", ics)] });

        var attachment = Assert.Single(mime.Attachments.OfType<MimePart>());
        Assert.Equal("text/calendar", attachment.ContentType.MimeType);
        Assert.Equal("PUBLISH", attachment.ContentType.Parameters["method"]);
        Assert.Equal("appointment.ics", attachment.FileName);
        using var content = new MemoryStream();
        attachment.Content!.DecodeTo(content);
        Assert.Equal(ics, content.ToArray());
        Assert.Contains("<b>Kim</b>", mime.HtmlBody);
        Assert.Equal("Hallo Kim", mime.TextBody?.TrimEnd());
    }

    [Fact]
    public void Mails_without_attachments_stay_plain_alternatives()
    {
        var mime = MailMessageFactory.Create(Options, Message);

        Assert.Empty(mime.Attachments);
        Assert.IsType<MultipartAlternative>(mime.Body);
    }

    [Fact]
    public void Mails_are_marked_as_automatic_so_auto_replies_stay_away()
    {
        var mime = MailMessageFactory.Create(Options, Message);

        Assert.Equal("auto-generated", mime.Headers["Auto-Submitted"]);
        Assert.Equal("OOF, AutoReply", mime.Headers["X-Auto-Response-Suppress"]);
    }

    [Fact]
    public void A_subject_cannot_inject_headers()
    {
        var mime = MailMessageFactory.Create(Options, Message with { Subject = "Hi\r\nBcc: evil@example.com" });
        using var stream = new MemoryStream();
        mime.WriteTo(stream);

        Assert.Empty(mime.Bcc);
        Assert.DoesNotContain("\r\nBcc:", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("")]
    public void An_invalid_recipient_is_refused(string to)
    {
        Assert.ThrowsAny<ParseException>(() => MailMessageFactory.Create(Options, Message with { To = to }));
    }

    [Theory]
    [InlineData(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, true)]
    [InlineData(SmtpErrorCode.SenderNotAccepted, SmtpStatusCode.MailboxNameNotAllowed, true)]
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.MailboxBusy, false)]
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.InsufficientStorage, false)]
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, false)]
    public void Server_answers_are_permanent_or_worth_another_try(SmtpErrorCode error, SmtpStatusCode status, bool permanent)
    {
        var outcome = MailKitEmailSender.Classify(new SmtpCommandException(error, status, "answer"));

        Assert.False(outcome.Succeeded);
        Assert.Equal(permanent, outcome.IsPermanent);
        Assert.Contains(((int)status).ToString(), outcome.Error);
    }
}
