using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalora.Application.Admin;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Staff;
using Kalora.Application.Subscriptions;
using Kalora.Domain.Appointments;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SubscriptionTests(PostgresFixture postgres)
{
    // The test host's clock: Tuesday 3 November 2026, 09:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private const string AdminEmail = "admin@kalora.test";

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Guid SalonId { get; init; }
        public required ApiSession Admin { get; init; }

        public Task<HttpResponseMessage> CreateStaffAsync(string name = "Anna") =>
            Owner.PostAsync("/api/staff", new Kalora.Application.Staff.CreateStaffRequest(name, "#7c5cfc", true, false, false));

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    /// <summary>A new salon starts in the trial, where the biggest plan's limits apply; most tests want the plan to count and start with an active salon.</summary>
    private async Task<World> NewWorldAsync(bool trial = false, string salonName = "Haarwerk Müller")
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com", salonName);

        // The platform admin is an ordinary account with a flag; the flag is read when the cookie is created.
        (await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration(AdminEmail, "Kalora Support"))).EnsureSuccessStatusCode();
        await using (var db = TestDb.CreateContext(host.ConnectionString))
        {
            await db.Users.Where(u => u.Email == AdminEmail).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsPlatformAdmin, true));
        }

        var admin = host.NewSession();
        (await admin.PostAsync("/api/auth/login", new LoginRequest(AdminEmail, AuthTestHost.Password))).EnsureSuccessStatusCode();
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        if (!trial)
        {
            await salon.ConfigureSalonAsync(s => s.Status = SalonStatus.Active);
        }

        return new World { Host = host, Owner = owner, Salon = salon, SalonId = me.Salon.Id, Admin = admin };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response) => await response.ProblemCodeAsync() ?? "(none)";

    // ------------------------------------------------------------ plan limits

    [Fact]
    public async Task The_solo_plan_has_room_for_one_active_staff_member_and_a_bigger_plan_for_more()
    {
        await using var world = await NewWorldAsync();

        Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync("Anna")).StatusCode);
        var second = await world.CreateStaffAsync("Ben");
        Assert.Equal((HttpStatusCode.Conflict, "subscription.staff_limit"), (second.StatusCode, await CodeAsync(second)));

        (await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/plan", new SetPlanRequest(PlanCode.Team))).EnsureSuccessStatusCode();
        for (var i = 2; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync($"Person {i}")).StatusCode);
        }

        Assert.Equal("subscription.staff_limit", await CodeAsync(await world.CreateStaffAsync("Zu viel")));
    }

    [Fact]
    public async Task An_inactive_person_does_not_count_but_cannot_come_back_beyond_the_limit()
    {
        await using var world = await NewWorldAsync();
        var anna = await ReadAsync<StaffDto>(await world.CreateStaffAsync("Anna"));
        (await world.Owner.PutAsync($"/api/staff/{anna.Id}", new SaveStaffRequest("Anna", "#7c5cfc", true, false, IsActive: false))).EnsureSuccessStatusCode();

        var ben = await ReadAsync<StaffDto>(await world.CreateStaffAsync("Ben"));
        var back = await world.Owner.PutAsync($"/api/staff/{anna.Id}", new SaveStaffRequest("Anna", "#7c5cfc", true, false, IsActive: true));

        Assert.Equal((HttpStatusCode.Conflict, "subscription.staff_limit"), (back.StatusCode, await CodeAsync(back)));
        // Changing the name of someone who is already active is no "adding".
        Assert.Equal(HttpStatusCode.OK, (await world.Owner.PutAsync($"/api/staff/{ben.Id}", new SaveStaffRequest("Benjamin", "#7c5cfc", true, false, IsActive: true))).StatusCode);
    }

    [Fact]
    public async Task Moving_to_a_smaller_plan_keeps_the_staff_but_blocks_adding()
    {
        await using var world = await NewWorldAsync();
        (await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/plan", new SetPlanRequest(PlanCode.Team))).EnsureSuccessStatusCode();
        await world.CreateStaffAsync("Anna");
        await world.CreateStaffAsync("Ben");

        var detail = await ReadAsync<AdminSalonDetail>(await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/plan", new SetPlanRequest(PlanCode.Solo)));

        Assert.Equal((2, 1), (detail.StaffCount, detail.StaffLimit));
        Assert.Equal(2, (await world.Owner.GetJsonAsync<List<StaffDto>>("/api/staff")).Count);
        Assert.Equal("subscription.staff_limit", await CodeAsync(await world.CreateStaffAsync("Cleo")));
    }

    [Fact]
    public async Task The_limits_come_from_the_configuration()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, new Dictionary<string, string> { ["Plans:SoloMaxStaff"] = "2" });
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        await new SalonSeeder(host.ConnectionString, me.Salon!.Id).ConfigureSalonAsync(s => s.Status = SalonStatus.Active);

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/staff", new Kalora.Application.Staff.CreateStaffRequest("Anna", "#7c5cfc", true, false, false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/staff", new Kalora.Application.Staff.CreateStaffRequest("Ben", "#7c5cfc", true, false, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/staff", new Kalora.Application.Staff.CreateStaffRequest("Cleo", "#7c5cfc", true, false, false))).StatusCode);
    }

    // ------------------------------------------------------------ read-only

    [Theory]
    [InlineData(SalonStatus.Suspended)]
    [InlineData(SalonStatus.Cancelled)]
    public async Task A_switched_off_salon_can_look_but_not_change_anything(SalonStatus status)
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.Status = status);

        Assert.Equal(HttpStatusCode.OK, (await world.Owner.GetAsync("/api/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Owner.GetAsync("/api/settings/profile")).StatusCode);
        var write = await world.CreateStaffAsync();
        Assert.Equal((HttpStatusCode.Forbidden, "subscription.read_only"), (write.StatusCode, await CodeAsync(write)));
        var settings = await world.Owner.PutAsync("/api/settings/booking-rules", new { });
        Assert.Equal("subscription.read_only", await CodeAsync(settings));
    }

    [Fact]
    public async Task A_trial_that_is_over_makes_the_salon_read_only_and_extending_it_opens_it_again()
    {
        await using var world = await NewWorldAsync(trial: true);
        await world.Salon.ConfigureSalonAsync(s => s.TrialEndsAtUtc = Now.AddDays(1));
        Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync("Anna")).StatusCode);

        world.Host.Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal("subscription.read_only", await CodeAsync(await world.CreateStaffAsync("Ben")));
        Assert.Equal(SalonAccess.TrialExpired, (await world.Owner.MeAsync()).Salon!.Access);

        (await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(14))).EnsureSuccessStatusCode();
        (await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/plan", new SetPlanRequest(PlanCode.Team))).EnsureSuccessStatusCode();

        Assert.Equal(SalonAccess.Full, (await world.Owner.MeAsync()).Salon!.Access);
        Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync("Ben")).StatusCode);
    }

    [Fact]
    public async Task Past_due_and_active_salons_work_fully()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.Status = SalonStatus.PastDue);

        Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync()).StatusCode);
        Assert.Equal(SalonAccess.Full, (await world.Owner.MeAsync()).Salon!.Access);
    }

    [Fact]
    public async Task A_read_only_salon_can_still_sign_out_and_honour_the_data_protection_rights_of_its_customers()
    {
        await using var world = await NewWorldAsync();
        var customerId = Guid.NewGuid();
        await world.Salon.WithDbAsync(async db =>
        {
            db.Customers.Add(new Kalora.Domain.Customers.Customer { Name = "Kim Kundin", Email = "kim@example.com" });
            await db.SaveChangesAsync();
            customerId = (await db.Customers.AsNoTracking().SingleAsync()).Id;
            return 0;
        });
        await world.Salon.ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        Assert.Equal(HttpStatusCode.OK, (await world.Owner.GetAsync($"/api/customers/{customerId}/export.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Owner.PostAsync($"/api/customers/{customerId}/anonymize")).StatusCode);
        Assert.True((await world.Owner.PostAsync("/api/auth/logout")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Another_salons_state_does_not_limit_this_one()
    {
        await using var world = await NewWorldAsync();
        var (other, otherMe) = await world.Host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        await new SalonSeeder(world.Host.ConnectionString, otherMe.Salon!.Id).ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        Assert.Equal("subscription.read_only", await CodeAsync(await other.PostAsync("/api/staff", new Kalora.Application.Staff.CreateStaffRequest("Anna", "#7c5cfc", true, false, false))));
        Assert.Equal(HttpStatusCode.OK, (await world.CreateStaffAsync()).StatusCode);
    }

    // ------------------------------------------------------------ the admin area

    [Fact]
    public async Task Only_the_platform_admin_may_use_the_admin_endpoints()
    {
        await using var world = await NewWorldAsync();
        var anonymous = world.Host.NewSession();

        foreach (var session in new[] { world.Owner, anonymous })
        {
            var expected = ReferenceEquals(session, anonymous) ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(expected, (await session.GetAsync("/api/admin/salons")).StatusCode);
            Assert.Equal(expected, (await session.GetAsync($"/api/admin/salons/{world.SalonId}")).StatusCode);
            Assert.Equal(expected, (await session.PostAsync($"/api/admin/salons/{world.SalonId}/suspend")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync("/api/admin/salons")).StatusCode);
        Assert.True((await world.Admin.MeAsync()).IsPlatformAdmin);
    }

    [Fact]
    public async Task The_list_shows_status_plan_staff_appointments_of_the_month_trial_end_and_owner()
    {
        await using var world = await NewWorldAsync(trial: true);
        var cut = await world.Salon.AddServiceAsync();
        var anna = await world.Salon.AddStaffAsync("Anna", 0, offers: cut);
        await world.Salon.AddStaffAsync("Inaktiv", 1, active: false, offers: cut);
        await world.Salon.AddAppointmentAsync(cut, anna, Now.AddDays(2), AppointmentStatus.Confirmed);
        await world.Salon.AddAppointmentAsync(cut, anna, Now.AddDays(2).AddHours(2), AppointmentStatus.Requested);
        await world.Salon.AddAppointmentAsync(cut, anna, Now.AddDays(3), AppointmentStatus.Cancelled);
        await world.Salon.AddAppointmentAsync(cut, anna, Now.AddDays(40), AppointmentStatus.Confirmed);

        var list = await ReadAsync<AdminSalonList>(await world.Admin.GetAsync("/api/admin/salons?q=haarwerk"));

        var item = Assert.Single(list.Items);
        Assert.Equal(("Haarwerk Müller", SalonStatus.Trial, PlanCode.Solo, SalonAccess.Full), (item.Name, item.Status, item.PlanCode, item.Access));
        // In the trial the limit of the biggest plan applies.
        Assert.Equal((1, 15, 2), (item.StaffCount, item.StaffLimit, item.AppointmentsThisMonth));
        Assert.Equal("maria@example.com", item.OwnerEmail);
        Assert.Equal(Now.AddDays(30), item.TrialEndsAtUtc);
        Assert.False(item.HasNote);
    }

    [Fact]
    public async Task The_list_can_be_searched_by_name_address_of_the_owner_and_filtered_by_status()
    {
        await using var world = await NewWorldAsync(trial: true);
        var (_, other) = await world.Host.RegisterOwnerAsync("chef@beispiel.de", "Barber Bob");
        await new SalonSeeder(world.Host.ConnectionString, other.Salon!.Id).ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        async Task<string[]> Names(string query) =>
            (await ReadAsync<AdminSalonList>(await world.Admin.GetAsync($"/api/admin/salons{query}"))).Items.Select(i => i.Name).Order().ToArray();

        Assert.Equal(["Barber Bob", "Haarwerk Müller", "Kalora Support"], await Names(string.Empty));
        Assert.Equal(["Barber Bob"], await Names("?q=BOB"));
        Assert.Equal(["Barber Bob"], await Names("?q=beispiel.de"));
        Assert.Equal(["Barber Bob"], await Names("?status=Suspended"));
        Assert.Equal(["Haarwerk Müller"], await Names("?status=Trial&q=haarwerk"));
        Assert.Empty(await Names("?q=gibtesnicht"));
    }

    [Fact]
    public async Task The_detail_has_master_data_and_the_note_but_no_customers()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s =>
        {
            s.City = "Berlin";
            s.Phone = "+49 30 123456";
        });

        var detail = await ReadAsync<AdminSalonDetail>(await world.Admin.GetAsync($"/api/admin/salons/{world.SalonId}"));
        var json = await (await world.Admin.GetAsync($"/api/admin/salons/{world.SalonId}")).Content.ReadAsStringAsync();

        Assert.Equal(("Berlin", "+49 30 123456", "Maria Müller", "maria@example.com"), (detail.City, detail.Phone, detail.OwnerName, detail.OwnerEmail));
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.GetAsync($"/api/admin/salons/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Extending_the_trial_adds_to_the_later_of_now_and_the_end_and_makes_reminders_possible_again()
    {
        await using var world = await NewWorldAsync(trial: true);
        await world.Salon.ConfigureSalonAsync(s =>
        {
            s.TrialEndsAtUtc = Now.AddDays(3);
            s.TrialWeekReminderSentAtUtc = Now;
            s.TrialDayReminderSentAtUtc = Now;
        });

        var running = await ReadAsync<AdminSalonDetail>(await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(10)));
        Assert.Equal(Now.AddDays(13), running.TrialEndsAtUtc);
        var stored = await world.Salon.WithDbAsync(db => db.AllSalons().AsNoTracking().SingleAsync(s => s.Id == world.SalonId));
        Assert.Null(stored.TrialWeekReminderSentAtUtc);
        Assert.Null(stored.TrialDayReminderSentAtUtc);

        await world.Salon.ConfigureSalonAsync(s => s.TrialEndsAtUtc = Now.AddDays(-5));
        var over = await ReadAsync<AdminSalonDetail>(await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(7)));
        Assert.Equal((Now.AddDays(7), SalonAccess.Full), (over.TrialEndsAtUtc, over.Access));
    }

    [Theory]
    [InlineData(0, "admin.trial_days_invalid")]
    [InlineData(91, "admin.trial_days_invalid")]
    public async Task The_trial_can_be_extended_by_one_to_ninety_days(int days, string code)
    {
        await using var world = await NewWorldAsync(trial: true);

        var response = await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(days));

        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, await CodeAsync(response)));
    }

    [Fact]
    public async Task Only_a_salon_in_the_trial_has_a_trial_to_extend()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.Status = SalonStatus.Active);

        var response = await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(7));

        Assert.Equal((HttpStatusCode.Conflict, "admin.not_in_trial"), (response.StatusCode, await CodeAsync(response)));
    }

    [Fact]
    public async Task Suspending_stops_bookings_and_changes_and_unsuspending_goes_back_to_the_trial_or_to_active()
    {
        await using var world = await NewWorldAsync(trial: true);

        var suspended = await ReadAsync<AdminSalonDetail>(await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/suspend"));
        Assert.Equal((SalonStatus.Suspended, SalonAccess.Suspended), (suspended.Status, suspended.Access));
        Assert.Equal("subscription.read_only", await CodeAsync(await world.CreateStaffAsync()));

        var back = await ReadAsync<AdminSalonDetail>(await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/unsuspend"));
        Assert.Equal((SalonStatus.Trial, SalonAccess.Full), (back.Status, back.Access));

        await world.Salon.ConfigureSalonAsync(s =>
        {
            s.Status = SalonStatus.Suspended;
            s.TrialEndsAtUtc = Now.AddDays(-1);
        });
        var active = await ReadAsync<AdminSalonDetail>(await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/unsuspend"));
        Assert.Equal(SalonStatus.Active, active.Status);

        var again = await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/unsuspend");
        Assert.Equal((HttpStatusCode.Conflict, "admin.not_suspended"), (again.StatusCode, await CodeAsync(again)));
    }

    [Fact]
    public async Task The_status_can_be_set_directly_and_the_note_is_kept_for_the_admin_only()
    {
        await using var world = await NewWorldAsync();

        var past = await ReadAsync<AdminSalonDetail>(await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/status", new SetStatusRequest(SalonStatus.PastDue)));
        var noted = await ReadAsync<AdminSalonDetail>(await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/note", new SetNoteRequest("  Zahlt per Überweisung, Rechnung 2026-11  ")));

        Assert.Equal(SalonStatus.PastDue, past.Status);
        Assert.Equal("Zahlt per Überweisung, Rechnung 2026-11", noted.AdminNote);
        Assert.True((await ReadAsync<AdminSalonList>(await world.Admin.GetAsync("/api/admin/salons?q=haarwerk"))).Items.Single().HasNote);
        var ownersView = await (await world.Owner.GetAsync("/api/settings/profile")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Überweisung", ownersView);
        Assert.DoesNotContain("Überweisung", JsonSerializer.Serialize(await world.Owner.MeAsync()));

        var cleared = await ReadAsync<AdminSalonDetail>(await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/note", new SetNoteRequest("   ")));
        Assert.Null(cleared.AdminNote);
        var tooLong = await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/note", new SetNoteRequest(new string('x', 2001)));
        Assert.Equal((HttpStatusCode.BadRequest, "admin.note_too_long"), (tooLong.StatusCode, await CodeAsync(tooLong)));
    }

    [Fact]
    public async Task Every_change_is_logged_with_the_admin_as_actor_and_without_personal_data()
    {
        await using var world = await NewWorldAsync(trial: true);
        var adminId = (await world.Admin.MeAsync()).UserId;

        await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/plan", new SetPlanRequest(PlanCode.Team));
        await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(7));
        await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/suspend");
        await world.Admin.PutAsync($"/api/admin/salons/{world.SalonId}/note", new SetNoteRequest("Geheime Notiz mit Name Max Mustermann"));

        var logs = await world.Salon.WithDbAsync(db => db.AuditLogs.AsNoTracking().Where(l => l.Action.StartsWith("admin.")).OrderBy(l => l.CreatedAtUtc).ToListAsync());
        Assert.Equal(["admin.plan_changed", "admin.trial_extended", "admin.status_changed", "admin.note_changed"], logs.Select(l => l.Action).ToArray());
        Assert.All(logs, log => Assert.Equal(adminId, log.ActorUserId));
        Assert.DoesNotContain(logs, log => log.Details!.Contains("Mustermann", StringComparison.Ordinal));
        Assert.Contains("\"from\":\"Solo\"", logs[0].Details);
        Assert.Contains("\"to\":\"Team\"", logs[0].Details);
    }

    [Fact]
    public async Task A_salon_that_does_not_exist_is_not_found_for_every_change()
    {
        await using var world = await NewWorldAsync();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PutAsync($"/api/admin/salons/{id}/plan", new SetPlanRequest(PlanCode.Team))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PostAsync($"/api/admin/salons/{id}/suspend")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PutAsync($"/api/admin/salons/{id}/note", new SetNoteRequest("x"))).StatusCode);
    }

    [Fact]
    public async Task The_admin_can_work_even_if_their_own_salon_is_switched_off()
    {
        await using var world = await NewWorldAsync();
        var adminSalon = (await world.Admin.MeAsync()).Salon!;
        await new SalonSeeder(world.Host.ConnectionString, adminSalon.Id).ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        Assert.Equal(HttpStatusCode.OK, (await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/suspend")).StatusCode);
    }

    // ------------------------------------------------------------ who becomes platform admin

    [Fact]
    public async Task Only_a_confirmed_account_named_in_the_configuration_becomes_platform_admin()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, new Dictionary<string, string>
        {
            ["PlatformAdmin:Emails:0"] = "Chef@Kalora.test",
            ["PlatformAdmin:Emails:1"] = "gibt-es-nicht@kalora.test",
        });
        (await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("chef@kalora.test", "Kalora"))).EnsureSuccessStatusCode();
        (await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("other@kalora.test", "Anderer"))).EnsureSuccessStatusCode();
        var bootstrap = host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Single(s => s.GetType().Name == "PlatformAdminBootstrap");

        async Task<bool?> IsAdminAsync(string email)
        {
            await using var db = TestDb.CreateContext(host.ConnectionString);
            return await db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => (bool?)u.IsPlatformAdmin).SingleOrDefaultAsync();
        }

        // Registering the address is not enough: it has to be confirmed.
        await bootstrap.StartAsync(CancellationToken.None);
        Assert.False(await IsAdminAsync("chef@kalora.test"));

        await using (var db = TestDb.CreateContext(host.ConnectionString))
        {
            await db.Users.ExecuteUpdateAsync(set => set.SetProperty(u => u.EmailConfirmed, true));
        }

        await bootstrap.StartAsync(CancellationToken.None);
        await bootstrap.StartAsync(CancellationToken.None); // again: nothing changes, nothing breaks

        Assert.True(await IsAdminAsync("chef@kalora.test"));
        Assert.False(await IsAdminAsync("other@kalora.test"));
        var session = host.NewSession();
        (await session.PostAsync("/api/auth/login", new LoginRequest("chef@kalora.test", AuthTestHost.Password))).EnsureSuccessStatusCode();
        Assert.True((await session.MeAsync()).IsPlatformAdmin);
    }

    // ------------------------------------------------------------ the profile

    [Fact]
    public async Task The_profile_tells_the_frontend_what_the_salon_may_do()
    {
        await using var world = await NewWorldAsync();
        Assert.Equal(SalonAccess.Full, (await world.Owner.MeAsync()).Salon!.Access);

        await world.Salon.ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        Assert.Equal(SalonAccess.Suspended, (await world.Owner.MeAsync()).Salon!.Access);
    }

    // ------------------------------------------------------------ trial reminders

    private sealed class Reminders(AuthTestHost host)
    {
        public async Task<TrialReminderResult> RunAsync()
        {
            await using var scope = host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ITrialReminderService>().RunAsync();
        }

        public async Task<List<EmailOutbox>> MailsAsync(string type)
        {
            await using var db = TestDb.CreateContext(host.ConnectionString);
            return await db.AllTenants<EmailOutbox>().AsNoTracking().Where(m => m.Type == type).ToListAsync();
        }
    }

    [Fact]
    public async Task The_owner_gets_one_mail_a_week_before_and_one_a_day_before_the_trial_ends()
    {
        await using var world = await NewWorldAsync(trial: true);
        var reminders = new Reminders(world.Host);

        // The test salon's trial ends in 30 days: nothing to say yet.
        Assert.Equal(0, (await reminders.RunAsync()).Total);

        world.Host.Clock.Advance(TimeSpan.FromDays(23).Add(TimeSpan.FromHours(1))); // 6 days 23 hours left
        Assert.Equal(new TrialReminderResult(Week: 2, Day: 0), await reminders.RunAsync()); // owner "Maria" and the admin's own salon
        Assert.Equal(0, (await reminders.RunAsync()).Total); // not twice

        var week = (await reminders.MailsAsync(EmailTypes.TrialEndsSoon)).Single(m => m.ToEmail == "maria@example.com");
        Assert.Equal("de", week.Language);
        var payload = JsonDocument.Parse(week.PayloadJson!).RootElement;
        Assert.Equal("Haarwerk Müller", payload.GetProperty("SalonName").GetString());
        Assert.EndsWith("/app/dashboard", payload.GetProperty("AppUrl").GetString());

        world.Host.Clock.Advance(TimeSpan.FromDays(6)); // 23 hours left
        Assert.Equal(new TrialReminderResult(Week: 0, Day: 2), await reminders.RunAsync());
        Assert.Equal(0, (await reminders.RunAsync()).Total);
        Assert.Single(await reminders.MailsAsync(EmailTypes.TrialEndsTomorrow), m => m.ToEmail == "maria@example.com");
    }

    [Fact]
    public async Task A_trial_that_is_already_inside_the_last_day_gets_only_the_tomorrow_mail()
    {
        await using var world = await NewWorldAsync(trial: true);
        await world.Salon.ConfigureSalonAsync(s => s.TrialEndsAtUtc = Now.AddHours(5));
        var reminders = new Reminders(world.Host);

        await reminders.RunAsync();
        await reminders.RunAsync();

        Assert.Single(await reminders.MailsAsync(EmailTypes.TrialEndsTomorrow), m => m.ToEmail == "maria@example.com");
        Assert.DoesNotContain(await reminders.MailsAsync(EmailTypes.TrialEndsSoon), m => m.ToEmail == "maria@example.com");
    }

    [Fact]
    public async Task No_reminder_for_salons_that_are_not_in_the_trial_or_whose_trial_is_over()
    {
        await using var world = await NewWorldAsync(trial: true);
        var reminders = new Reminders(world.Host);

        foreach (var (status, ends) in new[] { (SalonStatus.Active, Now.AddDays(2)), (SalonStatus.Suspended, Now.AddDays(2)), (SalonStatus.Trial, Now.AddDays(-1)) })
        {
            await world.Salon.ConfigureSalonAsync(s =>
            {
                s.Status = status;
                s.TrialEndsAtUtc = ends;
            });
            await reminders.RunAsync();
        }

        Assert.DoesNotContain(await reminders.MailsAsync(EmailTypes.TrialEndsSoon), m => m.ToEmail == "maria@example.com");
        Assert.DoesNotContain(await reminders.MailsAsync(EmailTypes.TrialEndsTomorrow), m => m.ToEmail == "maria@example.com");
    }

    [Fact]
    public async Task After_an_extension_the_reminders_come_again_for_the_new_end()
    {
        await using var world = await NewWorldAsync(trial: true);
        await world.Salon.ConfigureSalonAsync(s => s.TrialEndsAtUtc = Now.AddDays(5));
        var reminders = new Reminders(world.Host);
        await reminders.RunAsync();

        (await world.Admin.PostAsync($"/api/admin/salons/{world.SalonId}/trial/extend", new ExtendTrialRequest(2))).EnsureSuccessStatusCode(); // ends in 7 days
        await reminders.RunAsync();

        Assert.Equal(2, (await reminders.MailsAsync(EmailTypes.TrialEndsSoon)).Count(m => m.ToEmail == "maria@example.com"));
    }

    [Fact]
    public async Task The_reminder_goes_to_the_owner_in_their_own_language()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var session = host.NewSession();
        (await session.PostAsync("/api/auth/register", AuthTestHost.Registration("ayse@example.com", "Salon Ayşe", "tr"))).EnsureSuccessStatusCode();
        host.Clock.Advance(TimeSpan.FromDays(25));

        await new Reminders(host).RunAsync();

        Assert.Equal("tr", (await new Reminders(host).MailsAsync(EmailTypes.TrialEndsSoon)).Single(m => m.ToEmail == "ayse@example.com").Language);
    }
}
