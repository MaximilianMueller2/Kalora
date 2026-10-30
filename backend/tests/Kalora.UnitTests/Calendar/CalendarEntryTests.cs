using Kalora.Application.Calendar;

namespace Kalora.UnitTests.Calendar;

public class CalendarEntryTests
{
    private static readonly Guid Id = Guid.Parse("0197a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    private static CalendarSource Source(string language = "de", int sequence = 0, string? staff = "Anna", string? address = "Hauptstraße 1, 10115 Berlin", string? manageUrl = "https://kalora.app/h/appointment/T") =>
        new(Id, sequence, DateTimeOffset.Parse("2026-11-04T08:00:00+00:00"), DateTimeOffset.Parse("2026-11-04T09:00:00+00:00"), "Europe/Berlin",
            DateTimeOffset.Parse("2026-11-03T09:00:00+00:00"), "Haarwerk", address, "Färben", staff, manageUrl, language, "Kim");

    [Fact]
    public void The_UID_is_stable_and_belongs_to_the_appointment()
    {
        var publish = CalendarEntryFactory.Create(Source(), CalendarMethod.Publish, "noreply@kalora.app", "kim@example.com");
        var cancel = CalendarEntryFactory.Create(Source(), CalendarMethod.Cancel, "noreply@kalora.app", "kim@example.com");

        Assert.Equal($"{Id}@kalora", publish.Uid);
        Assert.Equal(publish.Uid, cancel.Uid);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(4, 4, 5)]
    public void A_cancellation_always_outranks_the_entry_it_withdraws(int sequence, int published, int cancelled)
    {
        Assert.Equal(published, CalendarEntryFactory.Create(Source(sequence: sequence), CalendarMethod.Publish, null, null).Sequence);
        Assert.Equal(cancelled, CalendarEntryFactory.Create(Source(sequence: sequence), CalendarMethod.Cancel, null, null).Sequence);
    }

    [Fact]
    public void Only_a_cancellation_names_the_attendee()
    {
        var publish = CalendarEntryFactory.Create(Source(), CalendarMethod.Publish, "noreply@kalora.app", "kim@example.com");
        var cancel = CalendarEntryFactory.Create(Source(), CalendarMethod.Cancel, "noreply@kalora.app", "kim@example.com");

        Assert.Null(publish.AttendeeEmail);
        Assert.Equal(("kim@example.com", "Kim"), (cancel.AttendeeEmail, cancel.AttendeeName));
        Assert.Equal("noreply@kalora.app", publish.OrganizerEmail);
    }

    [Theory]
    [InlineData("de", "Bei: Anna", "Termin ansehen oder stornieren:", "Erinnerung: Färben bei Haarwerk")]
    [InlineData("en", "With: Anna", "View or cancel your appointment:", "Reminder: Färben at Haarwerk")]
    [InlineData("tr", "Kimde: Anna", "Randevunuzu görüntüleyin veya iptal edin:", "Hatırlatma: Färben – Haarwerk")]
    public void The_words_follow_the_language_of_the_booking(string language, string staff, string manage, string reminder)
    {
        var entry = CalendarEntryFactory.Create(Source(language), CalendarMethod.Publish, null, null);

        Assert.Equal("Färben – Haarwerk", entry.Summary);
        Assert.Contains(staff, entry.Description);
        Assert.Contains(manage, entry.Description);
        Assert.Contains("https://kalora.app/h/appointment/T", entry.Description);
        Assert.Equal(reminder, entry.ReminderText);
        Assert.Equal(TimeSpan.FromHours(2), entry.ReminderBefore);
    }

    [Fact]
    public void Missing_parts_are_left_out_instead_of_leaving_holes()
    {
        var entry = CalendarEntryFactory.Create(Source(staff: null, address: " ", manageUrl: null), CalendarMethod.Publish, null, null);

        Assert.Null(entry.Location);
        Assert.Equal("Färben", entry.Description);
    }

    [Fact]
    public void An_unknown_language_falls_back_to_German()
    {
        Assert.Contains("Bei: Anna", CalendarEntryFactory.Create(Source("fr"), CalendarMethod.Publish, null, null).Description);
    }
}
