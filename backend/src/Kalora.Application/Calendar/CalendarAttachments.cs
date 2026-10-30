using System.Text;
using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Email;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Calendar;

/// <summary>
/// The .ics file that goes with some mails: the confirmation carries the appointment (it opens straight in the calendar of an
/// iPhone), a cancellation carries its withdrawal. Built from the queued payload when the mail is sent.
/// </summary>
internal sealed class CalendarAttachments(ICalendarFileService files, IOptions<MailOptions> mail)
{
    public const string FileName = "appointment.ics";

    public EmailAttachment? Build(string type, string language, string recipient, string? payloadJson)
    {
        var method = type switch
        {
            EmailTypes.BookingConfirmed => CalendarMethod.Publish,
            EmailTypes.BookingCancelledByCustomer or EmailTypes.BookingCancelledBySalon => CalendarMethod.Cancel,
            _ => (CalendarMethod?)null,
        };
        if (method is null || string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payloadJson);
        if (CalendarSource.FromPayload(document.RootElement, EmailTexts.ResolveLanguage(language)) is not { } source)
        {
            return null;
        }

        var entry = CalendarEntryFactory.Create(source, method.Value, mail.Value.FromAddress, recipient);
        var contentType = $"text/calendar; method={(method == CalendarMethod.Cancel ? "CANCEL" : "PUBLISH")}; charset=utf-8";
        return new EmailAttachment(FileName, contentType, Encoding.UTF8.GetBytes(files.Create(entry)));
    }
}
