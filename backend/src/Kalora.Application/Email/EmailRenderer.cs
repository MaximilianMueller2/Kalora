using System.Globalization;
using System.Text.Json;
using Kalora.Application.Calendar;
using Kalora.Application.Common;

namespace Kalora.Application.Email;

/// <summary>
/// Builds a mail from its template key and the JSON payload that was queued with it. The payload names are the
/// ones the services write (<c>SalonName</c>, <c>StartUtc</c>, <c>ManageUrl</c>, ...).
/// </summary>
internal sealed class EmailRenderer : IEmailRenderer
{
    private const string AccountBrand = "Kalora";

    private static readonly string[] CustomerTypes =
    [
        EmailTypes.BookingRequestReceived, EmailTypes.BookingConfirmed, EmailTypes.BookingDeclined, EmailTypes.BookingExpired,
        EmailTypes.BookingCancelledByCustomer, EmailTypes.BookingCancelledBySalon, EmailTypes.BookingReminder, EmailTypes.BookingRescheduled,
    ];

    private static readonly string[] SalonTypes = [EmailTypes.BookingNewForSalon, EmailTypes.BookingCancellationForSalon];

    /// <summary>Mails whose button leads back to the booking page instead of the appointment.</summary>
    private static readonly string[] BookAgainTypes =
        [EmailTypes.BookingDeclined, EmailTypes.BookingExpired, EmailTypes.BookingCancelledByCustomer, EmailTypes.BookingCancelledBySalon];

    public RenderedEmail Render(string type, string language, string? payloadJson)
    {
        var texts = EmailTexts.For(EmailTexts.ResolveLanguage(language));
        if (!texts.Mails.TryGetValue(type, out var mail))
        {
            throw new InvalidOperationException($"No mail template for '{type}'.");
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        var payload = new Payload(document.RootElement);
        var values = Values(payload, texts);

        string Fill(string template) => values.Aggregate(template, (text, pair) => text.Replace('{' + pair.Key + '}', pair.Value));
        var paragraphs = mail.Intro.Select(Fill).ToList();
        if (mail.Deadline is not null && payload.Has("RespondByUtc"))
        {
            paragraphs.Add(Fill(mail.Deadline));
        }

        if (mail.ReasonLine is not null && payload.Has("Reason"))
        {
            paragraphs.Add(Fill(mail.ReasonLine));
        }

        var isCustomerMail = CustomerTypes.Contains(type);
        var isSalonMail = SalonTypes.Contains(type);
        var content = new MailContent(
            EmailTexts.ResolveLanguage(language),
            Brand(type, payload),
            Fill(mail.Subject),
            Fill(mail.Heading),
            paragraphs,
            isCustomerMail || isSalonMail ? Facts(type, payload, texts, values) : [],
            mail.Outro.Select(Fill).ToList(),
            mail.Button,
            ButtonUrl(type, payload),
            texts.Layout["buttonFallback"],
            isCustomerMail ? texts.Layout["manageLink"] : null,
            isCustomerMail ? payload.Text("ManageUrl") : null,
            Footer(type, isCustomerMail, payload, texts, values),
            type is EmailTypes.BookingConfirmed or EmailTypes.BookingRescheduled ? CalendarBlock(document.RootElement, language, texts) : null);
        return EmailLayout.Build(content);
    }

    /// <summary>"Add to Google Calendar / Outlook" links; the attached .ics file covers iPhone and everything else.</summary>
    private static CalendarBlock? CalendarBlock(JsonElement root, string language, LanguageTexts texts)
    {
        if (CalendarSource.FromPayload(root, EmailTexts.ResolveLanguage(language)) is not { } source)
        {
            return null;
        }

        var entry = CalendarEntryFactory.Create(source, CalendarMethod.Publish, organizerEmail: null, attendeeEmail: null);
        return new CalendarBlock(
            texts.Layout["calendarAdd"],
            [(texts.Layout["googleCalendar"], CalendarLinks.Google(entry)), (texts.Layout["outlookCalendar"], CalendarLinks.Outlook(entry))],
            texts.Layout["calendarAttached"]);
    }

    private static string Brand(string type, Payload payload) =>
        type is EmailTypes.ConfirmEmail or EmailTypes.ResetPassword ? AccountBrand : payload.Text("SalonName") ?? AccountBrand;

    private static string? ButtonUrl(string type, Payload payload) => type switch
    {
        EmailTypes.ConfirmEmail => payload.Text("ConfirmUrl"),
        EmailTypes.ResetPassword => payload.Text("ResetUrl"),
        EmailTypes.StaffInvitation => payload.Text("InvitationUrl"),
        EmailTypes.BookingNewForSalon => payload.Text("RequestUrl"),
        EmailTypes.BookingCancellationForSalon => payload.Text("CalendarUrl") ?? payload.Text("RequestUrl"),
        _ when BookAgainTypes.Contains(type) => payload.Text("BookingUrl") ?? payload.Text("ManageUrl"),
        _ => payload.Text("ManageUrl"),
    };

    /// <summary>Values for the placeholders in the template texts.</summary>
    private static Dictionary<string, string> Values(Payload payload, LanguageTexts texts) => new()
    {
        ["salon"] = payload.Text("SalonName") ?? AccountBrand,
        ["name"] = payload.Text("CustomerName") ?? payload.Text("DisplayName") ?? payload.Text("StaffName") ?? string.Empty,
        ["service"] = payload.Text("ServiceName") ?? string.Empty,
        ["staff"] = payload.Text("StaffName") ?? string.Empty,
        ["when"] = When(payload, "StartUtc", texts) ?? string.Empty,
        ["respondBy"] = When(payload, "RespondByUtc", texts) ?? string.Empty,
        ["expires"] = When(payload, "ExpiresAtUtc", texts) ?? string.Empty,
        ["reason"] = payload.Text("Reason") ?? string.Empty,
        ["ignore"] = texts.Layout["ignore"],
    };

    private static List<(string, string)> Facts(string type, Payload payload, LanguageTexts texts, Dictionary<string, string> values)
    {
        var facts = new List<(string, string)>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                facts.Add((texts.Labels[label], value));
            }
        }

