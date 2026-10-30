using System.Globalization;

namespace Kalora.Application.Calendar;

/// <summary>Prefilled "add event" links of web calendars. Nothing is sent anywhere until the person clicks.</summary>
internal static class CalendarLinks
{
    public static string Google(CalendarEntry entry) =>
        "https://calendar.google.com/calendar/render?action=TEMPLATE"
        + $"&text={Escape(entry.Summary)}"
        + $"&dates={Compact(entry.StartUtc)}/{Compact(entry.EndUtc)}"
        + $"&details={Escape(entry.Description)}"
        + $"&location={Escape(entry.Location)}";

    public static string Outlook(CalendarEntry entry) =>
        "https://outlook.live.com/calendar/0/deeplink/compose?path=%2Fcalendar%2Faction%2Fcompose&rru=addevent"
        + $"&subject={Escape(entry.Summary)}"
        + $"&startdt={Iso(entry.StartUtc)}&enddt={Iso(entry.EndUtc)}"
        + $"&body={Escape(entry.Description)}"
        + $"&location={Escape(entry.Location)}";

    private static string Compact(DateTimeOffset instant) => instant.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Iso(DateTimeOffset instant) => Uri.EscapeDataString(instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

    private static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
