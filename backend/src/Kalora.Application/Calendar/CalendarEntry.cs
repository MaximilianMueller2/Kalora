namespace Kalora.Application.Calendar;

public enum CalendarMethod
{
    /// <summary>The event as it stands; importing it again updates the entry with the same UID.</summary>
    Publish,

    /// <summary>The event is withdrawn; calendars remove the entry with this UID.</summary>
    Cancel,
}

/// <summary>One appointment as a calendar entry; the data of an .ics file without any file format knowledge.</summary>
/// <param name="Uid">Stable per appointment, so a changed or cancelled file replaces the entry instead of adding one.</param>
/// <param name="Sequence">Grows with every change of time; calendars ignore files with a lower number than the one they hold.</param>
/// <param name="TimeZone">IANA zone of the salon; the file carries its rules so the wall-clock time is right everywhere.</param>
/// <param name="Stamp">When this version of the entry was made; stays the same for the same data, so files are reproducible.</param>
/// <param name="OrganizerEmail">The address mails are sent from; the salon is the organizer.</param>
/// <param name="AttendeeEmail">Only set for <see cref="CalendarMethod.Cancel"/>, where the invitation names who it withdraws.</param>
public sealed record CalendarEntry(
    string Uid,
    int Sequence,
    CalendarMethod Method,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    DateTimeOffset Stamp,
    string Summary,
    string? Location,
    string? Description,
    string? Url,
    string? OrganizerName,
    string? OrganizerEmail,
    string? AttendeeEmail,
    string? AttendeeName,
    string ReminderText,
    TimeSpan ReminderBefore);

/// <summary>Writes the iCalendar (.ics) text for an entry.</summary>
public interface ICalendarFileService
{
    string Create(CalendarEntry entry);
}
