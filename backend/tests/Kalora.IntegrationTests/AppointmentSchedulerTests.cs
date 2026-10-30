using System.Text.Json;
using Kalora.Application.Appointments;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class AppointmentSchedulerTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed class Setup
    {
        public required string ConnectionString { get; init; }

        public required FakeTimeProvider Clock { get; init; }

        public int BatchSize { get; set; } = 100;

        public AppointmentScheduler Scheduler()
        {
            var db = TestDb.CreateContext(ConnectionString, Clock);
            var app = Options.Create(new AppOptions { PublicBaseUrl = "https://kalora.app", ManageTokenSecret = new string('s', 40) });
            var mails = new AppointmentMails(new EmailOutboxWriter(db, Clock), new ManageTokens(app), app, Clock);
            return new AppointmentScheduler(db, mails, Options.Create(new SchedulerOptions { BatchSize = BatchSize }), Clock, NullLogger<AppointmentScheduler>.Instance);
        }

        public async Task<(SalonSeeder Salon, Service Cut, StaffMember Anna)> AddSalonAsync(string name = "Haarwerk")
        {
            var salon = new Salon { Name = name, Slug = $"s-{Guid.NewGuid():N}", Phone = "+49 30 123456" };
            await using (var db = TestDb.CreateContext(ConnectionString, Clock, salon.Id))
            {
                db.Salons.Add(salon);
                await db.SaveChangesAsync();
            }

            var seeder = new SalonSeeder(ConnectionString, salon.Id);
            var cut = await seeder.AddServiceAsync();
            var anna = await seeder.AddStaffAsync("Anna", 0, offers: cut);
            return (seeder, cut, anna);
        }

        public async Task<Appointment> ReloadAsync(SalonSeeder salon, Appointment appointment) =>
            await salon.WithDbAsync(db => db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id));
    }

    private async Task<Setup> NewSetupAsync() => new()
    {
        ConnectionString = await postgres.CreateMigratedDatabaseAsync(),
        Clock = new FakeTimeProvider(Start),
    };

    private static async Task<Appointment> ConfirmedAsync(SalonSeeder salon, Service cut, StaffMember staff, DateTimeOffset start, DateTimeOffset? confirmedAt = null)
    {
        var appointment = await salon.AddAppointmentAsync(cut, staff, start, AppointmentStatus.Confirmed);
        return await salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.SingleAsync(a => a.Id == appointment.Id);
            stored.ConfirmedAtUtc = confirmedAt ?? Start.AddDays(-10);
            await db.SaveChangesAsync();
            return stored;
        });
    }

    // ------------------------------------------------------------ expiry

    [Fact]
    public async Task A_request_that_ran_out_of_time_expires_and_the_customer_is_told()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync("Haarwerk Müller");
        var request = await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(-1), language: "en");

        var result = await setup.Scheduler().RunAsync();

        Assert.Equal(new SchedulerResult(1, 0), result);
        var stored = await setup.ReloadAsync(salon, request);
        Assert.Equal((AppointmentStatus.Expired, Start), (stored.Status, stored.ExpiredAtUtc));
        await salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingExpired);
            Assert.Equal("en", mail.Language);
            var payload = JsonDocument.Parse(mail.PayloadJson!).RootElement;
            Assert.Equal("Haarwerk Müller", payload.GetProperty("SalonName").GetString());
            Assert.Equal("https://kalora.app/" + (await db.Salons.SingleAsync()).Slug, payload.GetProperty("BookingUrl").GetString());
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "appointment.expired");
            Assert.Null(audit.ActorUserId);
            Assert.Equal(request.Id, audit.EntityId);
            return 0;
        });
    }

    [Fact]
    public async Task The_time_of_an_expired_request_is_free_again()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(-1));
        await setup.Scheduler().RunAsync();

        var again = await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), AppointmentStatus.Confirmed);

        Assert.NotEqual(Guid.Empty, again.Id);
    }

    [Fact]
    public async Task Requests_with_time_left_and_appointments_in_other_states_are_left_alone()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        var open = await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(1));
        var confirmed = await ConfirmedAsync(salon, cut, anna, Start.AddDays(3));
        var declined = await salon.AddAppointmentAsync(cut, anna, Start.AddDays(4), AppointmentStatus.Declined);

        var result = await setup.Scheduler().RunAsync();

        Assert.Equal(0, result.Expired);
        Assert.Equal(AppointmentStatus.Requested, (await setup.ReloadAsync(salon, open)).Status);
        Assert.Equal(AppointmentStatus.Confirmed, (await setup.ReloadAsync(salon, confirmed)).Status);
        Assert.Equal(AppointmentStatus.Declined, (await setup.ReloadAsync(salon, declined)).Status);

        setup.Clock.Advance(TimeSpan.FromMinutes(1)); // now the open one is due, exactly at its deadline
        Assert.Equal(1, (await setup.Scheduler().RunAsync()).Expired);
        Assert.Equal(AppointmentStatus.Expired, (await setup.ReloadAsync(salon, open)).Status);
    }

    [Fact]
    public async Task Running_again_does_nothing_twice()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(-1));

        await setup.Scheduler().RunAsync();
        var second = await setup.Scheduler().RunAsync();

        Assert.Equal(0, second.Total);
        await salon.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingExpired).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task Requests_of_all_salons_expire_in_one_run()
    {
        var setup = await NewSetupAsync();
        var (first, firstCut, firstAnna) = await setup.AddSalonAsync("Eins");
        var (second, secondCut, secondAnna) = await setup.AddSalonAsync("Zwei");
        await first.AddAppointmentAsync(firstCut, firstAnna, Start.AddDays(2), expiresAt: Start.AddHours(-1));
        await second.AddAppointmentAsync(secondCut, secondAnna, Start.AddDays(2), expiresAt: Start.AddHours(-1));

        Assert.Equal(2, (await setup.Scheduler().RunAsync()).Expired);

        foreach (var salon in new[] { first, second })
        {
            await salon.WithDbAsync(async db =>
            {
                Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingExpired).ToListAsync());
                return 0;
            });
        }
    }

    [Fact]
    public async Task One_run_handles_one_batch()
    {
        var setup = await NewSetupAsync();
        setup.BatchSize = 2;
        var (salon, cut, anna) = await setup.AddSalonAsync();
        for (var i = 0; i < 3; i++)
        {
            await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2 + i), expiresAt: Start.AddMinutes(-10 + i));
        }

        Assert.Equal(2, (await setup.Scheduler().RunAsync()).Expired);
        Assert.Equal(1, (await setup.Scheduler().RunAsync()).Expired);
    }

    [Fact]
    public async Task Two_schedulers_at_the_same_moment_expire_a_request_only_once()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(-1));

        var results = await Task.WhenAll(setup.Scheduler().RunAsync(), setup.Scheduler().RunAsync());

        Assert.Equal(1, results.Sum(r => r.Expired));
        await salon.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingExpired).ToListAsync());
            return 0;
        });
    }

    // ------------------------------------------------------------ reminders

    [Fact]
    public async Task A_reminder_goes_out_a_day_before_and_only_once()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync("Haarwerk Müller");
        var soon = await ConfirmedAsync(salon, cut, anna, Start.AddHours(23).AddMinutes(50));

        var first = await setup.Scheduler().RunAsync();
        var second = await setup.Scheduler().RunAsync();

        Assert.Equal(new SchedulerResult(0, 1), first);
        Assert.Equal(0, second.Total);
        Assert.Equal(Start, (await setup.ReloadAsync(salon, soon)).ReminderSentAtUtc);
        await salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingReminder);
            var payload = JsonDocument.Parse(mail.PayloadJson!).RootElement;
            Assert.Equal(soon.Id, payload.GetProperty("AppointmentId").GetGuid());
            Assert.Equal("Anna", payload.GetProperty("StaffName").GetString());
            Assert.Contains("/appointment/", payload.GetProperty("ManageUrl").GetString());
            return 0;
        });
    }

    [Fact]
    public async Task An_appointment_further_away_gets_its_reminder_when_the_day_before_begins()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        var later = await ConfirmedAsync(salon, cut, anna, Start.AddHours(26));

        Assert.Equal(0, (await setup.Scheduler().RunAsync()).Reminders);

        setup.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(1, (await setup.Scheduler().RunAsync()).Reminders);
        Assert.NotNull((await setup.ReloadAsync(salon, later)).ReminderSentAtUtc);
    }

    [Fact]
    public async Task Only_confirmed_future_appointments_are_reminded()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await salon.AddAppointmentAsync(cut, anna, Start.AddHours(5), expiresAt: Start.AddHours(3)); // still a request
        await salon.AddAppointmentAsync(cut, anna, Start.AddHours(8), AppointmentStatus.Cancelled);
        await ConfirmedAsync(salon, cut, anna, Start.AddHours(-1)); // already begun
        await salon.AddAppointmentAsync(cut, anna, Start.AddHours(12), AppointmentStatus.Completed);

        Assert.Equal(0, (await setup.Scheduler().RunAsync()).Reminders);
    }

    [Fact]
    public async Task An_appointment_confirmed_inside_the_last_day_needs_no_reminder()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await ConfirmedAsync(salon, cut, anna, Start.AddHours(10), confirmedAt: Start.AddHours(-1));

        Assert.Equal(0, (await setup.Scheduler().RunAsync()).Reminders);
    }

    [Fact]
    public async Task A_cancelled_appointment_is_not_reminded_even_if_it_was_due()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        var appointment = await ConfirmedAsync(salon, cut, anna, Start.AddHours(10));
        await salon.WithDbAsync(async db =>
        {
            (await db.Appointments.SingleAsync(a => a.Id == appointment.Id)).TryMoveTo(AppointmentStatus.Cancelled, Start);
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(0, (await setup.Scheduler().RunAsync()).Reminders);
    }

    [Fact]
    public async Task Expiry_and_reminders_happen_in_the_same_run()
    {
        var setup = await NewSetupAsync();
        var (salon, cut, anna) = await setup.AddSalonAsync();
        await salon.AddAppointmentAsync(cut, anna, Start.AddDays(2), expiresAt: Start.AddMinutes(-1));
        await ConfirmedAsync(salon, cut, anna, Start.AddHours(5));

        Assert.Equal(new SchedulerResult(1, 1), await setup.Scheduler().RunAsync());
    }
}
