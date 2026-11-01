using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalora.Application.Appointments;
using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ManageBookingTests(PostgresFixture postgres)
{
    // The test host's clock: Tuesday 3 November 2026, 09:00 UTC. The default cancellation period of a salon is 24 hours.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Thursday10 = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero); // 10:00 in Berlin

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }
        public required HttpClient Customer { get; init; }
        public required string Slug { get; init; }

        public async Task<(Appointment Appointment, string Token)> AddAsync(AppointmentStatus status = AppointmentStatus.Confirmed, DateTimeOffset? start = null, StaffMember? staff = null)
        {
            var appointment = await Salon.AddAppointmentAsync(Cut, staff ?? Anna, start ?? Thursday10, status, expiresAt: Now.AddDays(1), language: "en");
            return (appointment, appointment.Id.ToString("N")); // the seeder derives the token hash from the id
        }

        public Task<HttpResponseMessage> GetAsync(string token) => Customer.GetAsync($"/api/public/appointments/{token}");

        public Task<HttpResponseMessage> CancelAsync(string token) => Customer.PostAsync($"/api/public/appointments/{token}/cancel", null);

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync(IReadOnlyDictionary<string, string>? settings = null)
    {
        var host = await AuthTestHost.StartAsync(postgres, settings);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com", "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        await salon.ConfigureSalonAsync(s => { s.Street = "Hauptstraße 1"; s.PostalCode = "10115"; s.City = "Berlin"; s.Phone = "+49 30 123456"; });
        var cut = await salon.AddServiceAsync(minutes: 60);
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        // The customer has no account and no cookies.
        return new World { Host = host, Owner = owner, Salon = salon, Cut = cut, Anna = anna, Customer = host.NewSession().Client, Slug = me.Salon.Slug };
    }

    private static async Task<ManagedAppointmentDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ManagedAppointmentDto>(TestJson.Options))!;
    }

    // ------------------------------------------------------------ looking

    [Fact]
    public async Task A_confirmed_appointment_shows_salon_service_time_and_what_the_customer_may_do()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();

        var response = await world.GetAsync(token);
        var dto = await ReadAsync(response);

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal((AppointmentStatus.Confirmed, "Haarschnitt", "Haarwerk Müller"), (dto.Status, dto.ServiceName, dto.SalonName));
        Assert.Equal((Thursday10, Thursday10.AddHours(1), "Europe/Berlin"), (dto.StartUtc, dto.EndUtc, dto.TimeZone));
        Assert.Equal((world.Slug, "+49 30 123456", "Hauptstraße 1, 10115 Berlin"), (dto.SalonSlug, dto.SalonPhone, dto.SalonAddress));
        Assert.Equal((Thursday10.AddHours(-24), true, true, false), (dto.CancelUntilUtc, dto.CanCancel, dto.CanAddToCalendar, dto.CanRebook));
    }

    [Fact]
    public async Task The_page_shows_nothing_about_the_person_the_notes_or_other_customers()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();
        await world.AddAsync(start: Thursday10.AddHours(3));

        var json = await (await world.GetAsync(token)).Content.ReadAsStringAsync();

        string[] expected = ["status", "startUtc", "endUtc", "serviceName", "salonName", "salonSlug", "salonPhone", "salonAddress", "timeZone", "cancelUntilUtc", "canCancel", "canAddToCalendar", "canRebook"];
        Assert.Equal(expected.Order(), JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).Order());
        foreach (var leak in new[] { "Anna", "Kim Kundin", "Föhn", "example.com", "170" })
        {
            Assert.DoesNotContain(leak, json);
        }
    }

    [Fact]
    public async Task An_open_request_can_be_withdrawn_until_it_begins_but_is_not_in_the_calendar_yet()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync(AppointmentStatus.Requested, start: Now.AddHours(5));

        var dto = await ReadAsync(await world.GetAsync(token));

        Assert.Equal((AppointmentStatus.Requested, true, false, false), (dto.Status, dto.CanCancel, dto.CanAddToCalendar, dto.CanRebook));
        Assert.Equal(Now.AddHours(5), dto.CancelUntilUtc); // inside the 24 hours, but a request can still be taken back
    }

    [Theory]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task After_a_no_the_customer_is_invited_to_ask_again(AppointmentStatus status)
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync(status);

        var dto = await ReadAsync(await world.GetAsync(token));

        Assert.Equal((status, false, false, true), (dto.Status, dto.CanCancel, dto.CanAddToCalendar, dto.CanRebook));
        Assert.Null(dto.CancelUntilUtc);
    }

    [Fact]
    public async Task An_appointment_that_took_place_offers_nothing()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync(AppointmentStatus.Completed, start: Now.AddDays(-1));

        var dto = await ReadAsync(await world.GetAsync(token));

        Assert.Equal((false, false, false), (dto.CanCancel, dto.CanAddToCalendar, dto.CanRebook));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public async Task An_unknown_token_is_not_found(string token)
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync();

        var response = await world.GetAsync(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("appointment.not_found", await response.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await world.CancelAsync(token)).StatusCode);
    }

    [Fact]
    public async Task A_far_too_long_token_is_not_found_without_a_lookup()
    {
        await using var world = await NewWorldAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await world.GetAsync(new string('a', 500))).StatusCode);
    }

    [Fact]
    public async Task The_link_stops_working_thirty_days_after_the_appointment_ended()
    {
        await using var world = await NewWorldAsync();
        var (appointment, token) = await world.AddAsync(AppointmentStatus.Completed, start: Now.AddDays(1));
        var lastDay = appointment.EndUtc.AddDays(30);

        world.Host.Clock.SetUtcNow(lastDay);
        Assert.Equal(HttpStatusCode.OK, (await world.GetAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/public/appointments/{token}/calendar.ics")).StatusCode);

        world.Host.Clock.SetUtcNow(lastDay.AddMinutes(1));
        Assert.Equal(HttpStatusCode.NotFound, (await world.GetAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.CancelAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/public/appointments/{token}/calendar.ics")).StatusCode);
    }

    [Theory]
    [InlineData(SalonStatus.Suspended)]
    [InlineData(SalonStatus.Cancelled)]
    public async Task A_switched_off_salon_gives_nothing_away(SalonStatus status)
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();
        await world.Salon.ConfigureSalonAsync(s => s.Status = status);

        Assert.Equal(HttpStatusCode.NotFound, (await world.GetAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.CancelAsync(token)).StatusCode);
    }

    // ------------------------------------------------------------ cancelling

    [Fact]
    public async Task Cancelling_in_time_cancels_frees_the_time_and_tells_the_customer_and_the_salon()
    {
        await using var world = await NewWorldAsync();
        var (appointment, token) = await world.AddAsync();

        var response = await world.CancelAsync(token);
        var dto = await ReadAsync(response);

        Assert.Equal((AppointmentStatus.Cancelled, false, true), (dto.Status, dto.CanCancel, dto.CanRebook));
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal((AppointmentStatus.Cancelled, Now), (stored.Status, stored.CancelledAtUtc));

            var toCustomer = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingCancelledByCustomer);
            Assert.Equal(appointment.CustomerId, (await db.Customers.SingleAsync(c => c.Email == toCustomer.ToEmail)).Id);
            Assert.Equal(appointment.Id, JsonDocument.Parse(toCustomer.PayloadJson!).RootElement.GetProperty("AppointmentId").GetGuid());
            Assert.Equal("en", toCustomer.Language); // the language of the booking

            var toSalon = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingCancellationForSalon);
            var payload = JsonDocument.Parse(toSalon.PayloadJson!).RootElement;
            Assert.Equal(("maria@example.com", "de", "Kim Kundin"), (toSalon.ToEmail, toSalon.Language, payload.GetProperty("CustomerName").GetString()));
            Assert.EndsWith("/app/calendar", payload.GetProperty("CalendarUrl").GetString());

            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "appointment.cancelled_by_customer");
            Assert.Equal((null, appointment.Id), (audit.ActorUserId, audit.EntityId));
            Assert.Equal("""{"from":"Confirmed","to":"Cancelled"}""", audit.Details);

            // The time is free again at once: the same person can be booked for it.
            var again = await world.Salon.AddAppointmentAsync(world.Cut, world.Anna, Thursday10, AppointmentStatus.Confirmed);
            Assert.NotEqual(appointment.Id, again.Id);
            return 0;
        });
    }

    [Fact]
    public async Task Cancelling_needs_no_login_and_no_antiforgery_token()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();

        var response = await world.Customer.PostAsJsonAsync($"/api/public/appointments/{token}/cancel", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task After_the_cancellation_period_the_customer_is_referred_to_the_salon()
    {
        await using var world = await NewWorldAsync();
        var (appointment, token) = await world.AddAsync(start: Now.AddHours(10));

        var look = await ReadAsync(await world.GetAsync(token));
        var cancel = await world.CancelAsync(token);

        Assert.Equal((false, "+49 30 123456"), (look.CanCancel, look.SalonPhone));
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("appointment.cancellation_deadline_passed", await cancel.ProblemCodeAsync());
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(AppointmentStatus.Confirmed, (await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id)).Status);
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task The_period_is_the_salons_own_setting_and_exactly_at_the_limit_is_still_fine()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.CancellationDeadlineHours = 2);
        var (_, early) = await world.AddAsync(start: Now.AddHours(2));
        var (_, late) = await world.AddAsync(start: Now.AddHours(2).AddMinutes(-1).AddHours(3));

        Assert.Equal(HttpStatusCode.OK, (await world.CancelAsync(early)).StatusCode); // two hours to go, exactly
        world.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await world.CancelAsync(late)).StatusCode); // 4 h 59 min before, deadline 2 h
    }

    [Fact]
    public async Task A_period_of_zero_allows_cancelling_until_the_start()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.CancellationDeadlineHours = 0);
        var (_, token) = await world.AddAsync(start: Now.AddMinutes(10));

        Assert.Equal(HttpStatusCode.OK, (await world.CancelAsync(token)).StatusCode);
    }

    [Fact]
    public async Task An_open_request_can_be_withdrawn_even_close_to_the_start()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync(AppointmentStatus.Requested, start: Now.AddHours(3));

        var dto = await ReadAsync(await world.CancelAsync(token));

        Assert.Equal(AppointmentStatus.Cancelled, dto.Status);
    }

    [Fact]
    public async Task Cancelling_twice_or_cancelling_something_already_answered_is_refused_without_more_mails()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();
        var (_, declined) = await world.AddAsync(AppointmentStatus.Declined, start: Thursday10.AddHours(3));
        await world.CancelAsync(token);

        var again = await world.CancelAsync(token);
        var refused = await world.CancelAsync(declined);

        Assert.Equal("appointment.invalid_transition", await again.ProblemCodeAsync());
        Assert.Equal("appointment.invalid_transition", await refused.ProblemCodeAsync());
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingCancelledByCustomer).ToListAsync());
            Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingCancellationForSalon).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task The_salon_is_told_by_owners_managers_and_the_person_the_appointment_was_with_only()
    {
        await using var world = await NewWorldAsync();
        await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Mona", "mona@example.com", SalonRole.Manager);
        await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Olga", "olga@example.com", SalonRole.Staff);
        var ben = await world.Salon.WithDbAsync(db => db.StaffMembers.AsNoTracking().SingleAsync(s => s.DisplayName == "Ben"));
        var (_, token) = await world.AddAsync(staff: ben);

        await world.CancelAsync(token);

        await world.Salon.WithDbAsync(async db =>
        {
            var recipients = await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingCancellationForSalon).Select(m => m.ToEmail).ToListAsync();
            Assert.Equal(["ben@example.com", "maria@example.com", "mona@example.com"], recipients.Order());
            return 0;
        });
    }

    [Fact]
    public async Task The_cancellation_shows_up_in_the_salons_calendar_as_free_time_and_the_file_becomes_a_withdrawal()
    {
        await using var world = await NewWorldAsync();
        var (_, token) = await world.AddAsync();
        await world.CancelAsync(token);

        var column = (await world.Owner.GetJsonAsync<Kalora.Application.Calendar.CalendarDto>("/api/calendar?from=2026-11-05&to=2026-11-05")).Columns.Single();
        var ics = await (await world.Customer.GetAsync($"/api/public/appointments/{token}/calendar.ics")).Content.ReadAsStringAsync();

        Assert.Empty(column.Appointments);
        Assert.Contains("METHOD:CANCEL", ics);
    }

    // ------------------------------------------------------------ protection

    [Fact]
    public async Task Trying_many_tokens_is_slowed_down_per_address()
    {
        await using var world = await NewWorldAsync(new Dictionary<string, string> { ["RateLimiting:Manage:PermitLimit"] = "3" });

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            codes.Add((await world.GetAsync($"guess-{i}")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], codes);
    }

    [Fact]
    public async Task The_link_in_the_confirmation_mail_opens_the_page()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.AutoConfirm = true);
        var response = await world.Customer.PostAsJsonAsync($"/api/public/salons/{world.Slug}/appointment-requests",
            new { serviceId = world.Cut.Id, startUtc = Thursday10, name = "Kim Kundin", email = "kim@example.com", language = "de" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var url = await world.Salon.WithDbAsync(async db =>
            JsonDocument.Parse((await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingConfirmed)).PayloadJson!).RootElement.GetProperty("ManageUrl").GetString()!);
        var token = Uri.UnescapeDataString(new Uri(url).Segments[^1]);

        Assert.Equal("m/", new Uri(url).Segments[^2]);
        Assert.Equal(AppointmentStatus.Confirmed, (await ReadAsync(await world.GetAsync(token))).Status);
    }
}
