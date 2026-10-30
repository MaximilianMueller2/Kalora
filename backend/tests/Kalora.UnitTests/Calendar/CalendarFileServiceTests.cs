using Kalora.Application.Calendar;
using Kalora.Infrastructure.Calendar;

namespace Kalora.UnitTests.Calendar;

public class CalendarFileServiceTests
{
    private static readonly ICalendarFileService Service = new IcalNetCalendarFileService();

    private static readonly Guid AppointmentId = Guid.Parse("0197a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    private static CalendarEntry Entry(Action<Dictionary<string, object?>>? change = null, CalendarMethod method = CalendarMethod.Publish, int sequence = 0,
        string start = "2026-11-04T08:00:00+00:00", string end = "2026-11-04T09:00:00+00:00", string timeZone = "Europe/Berlin") =>
        new(
            $"{AppointmentId}@kalora", sequence, method, DateTimeOffset.Parse(start), DateTimeOffset.Parse(end), timeZone,
            DateTimeOffset.Parse("2026-11-03T09:00:00+00:00"),
            "Färben – Haarwerk Müller", "Hauptstraße 1, 10115 Berlin", "Färben\nBei: Anna\n\nTermin ansehen oder stornieren:\nhttps://kalora.app/haarwerk/appointment/TOKEN",
            "https://kalora.app/haarwerk/appointment/TOKEN", "Haarwerk Müller", "noreply@kalora.app",
            method == CalendarMethod.Cancel ? "kim@example.com" : null, method == CalendarMethod.Cancel ? "Kim Kundin" : null,
            "Erinnerung: Färben bei Haarwerk Müller", TimeSpan.FromHours(2));

    /// <summary>Lines of the file with RFC 5545 line folding undone.</summary>
    private static List<string> Lines(string ics) =>
        ics.Replace("\r\n ", string.Empty).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();

    [Fact]
    public void The_file_is_a_calendar_with_one_event_for_the_appointment()
    {
        var lines = Lines(Service.Create(Entry()));

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.Contains("VERSION:2.0", lines);
        Assert.Contains("PRODID:-//Kalora//Appointments//EN", lines);
        Assert.Contains("METHOD:PUBLISH", lines);
        Assert.Equal(1, lines.Count(l => l == "BEGIN:VEVENT"));
        Assert.Contains($"UID:{AppointmentId}@kalora", lines);
        Assert.Contains("SEQUENCE:0", lines);
        Assert.Contains("STATUS:CONFIRMED", lines);
        Assert.Contains("DTSTAMP:20261103T090000Z", lines);
    }

    [Fact]
    public void The_time_is_written_in_the_salons_zone_and_the_zone_rules_are_included()
    {
        var lines = Lines(Service.Create(Entry()));

        Assert.Contains("DTSTART;TZID=Europe/Berlin:20261104T090000", lines);
        Assert.Contains("DTEND;TZID=Europe/Berlin:20261104T100000", lines);
        Assert.Contains("BEGIN:VTIMEZONE", lines);
        Assert.Contains("TZID:Europe/Berlin", lines);
        Assert.Contains("BEGIN:STANDARD", lines);
        Assert.Contains("BEGIN:DAYLIGHT", lines);
        Assert.Contains("TZOFFSETTO:+0100", lines);
        Assert.Contains("TZOFFSETTO:+0200", lines);
    }

    [Theory]
    [InlineData("2026-07-01T07:00:00+00:00", "2026-07-01T08:00:00+00:00", "Europe/Berlin", "20260701T090000", "20260701T100000")] // summer time
    [InlineData("2026-03-29T00:30:00+00:00", "2026-03-29T01:30:00+00:00", "Europe/Berlin", "20260329T013000", "20260329T033000")] // an hour that does not exist locally
    [InlineData("2026-11-04T08:00:00+00:00", "2026-11-04T09:00:00+00:00", "Europe/Istanbul", "20261104T110000", "20261104T120000")] // no DST
    [InlineData("2026-11-04T08:00:00+00:00", "2026-11-04T09:00:00+00:00", "America/New_York", "20261104T030000", "20261104T040000")]
    public void Local_times_follow_the_zone_including_summer_time(string start, string end, string zone, string localStart, string localEnd)
    {
        var lines = Lines(Service.Create(Entry(start: start, end: end, timeZone: zone)));

        Assert.Contains($"DTSTART;TZID={zone}:{localStart}", lines);
        Assert.Contains($"DTEND;TZID={zone}:{localEnd}", lines);
        Assert.Contains($"TZID:{zone}", lines);
    }