        Add("service", payload.Text("ServiceName"));
        Add("staff", payload.Text("StaffName"));
        Add("when", values["when"]);
        if (SalonTypes.Contains(type))
        {
            Add("customer", payload.Text("CustomerName"));
            Add("email", payload.Text("CustomerEmail"));
            Add("phone", payload.Text("CustomerPhone"));
            Add("note", payload.Text("CustomerNote"));
            var status = payload.Text("Status");
            Add("status", status is not null && texts.Status.TryGetValue(status, out var label) ? label : status);
        }

        return facts;
    }

    /// <summary>Customer mails end with the salon's address and phone; every other mail with a note that it is automatic.</summary>
    private static List<string> Footer(string type, bool isCustomerMail, Payload payload, LanguageTexts texts, Dictionary<string, string> values)
    {
        if (!isCustomerMail)
        {
            return [texts.Layout["automatic"]];
        }

        var lines = new List<string> { texts.Layout["salonContact"].Replace("{salon}", values["salon"]) };
        if (payload.Text("SalonAddress") is { Length: > 0 } address)
        {
            lines.Add(address);
        }

        if (payload.Text("SalonPhone") is { Length: > 0 } phone)
        {
            lines.Add(phone);
        }

        lines.Add(texts.Layout["automatic"]);
        return lines;
    }

    /// <summary>The time in the salon's own zone and the reader's language. Words and order come from the texts, so no locale data is needed.</summary>
    private static string? When(Payload payload, string name, LanguageTexts texts)
    {
        if (payload.Instant(name) is not { } instant)
        {
            return null;
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(payload.Text("TimeZone") ?? "UTC", out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return texts.DateFormat
            .Replace("{weekday}", texts.Weekdays[(int)local.DayOfWeek])
            .Replace("{day}", local.Day.ToString(CultureInfo.InvariantCulture))
            .Replace("{month}", texts.Months[local.Month - 1])
            .Replace("{year}", local.Year.ToString(CultureInfo.InvariantCulture))
            .Replace("{hh}", local.Hour.ToString("00", CultureInfo.InvariantCulture))
            .Replace("{mm}", local.Minute.ToString("00", CultureInfo.InvariantCulture))
            .Replace("{h12}", (local.Hour % 12 == 0 ? 12 : local.Hour % 12).ToString(CultureInfo.InvariantCulture))
            .Replace("{ampm}", local.Hour < 12 ? "AM" : "PM");
    }

    private readonly struct Payload(JsonElement root)
    {
        public bool Has(string name) => root.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

        public string? Text(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()
                : null;

        public DateTimeOffset? Instant(string name) =>
            Text(name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant) ? instant : null;
    }
}
