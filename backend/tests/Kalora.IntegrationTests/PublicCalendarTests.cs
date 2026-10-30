using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.PublicBooking;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class PublicCalendarTests(PostgresFixture postgres)
{
    // Tuesday 3 November 2026, 09:00 UTC; the salon is in Berlin (winter time).
    private static readonly DateTimeOffset Wednesday1000 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required string Slug { get; init; }
        public required HttpClient Client { get; init; }
        public required Service Cut { get; init; }

        public async Task<BookingReceipt> BookAsync()
        {
            var response = await Client.PostAsJsonAsync($"/api/public/salons/{Slug}/appointment-requests",
                new { serviceId = Cut.Id, startUtc = Wednesday1000, name = "Kim Kundin", email = "kim@example.com", language = "de" });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<BookingReceipt>(TestJson.Options))!;
        }

        public Task<HttpResponseMessage> GetCalendarAsync(string token) => Client.GetAsync($"/api/public/appointments/{token}/calendar.ics");

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync(bool autoConfirm = true)
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (_, me) = await host.RegisterOwnerAsync("maria@example.com", "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        await salon.ConfigureSalonAsync(s =>
        {
            s.AutoConfirm = autoConfirm;
            s.Street = "Hauptstraße 1";
            s.PostalCode = "10115";
            s.City = "Berlin";
        });
        var cut = await salon.AddServiceAsync(minutes: 60);
        await salon.AddStaffAsync("Anna", 0, offers: cut);
        // No session cookies: the customer has no account.
        return new World { Host = host, Salon = salon, Slug = me.Salon.Slug, Client = host.NewSession().Client, Cut = cut };
    }

    private static List<string> Lines(string ics) => ics.Replace("\r\n ", string.Empty).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();

    [Fact]
    public async Task A_confirmed_appointment_downloads_as_a_calendar_file_without_login()
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();

        var response = await world.GetCalendarAsync(receipt.ManageToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("appointment.ics", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var lines = Lines(await response.Content.ReadAsStringAsync());
        Guid appointmentId = await world.Salon.WithDbAsync(async db => (await db.Appointments.SingleAsync()).Id);
        Assert.Contains($"UID:{appointmentId}@kalora", lines);
        Assert.Contains("METHOD:PUBLISH", lines);
        Assert.Contains("SEQUENCE:0", lines);
        Assert.Contains("STATUS:CONFIRMED", lines);
        Assert.Contains("DTSTART;TZID=Europe/Berlin:20261104T100000", lines);
        Assert.Contains("DTEND;TZID=Europe/Berlin:20261104T110000", lines);
        Assert.Contains("TZID:Europe/Berlin", lines);
        Assert.Contains("LOCATION:Hauptstraße 1\\, 10115 Berlin", lines);
        Assert.Contains("TRIGGER:-PT2H", lines);
        Assert.Contains(lines, l => l.StartsWith("SUMMARY:Haarschnitt – Haarwerk Müller", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("DESCRIPTION:", StringComparison.Ordinal) && l.Contains($"/{world.Slug}/appointment/{receipt.ManageToken}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_file_matches_the_one_attached_to_the_confirmation_mail()
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();

        var appointmentId = await world.Salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingConfirmed);
            var payload = JsonDocument.Parse(mail.PayloadJson!).RootElement;
            var id = (await db.Appointments.SingleAsync()).Id;
            Assert.Equal(id, payload.GetProperty("AppointmentId").GetGuid());
            Assert.Equal(0, payload.GetProperty("CalendarSequence").GetInt32());
            return id;
        });

        var lines = Lines(await (await world.GetCalendarAsync(receipt.ManageToken)).Content.ReadAsStringAsync());
        Assert.Contains($"UID:{appointmentId}@kalora", lines);
    }

    [Fact]
    public async Task A_request_that_is_still_open_has_no_calendar_file_yet()
    {
        await using var world = await NewWorldAsync(autoConfirm: false);
        var receipt = await world.BookAsync();

        var response = await world.GetCalendarAsync(receipt.ManageToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("appointment.calendar_unavailable", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    public async Task A_declined_or_expired_request_has_no_calendar_file(AppointmentStatus status)
    {
        await using var world = await NewWorldAsync(autoConfirm: false);
        var receipt = await world.BookAsync();
        await SetStatusAsync(world, status);

        Assert.Equal(HttpStatusCode.Conflict, (await world.GetCalendarAsync(receipt.ManageToken)).StatusCode);
    }

    [Fact]
    public async Task A_cancelled_appointment_downloads_as_a_cancellation_with_a_higher_sequence()
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();
        await SetStatusAsync(world, AppointmentStatus.Cancelled, sequence: 1);

        var lines = Lines(await (await world.GetCalendarAsync(receipt.ManageToken)).Content.ReadAsStringAsync());

        Assert.Contains("METHOD:CANCEL", lines);
        Assert.Contains("STATUS:CANCELLED", lines);
        Assert.Contains("SEQUENCE:2", lines);
        Assert.Contains("ATTENDEE;CN=Kim Kundin:mailto:kim@example.com", lines);
        Assert.DoesNotContain("BEGIN:VALARM", lines);
    }

    [Fact]
    public async Task A_moved_appointment_keeps_its_UID_and_carries_the_stored_sequence()
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();
        var original = Lines(await (await world.GetCalendarAsync(receipt.ManageToken)).Content.ReadAsStringAsync());

        await world.Salon.WithDbAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            appointment.StartUtc = appointment.StartUtc.AddDays(1);
            appointment.EndUtc = appointment.EndUtc.AddDays(1);
            appointment.BlockedStartUtc = appointment.BlockedStartUtc.AddDays(1);
            appointment.BlockedEndUtc = appointment.BlockedEndUtc.AddDays(1);
            appointment.CalendarSequence = 2;
            await db.SaveChangesAsync();
            return 0;
        });
        var moved = Lines(await (await world.GetCalendarAsync(receipt.ManageToken)).Content.ReadAsStringAsync());

        Assert.Equal(original.Single(l => l.StartsWith("UID:", StringComparison.Ordinal)), moved.Single(l => l.StartsWith("UID:", StringComparison.Ordinal)));
        Assert.Contains("SEQUENCE:2", moved);
        Assert.Contains("DTSTART;TZID=Europe/Berlin:20261105T100000", moved);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789a")]
    public async Task An_unknown_token_is_not_found(string token)
    {
        await using var world = await NewWorldAsync();
        await world.BookAsync();

        var response = await world.GetCalendarAsync(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("appointment.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_far_too_long_token_is_not_found_without_a_lookup()
    {
        await using var world = await NewWorldAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await world.GetCalendarAsync(new string('a', 500))).StatusCode);
    }

    [Theory]
    [InlineData(SalonStatus.Suspended)]
    [InlineData(SalonStatus.Cancelled)]
    public async Task A_switched_off_salon_gives_nothing_away(SalonStatus status)
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();
        await world.Salon.ConfigureSalonAsync(s => s.Status = status);

        var response = await world.GetCalendarAsync(receipt.ManageToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("appointment.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_token_is_the_only_credential_and_a_signed_in_salon_gets_no_extra_view()
    {
        await using var world = await NewWorldAsync();
        var receipt = await world.BookAsync();
        var owner = world.Host.NewSession();
        await owner.Client.PostAsJsonAsync("/api/auth/login", new { email = "maria@example.com", password = AuthTestHost.Password });

        // Neither the id of the appointment nor a guessed token opens anything.
        var appointmentId = await world.Salon.WithDbAsync(async db => (await db.Appointments.SingleAsync()).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync($"/api/public/appointments/{appointmentId}/calendar.ics")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync($"/api/public/appointments/{receipt.ManageToken}/calendar.ics")).StatusCode);
    }

    private static Task SetStatusAsync(World world, AppointmentStatus status, int sequence = 0) => world.Salon.WithDbAsync(async db =>
    {
        var appointment = await db.Appointments.SingleAsync();
        appointment.Status = status;
        appointment.CalendarSequence = sequence;
        await db.SaveChangesAsync();
        return 0;
    });
}