    [Fact]
    public void An_unknown_zone_falls_back_to_UTC_instead_of_failing()
    {
        var lines = Lines(Service.Create(Entry(timeZone: "Mars/Olympus")));

        Assert.Contains("DTSTART:20261104T080000Z", lines);
    }

    [Fact]
    public void Place_text_and_link_are_set()
    {
        var lines = Lines(Service.Create(Entry()));

        Assert.Contains("SUMMARY:Färben – Haarwerk Müller", lines);
        Assert.Contains("LOCATION:Hauptstraße 1\\, 10115 Berlin", lines);
        Assert.Contains("DESCRIPTION:Färben\\nBei: Anna\\n\\nTermin ansehen oder stornieren:\\nhttps://kalora.app/haarwerk/appointment/TOKEN", lines);
        Assert.Contains("URL:https://kalora.app/haarwerk/appointment/TOKEN", lines);
        Assert.Contains("ORGANIZER;CN=Haarwerk Müller:mailto:noreply@kalora.app", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("ATTENDEE", StringComparison.Ordinal));
    }

    [Fact]
    public void An_alert_two_hours_before_is_included()
    {
        var lines = Lines(Service.Create(Entry()));

        var alarm = lines.SkipWhile(l => l != "BEGIN:VALARM").TakeWhile(l => l != "END:VALARM").ToList();
        Assert.Contains("ACTION:DISPLAY", alarm);
        Assert.Contains("TRIGGER:-PT2H", alarm);
        Assert.Contains("DESCRIPTION:Erinnerung: Färben bei Haarwerk Müller", alarm);
    }

    [Fact]
    public void A_changed_appointment_keeps_its_UID_and_raises_the_sequence()
    {
        var original = Lines(Service.Create(Entry()));
        var moved = Lines(Service.Create(Entry(sequence: 1, start: "2026-11-05T08:00:00+00:00", end: "2026-11-05T09:00:00+00:00")));

        Assert.Equal(original.Single(l => l.StartsWith("UID:", StringComparison.Ordinal)), moved.Single(l => l.StartsWith("UID:", StringComparison.Ordinal)));
        Assert.Contains("SEQUENCE:1", moved);
        Assert.Contains("DTSTART;TZID=Europe/Berlin:20261105T090000", moved);
    }

    [Fact]
    public void A_cancellation_is_a_CANCEL_for_the_same_UID_with_the_attendee_and_no_alert()
    {
        var lines = Lines(Service.Create(Entry(method: CalendarMethod.Cancel, sequence: 2)));

        Assert.Contains("METHOD:CANCEL", lines);
        Assert.Contains("STATUS:CANCELLED", lines);
        Assert.Contains($"UID:{AppointmentId}@kalora", lines);
        Assert.Contains("SEQUENCE:2", lines);
        Assert.Contains("ATTENDEE;CN=Kim Kundin:mailto:kim@example.com", lines);
        Assert.DoesNotContain("BEGIN:VALARM", lines);
    }

    [Fact]
    public void The_same_entry_always_gives_the_same_file()
    {
        Assert.Equal(Service.Create(Entry()), Service.Create(Entry()));
    }

    [Fact]
    public void Lines_are_folded_at_75_octets_and_end_with_CRLF()
    {
        var ics = Service.Create(Entry());

        Assert.EndsWith("\r\n", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, line));
    }

    [Fact]
    public void The_file_can_be_read_back_by_a_calendar_parser()
    {
        var parsed = Ical.Net.Calendar.Load(Service.Create(Entry()))!;

        var calendarEvent = Assert.Single(parsed.Events);
        Assert.Equal($"{AppointmentId}@kalora", calendarEvent.Uid);
        Assert.Equal("Europe/Berlin", calendarEvent.DtStart!.TzId);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero), calendarEvent.DtStart.AsUtc);
        Assert.Equal(TimeSpan.FromHours(1), calendarEvent.DtEnd!.AsUtc - calendarEvent.DtStart.AsUtc);
    }
}
