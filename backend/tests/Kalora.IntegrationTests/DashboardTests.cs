using System.Net;
using System.Net.Http.Json;
using System.Text;
using Kalora.Application.Dashboard;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class DashboardTests(PostgresFixture postgres)
{
    // The test host's clock: Tuesday 3 November 2026, 09:00 UTC = 10:00 in Berlin (winter time). The salon is open 08:00-18:00,
    // Anna works 09:00-17:00 every day.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Berlin(string time, int day = 3) =>
        new DateTimeOffset(new DateOnly(2026, 11, day).ToDateTime(TimeOnly.Parse(time)), TimeSpan.FromHours(1)).ToUniversalTime();

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }
        public required string Slug { get; init; }

        public Task<Appointment> AddAsync(string time, AppointmentStatus status = AppointmentStatus.Confirmed, int day = 3, int minutes = 60, StaffMember? staff = null, Service? service = null) =>
            Salon.AddAppointmentAsync(service ?? Cut, staff ?? Anna, Berlin(time, day), status, expiresAt: Now.AddDays(1), minutes: minutes);

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync(Action<Salon>? configure = null)
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com", "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        if (configure is not null)
        {
            await salon.ConfigureSalonAsync(configure);
        }

        var cut = await salon.AddServiceAsync(minutes: 60);
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        return new World { Host = host, Owner = owner, Salon = salon, Cut = cut, Anna = anna, Slug = me.Salon.Slug };
    }

    private static async Task<DashboardDto> ReadAsync(ApiSession session)
    {
        var response = await session.GetAsync("/api/dashboard");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DashboardDto>(TestJson.Options))!;
    }

    // ------------------------------------------------------------ today

    [Fact]
    public async Task Today_has_a_timeline_per_person_with_the_appointments_of_the_day_only()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.AddAsync("11:00");
        await world.AddAsync("14:00", AppointmentStatus.Requested);
        await world.AddAsync("15:00", AppointmentStatus.Cancelled);
        await world.AddAsync("11:00", day: 4); // tomorrow
        await world.AddAsync("12:00", staff: ben);

        var dashboard = await ReadAsync(world.Owner);

        Assert.Equal((new DateOnly(2026, 11, 3), "Europe/Berlin"), (dashboard.Today.Date, dashboard.Today.TimeZone));
        Assert.Equal(["Anna", "Ben"], dashboard.Today.People.Select(p => p.DisplayName));
        var anna = dashboard.Today.People[0];
        Assert.Equal([Berlin("11:00"), Berlin("14:00")], anna.Appointments.Select(a => a.StartUtc));
        Assert.Equal(("Haarschnitt", "Kim Kundin", AppointmentStatus.Confirmed), (anna.Appointments[0].ServiceName, anna.Appointments[0].CustomerName, anna.Appointments[0].Status));
        Assert.Equal((Berlin("09:00"), Berlin("17:00")), (anna.Working.Single().StartUtc, anna.Working.Single().EndUtc));
        Assert.Single(dashboard.Today.People[1].Appointments);
    }

    [Fact]
    public async Task Free_gaps_are_what_is_left_of_the_working_time_from_now_on()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("11:00"); // 11:00-12:00
        await world.Salon.WithDbAsync(async db =>
        {
            db.TimeBlocks.Add(new TimeBlock { StaffId = world.Anna.Id, StartUtc = Berlin("14:00"), EndUtc = Berlin("14:30"), Label = "Pause" });
            await db.SaveChangesAsync();
            return 0;
        });

        var gaps = (await ReadAsync(world.Owner)).Today.People.Single().Gaps;

        // It is 10:00: the morning before work counts for nothing, and the gaps are cut by the appointment and the break.
        Assert.Equal(
            [(Berlin("10:00"), Berlin("11:00")), (Berlin("12:00"), Berlin("14:00")), (Berlin("14:30"), Berlin("17:00"))],
            gaps.Select(g => (g.StartUtc, g.EndUtc)));
    }

    [Fact]
    public async Task Gaps_count_the_buffers_of_the_services_and_skip_what_is_too_short()
    {
        await using var world = await NewWorldAsync();
        var buffered = await world.Salon.AddServiceAsync("Färben", 60, before: 15, after: 15);
        await world.AddAsync("11:00", service: buffered); // occupies 10:45-12:15
        await world.AddAsync("12:30", minutes: 60); // leaves 12:15-12:30 free: too short to show

        var gaps = (await ReadAsync(world.Owner)).Today.People.Single().Gaps;

        Assert.Equal(
            [(Berlin("10:00"), Berlin("10:45")), (Berlin("13:30"), Berlin("17:00"))],
            gaps.Select(g => (g.StartUtc, g.EndUtc)));
    }

    [Fact]
    public async Task Absences_and_open_requests_take_gap_time_too()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("12:00", AppointmentStatus.Requested); // an open request still holds its time
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffAbsences.Add(new StaffAbsence { StaffId = world.Anna.Id, StartUtc = Berlin("15:00"), EndUtc = Berlin("17:00"), Type = AbsenceType.Private });
            await db.SaveChangesAsync();
            return 0;
        });

        var gaps = (await ReadAsync(world.Owner)).Today.People.Single().Gaps;

        Assert.Equal(
            [(Berlin("10:00"), Berlin("12:00")), (Berlin("13:00"), Berlin("15:00"))],
            gaps.Select(g => (g.StartUtc, g.EndUtc)));
    }

    [Fact]
    public async Task A_closed_day_has_no_gaps()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.SalonClosures.Add(new SalonClosure { Date = new DateOnly(2026, 11, 3) });
            await db.SaveChangesAsync();
            return 0;
        });

        var person = (await ReadAsync(world.Owner)).Today.People.Single();

        Assert.Empty(person.Working);
        Assert.Empty(person.Gaps);
    }

    // ------------------------------------------------------------ requests

    [Fact]
    public async Task Open_requests_are_counted_and_the_most_urgent_five_are_listed()
    {
        await using var world = await NewWorldAsync();
        for (var hour = 9; hour <= 15; hour++)
        {
            await world.Salon.AddAppointmentAsync(world.Cut, world.Anna, Berlin($"{hour}:00", 5), AppointmentStatus.Requested, expiresAt: Now.AddHours(20 - hour));
        }

        await world.AddAsync("10:00", AppointmentStatus.Confirmed);

        var requests = (await ReadAsync(world.Owner)).Requests;

        Assert.Equal(7, requests.Count);
        Assert.Equal(5, requests.Items.Count);
        Assert.Equal(requests.Items.Select(i => i.ExpiresAtUtc).Order(), requests.Items.Select(i => i.ExpiresAtUtc)); // most urgent first
        Assert.All(requests.Items, item => Assert.Equal(AppointmentStatus.Requested, item.Status));
        Assert.Equal("Kim Kundin", requests.Items[0].Customer.Name);
    }

    // ------------------------------------------------------------ week

    [Fact]
    public async Task The_week_counts_what_counts_and_estimates_revenue_from_list_prices()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("09:00", day: 2); // Monday: confirmed 45.00
        await world.AddAsync("11:00", AppointmentStatus.Completed, day: 2, minutes: 90); // attended 45.00 (the seeder prices every appointment at 45.00)
        await world.AddAsync("13:00", AppointmentStatus.NoShow, day: 2); // missed: counts as booked, earns nothing
        await world.AddAsync("10:00", AppointmentStatus.Requested, day: 5); // still a request
        await world.AddAsync("12:00", AppointmentStatus.Cancelled, day: 5);
        await world.AddAsync("10:00", day: 9); // next week
        await world.AddAsync("10:00", day: 1); // last week (Sunday 1 November)

        var week = (await ReadAsync(world.Owner)).Week;

        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)), (week.From, week.To));
        Assert.Equal(3, week.Appointments);
        Assert.Equal(60 + 90 + 60, week.BookedMinutes);
        Assert.Equal(1, week.NoShows);
        Assert.Equal((9000, "EUR", false), (week.EstimatedRevenueCents, week.Currency, week.IncludesFromPrices));
    }

    [Fact]
    public async Task Utilization_is_booked_over_available_minutes_without_absences_and_blockers()
    {
        await using var world = await NewWorldAsync();
        // Anna works 8 h on each of the 7 days: 3360 minutes. A 2-hour blocker and a whole-day absence (8 h) take 600 of them.
        await world.Salon.WithDbAsync(async db =>
        {
            db.TimeBlocks.Add(new TimeBlock { StaffId = world.Anna.Id, StartUtc = Berlin("12:00", 3), EndUtc = Berlin("14:00", 3) });
            db.StaffAbsences.Add(new StaffAbsence { StaffId = world.Anna.Id, StartUtc = Berlin("00:00", 4), EndUtc = Berlin("23:59", 4), Type = AbsenceType.Vacation });
            await db.SaveChangesAsync();
            return 0;
        });
        await world.AddAsync("09:00", day: 5, minutes: 60);
        await world.AddAsync("10:30", day: 5, minutes: 90);

        var week = (await ReadAsync(world.Owner)).Week;

        Assert.Equal(3360 - 120 - 480, week.AvailableMinutes);
        Assert.Equal(150, week.BookedMinutes);
        Assert.Equal((int)Math.Round(100.0 * 150 / 2760), week.UtilizationPercent);
    }

    [Fact]
    public async Task Without_working_time_the_utilization_is_zero_and_never_above_a_hundred()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffWorkingHours.RemoveRange(await db.StaffWorkingHours.ToListAsync());
            await db.SaveChangesAsync();
            return 0;
        });
        await world.AddAsync("09:00");

        var week = (await ReadAsync(world.Owner)).Week;

        Assert.Equal((0, 0, 60), (week.UtilizationPercent, week.AvailableMinutes, week.BookedMinutes));
    }

    [Fact]
    public async Task Prices_marked_as_from_are_flagged_as_estimate_that_may_be_higher()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            var appointment = await world.AddAsync("10:00");
            var stored = await db.Appointments.SingleAsync(a => a.Id == appointment.Id);
            stored.PriceIsFrom = true;
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.True((await ReadAsync(world.Owner)).Week.IncludesFromPrices);
    }

    // ------------------------------------------------------------ rights and links

    [Fact]
    public async Task Staff_see_only_their_own_column_and_numbers_and_get_no_booking_link()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var benMember = await world.Salon.WithDbAsync(db => db.StaffMembers.AsNoTracking().SingleAsync(s => s.DisplayName == "Ben"));
        await world.AddAsync("11:00");
        await world.AddAsync("12:00", staff: benMember);
        await world.AddAsync("13:00", AppointmentStatus.Requested, staff: benMember);

        var asBen = await ReadAsync(ben);
        var asOwner = await ReadAsync(world.Owner);

        Assert.Equal(["Ben"], asBen.Today.People.Select(p => p.DisplayName));
        Assert.Equal(1, asBen.Week.Appointments);
        Assert.Equal(1, asBen.Requests.Count);
        Assert.Null(asBen.BookingLink);
        Assert.Equal(2, asOwner.Week.Appointments);
        Assert.EndsWith($"/{world.Slug}", asOwner.BookingLink?.Url);
    }

    [Fact]
    public async Task The_dashboard_needs_a_login()
    {
        await using var world = await NewWorldAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await world.Host.NewSession().GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task The_booking_link_comes_as_a_PNG_and_as_a_poster_PDF()
    {
        await using var world = await NewWorldAsync();

        var png = await world.Owner.GetAsync("/api/dashboard/booking-link/qr.png");
        var pdf = await world.Owner.GetAsync("/api/dashboard/booking-link/poster.pdf");

        Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], (await png.Content.ReadAsByteArrayAsync())[..4]);
        Assert.Equal($"{world.Slug}-qr.png", png.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var text = Encoding.Latin1.GetString(await pdf.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("(Haarwerk Müller) Tj", text);
        Assert.Contains($"/{world.Slug}) Tj", text);
        Assert.Contains("(Jetzt online Termin buchen) Tj", text);
    }

    [Fact]
    public async Task The_poster_speaks_the_language_of_the_salon()
    {
        await using var world = await NewWorldAsync(s => s.DefaultLanguage = "tr");

        var text = Encoding.Latin1.GetString(await (await world.Owner.GetAsync("/api/dashboard/booking-link/poster.pdf")).Content.ReadAsByteArrayAsync());

        Assert.Contains("(Online randevu alin) Tj", text);
    }

    [Fact]
    public async Task Only_owners_and_managers_may_fetch_the_hand_outs()
    {
        await using var world = await NewWorldAsync();
        var staff = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var manager = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Mona", "mona@example.com", SalonRole.Manager);

        foreach (var path in new[] { "/api/dashboard/booking-link/qr.png", "/api/dashboard/booking-link/poster.pdf" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await world.Host.NewSession().GetAsync(path)).StatusCode);
        }
    }
}
