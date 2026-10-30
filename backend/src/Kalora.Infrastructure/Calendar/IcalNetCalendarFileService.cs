using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Kalora.Application.Calendar;
using NodaTime;

namespace Kalora.Infrastructure.Calendar;

/// <summary>Writes .ics files with Ical.Net. The time zone rules (VTIMEZONE) come from NodaTime's bundled tz database, so the result does not depend on the server's OS.</summary>
internal sealed class IcalNetCalendarFileService : ICalendarFileService
{
    private const string ProductId = "-//Kalora//Appointments//EN";

    public string Create(CalendarEntry entry)
    {
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(entry.TimeZone) ?? DateTimeZone.Utc;
        var calendar = new Ical.Net.Calendar { ProductId = ProductId, Version = "2.0", Method = entry.Method == CalendarMethod.Cancel ? "CANCEL" : "PUBLISH" };

        var start = Instant.FromDateTimeOffset(entry.StartUtc).InZone(zone).LocalDateTime;
        var end = Instant.FromDateTimeOffset(entry.EndUtc).InZone(zone).LocalDateTime;
        var calendarEvent = new CalendarEvent
        {
            Uid = entry.Uid,
            Sequence = entry.Sequence,
            DtStamp = new CalDateTime(entry.Stamp.UtcDateTime, "UTC", hasTime: true),
            DtStart = new CalDateTime(start.ToDateTimeUnspecified(), zone.Id, hasTime: true),
            DtEnd = new CalDateTime(end.ToDateTimeUnspecified(), zone.Id, hasTime: true),
            Summary = entry.Summary,
            Location = entry.Location,
            Description = entry.Description,
            Status = entry.Method == CalendarMethod.Cancel ? EventStatus.Cancelled : EventStatus.Confirmed,
            Transparency = TransparencyType.Opaque,
        };
        if (entry.Url is { Length: > 0 } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            calendarEvent.Url = uri;
        }

        if (entry.OrganizerEmail is { Length: > 0 } organizer)
        {
            calendarEvent.Organizer = new Organizer($"mailto:{organizer}") { CommonName = entry.OrganizerName };
        }

        if (entry.AttendeeEmail is { Length: > 0 } attendee)
        {
            calendarEvent.Attendees.Add(new Attendee($"mailto:{attendee}") { CommonName = entry.AttendeeName });
        }

        // A withdrawn appointment needs no alert.
        if (entry.Method == CalendarMethod.Publish)
        {
            calendarEvent.Alarms.Add(new Alarm
            {
                Action = AlarmAction.Display,
                Trigger = new Trigger(-Ical.Net.DataTypes.Duration.FromTimeSpanExact(entry.ReminderBefore)),
                Description = entry.ReminderText,
            });
        }

        calendar.Events.Add(calendarEvent);
        calendar.AddTimeZone(VTimeZone.FromDateTimeZone(zone.Id, entry.StartUtc.UtcDateTime.AddDays(-1), false));
        return new CalendarSerializer().SerializeToString(calendar) ?? string.Empty;
    }
}
