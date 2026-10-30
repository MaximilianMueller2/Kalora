using System.Text;
using Kalora.Application.Email;

namespace Kalora.Application.Calendar;

/// <summary>Turns appointment facts into a calendar entry, with the words in the language of the booking.</summary>
internal static class CalendarEntryFactory
{
    /// <summary>The standard alert: two hours before.</summary>
    public static readonly TimeSpan Reminder = TimeSpan.FromHours(2);

    public static CalendarEntry Create(CalendarSource source, CalendarMethod method, string? organizerEmail, string? attendeeEmail)
    {
        var texts = EmailTexts.For(EmailTexts.ResolveLanguage(source.Language));
        var summary = $"{source.ServiceName} – {source.SalonName}";

        var description = new StringBuilder(source.ServiceName);
        if (!string.IsNullOrWhiteSpace(source.StaffName))
        {
            description.Append('\n').Append(texts.Labels["staff"]).Append(": ").Append(source.StaffName);
        }

        if (!string.IsNullOrWhiteSpace(source.ManageUrl))
        {
            description.Append("\n\n").Append(texts.Layout["manageLink"]).Append('\n').Append(source.ManageUrl);
        }

        return new CalendarEntry(
            Uid: $"{source.AppointmentId}@kalora",
            // A withdrawal must outrank the entry it withdraws, whatever the number of the last change was.
            Sequence: method == CalendarMethod.Cancel ? source.Sequence + 1 : source.Sequence,
            Method: method,
            source.StartUtc,
            source.EndUtc,
            source.TimeZone,
            source.Stamp,
            summary,
            string.IsNullOrWhiteSpace(source.SalonAddress) ? null : source.SalonAddress,
            description.ToString(),
            source.ManageUrl,
            source.SalonName,
            organizerEmail,
            method == CalendarMethod.Cancel ? attendeeEmail : null,
            method == CalendarMethod.Cancel ? source.CustomerName : null,
            texts.Layout["calendarReminder"].Replace("{service}", source.ServiceName).Replace("{salon}", source.SalonName),
            Reminder);
    }
}
