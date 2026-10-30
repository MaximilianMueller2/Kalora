using System.Net.Sockets;
using Kalora.Application.Configuration;
using Kalora.Application.Email;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kalora.Infrastructure.Mail;

/// <summary>Delivers mail over SMTP with MailKit. One connection per mail: the volume is small and a stale connection cannot cause surprises.</summary>
internal sealed class MailKitEmailSender(IOptions<MailOptions> options) : IEmailSender
{
    public async Task<SendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        MimeMessage mime;
        try
        {
            mime = MailMessageFactory.Create(settings, message);
        }
        catch (ParseException exception)
        {
            return SendOutcome.Permanent($"Invalid address: {exception.GetType().Name}");
        }

        try
        {
            using var client = new SmtpClient { Timeout = settings.Smtp.TimeoutSeconds * 1000 };
            await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, ToSocketOptions(settings.Smtp.Security), cancellationToken);
            if (!string.IsNullOrEmpty(settings.Smtp.User))
            {
                await client.AuthenticateAsync(settings.Smtp.User, settings.Smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            return SendOutcome.Sent;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SmtpCommandException exception)
        {
            return Classify(exception);
        }
        catch (Exception exception) when (exception is SmtpProtocolException or ServiceNotConnectedException or ServiceNotAuthenticatedException
            or AuthenticationException or SslHandshakeException or IOException or SocketException or TimeoutException)
        {
            // The server or the network has a problem; it may be gone in a minute.
            return SendOutcome.Temporary($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>4xx answers and unknown errors are worth another try; 5xx answers (bad recipient, rejected content) are final.</summary>
    internal static SendOutcome Classify(SmtpCommandException exception)
    {
        var code = (int)exception.StatusCode;
        var text = $"SMTP {code} ({exception.ErrorCode}): {exception.Message}";
        return code >= 500 || exception.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted
            ? SendOutcome.Permanent(text)
            : SendOutcome.Temporary(text);
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };
}
