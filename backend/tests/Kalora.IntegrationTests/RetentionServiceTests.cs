using System.Text.Json;
using Kalora.Application.Configuration;
using Kalora.Application.Customers;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class RetentionServiceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed class Setup
    {
        public required string ConnectionString { get; init; }

        public required FakeTimeProvider Clock { get; init; }

        public int BatchSize { get; set; } = 100;

        public RetentionService Service()
        {
            var db = TestDb.CreateContext(ConnectionString, Clock);
            return new RetentionService(db, new CustomerAnonymizer(db), Options.Create(new RetentionOptions { BatchSize = BatchSize }), Clock, NullLogger<RetentionService>.Instance);
        }

        public async Task<SalonSeeder> AddSalonAsync(int retentionMonths = 24)
        {
            var salon = new Salon { Name = "Haarwerk", Slug = $"s-{Guid.NewGuid():N}", RetentionMonths = retentionMonths };
            await using (var db = TestDb.CreateContext(ConnectionString, Clock, salon.Id))
            {
                db.Salons.Add(salon);
                await db.SaveChangesAsync();
            }

            return new SalonSeeder(ConnectionString, salon.Id);
        }
    }

    private async Task<Setup> NewSetupAsync() => new()
    {
        ConnectionString = await postgres.CreateMigratedDatabaseAsync(),
        Clock = new FakeTimeProvider(Now),
    };

    private static async Task<(Customer Customer, Appointment? Appointment)> AddCustomerAsync(SalonSeeder salon, string name, DateTimeOffset? lastAppointmentEnd, DateTimeOffset? created = null)
    {
        var service = await salon.AddServiceAsync(name: $"Leistung {name}");
        var staff = await salon.AddStaffAsync($"Person {name}", 0, offers: service);
        return await salon.WithDbAsync(async db =>
        {
            var customer = new Customer { Name = name, Email = $"{name.ToLowerInvariant().Replace(' ', '.')}@example.com", Phone = "+49 170 1" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            if (created is not null)
            {
                await db.Customers.Where(c => c.Id == customer.Id).ExecuteUpdateAsync(set => set.SetProperty(c => c.CreatedAtUtc, created.Value));
            }

            Appointment? appointment = null;
            if (lastAppointmentEnd is { } end)
            {
                appointment = new Appointment
                {
                    StaffId = staff.Id, ServiceId = service.Id, CustomerId = customer.Id, StartUtc = end.AddHours(-1), EndUtc = end, BlockedStartUtc = end.AddHours(-1), BlockedEndUtc = end,
                    Status = end > Now ? AppointmentStatus.Confirmed : AppointmentStatus.Completed, ServiceName = service.Name, DurationMinutes = 60,
                    CustomerNote = "Notiz", InternalNote = "Intern", ManageTokenHash = "hash-" + Guid.NewGuid().ToString("N"),
                };
                db.Appointments.Add(appointment);
                await db.SaveChangesAsync();
            }

            return (customer, appointment);
        });
    }

    private static Task<Customer> Reload(SalonSeeder salon, Customer customer) => salon.WithDbAsync(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id));

    // ------------------------------------------------------------ customers

    [Fact]
    public async Task A_customer_without_a_visit_for_the_retention_period_is_anonymized_and_logged()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync(24);
        var (old, appointment) = await AddCustomerAsync(salon, "Alt Kunde", Now.AddMonths(-25));

        var result = await setup.Service().RunAsync();

        Assert.Equal(1, result.Customers);
        var stored = await Reload(salon, old);
        Assert.Equal((Customer.AnonymizedName, null, null, Now), (stored.Name, stored.Email, stored.Phone, stored.AnonymizedAtUtc));
        await salon.WithDbAsync(async db =>
        {
            var a = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == appointment!.Id);
            Assert.Equal((null, null, null), (a.CustomerNote, a.InternalNote, a.ManageTokenHash));
            var audit = await db.AuditLogs.SingleAsync(x => x.Action == "customer.anonymized");
            Assert.Null(audit.ActorUserId);
            var details = JsonDocument.Parse(audit.Details!).RootElement;
            Assert.Equal(("retention", 24), (details.GetProperty("reason").GetString(), details.GetProperty("months").GetInt32()));
            return 0;
        });
    }

    [Fact]
    public async Task Whoever_came_within_the_period_or_has_an_appointment_to_come_is_kept()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync(24);
        var (recent, _) = await AddCustomerAsync(salon, "Neu Kunde", Now.AddMonths(-23));
        var (future, _) = await AddCustomerAsync(salon, "Bald Kunde", Now.AddMonths(2));

        var result = await setup.Service().RunAsync();

        Assert.Equal(0, result.Customers);
        Assert.Null((await Reload(salon, recent)).AnonymizedAtUtc);
        Assert.Null((await Reload(salon, future)).AnonymizedAtUtc);
    }

    [Fact]
    public async Task A_customer_who_never_had_an_appointment_counts_from_the_day_they_were_created()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync(12);
        var (stale, _) = await AddCustomerAsync(salon, "Nie Gekommen", null, created: Now.AddMonths(-13));
        var (fresh, _) = await AddCustomerAsync(salon, "Gerade Angelegt", null, created: Now.AddMonths(-2));

        await setup.Service().RunAsync();

        Assert.NotNull((await Reload(salon, stale)).AnonymizedAtUtc);
        Assert.Null((await Reload(salon, fresh)).AnonymizedAtUtc);
    }

    [Fact]
    public async Task Every_salon_has_its_own_period()
    {
        var setup = await NewSetupAsync();
        var shortSalon = await setup.AddSalonAsync(6);
        var longSalon = await setup.AddSalonAsync(36);
        var (inShort, _) = await AddCustomerAsync(shortSalon, "Kurz Kunde", Now.AddMonths(-8));
        var (inLong, _) = await AddCustomerAsync(longSalon, "Lang Kunde", Now.AddMonths(-8));

        await setup.Service().RunAsync();

        Assert.NotNull((await Reload(shortSalon, inShort)).AnonymizedAtUtc);
        Assert.Null((await Reload(longSalon, inLong)).AnonymizedAtUtc);
    }

    [Fact]
    public async Task Running_again_does_nothing_and_a_small_batch_still_gets_everybody()
    {
        var setup = await NewSetupAsync();
        setup.BatchSize = 2;
        var salon = await setup.AddSalonAsync();
        for (var i = 0; i < 5; i++)
        {
            await AddCustomerAsync(salon, $"Alt {i}", Now.AddMonths(-30));
        }

        var first = await setup.Service().RunAsync();
        var second = await setup.Service().RunAsync();

        Assert.Equal(5, first.Customers);
        Assert.Equal(0, second.Customers);
        Assert.Equal(5, await salon.WithDbAsync(db => db.Customers.CountAsync(c => c.AnonymizedAtUtc != null)));
    }

    [Fact]
    public async Task An_anonymized_customer_is_not_anonymized_again()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        var (customer, _) = await AddCustomerAsync(salon, "Alt Kunde", Now.AddMonths(-30));
        await setup.Service().RunAsync();
        setup.Clock.Advance(TimeSpan.FromDays(30));

        await setup.Service().RunAsync();

        Assert.Equal(Now, (await Reload(salon, customer)).AnonymizedAtUtc); // the first date stays
        Assert.Single(await salon.WithDbAsync(db => db.AuditLogs.Where(a => a.Action == "customer.anonymized").ToListAsync()));
    }

    // ------------------------------------------------------------ links, invitations, mails

    [Fact]
    public async Task Management_links_stop_working_thirty_days_after_the_appointment()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync(120);
        var (_, old) = await AddCustomerAsync(salon, "Eins", Now.AddDays(-31));
        var (_, recent) = await AddCustomerAsync(salon, "Zwei", Now.AddDays(-10));
        var (_, future) = await AddCustomerAsync(salon, "Drei", Now.AddDays(10));

        var result = await setup.Service().RunAsync();

        Assert.Equal(1, result.Links);
        await salon.WithDbAsync(async db =>
        {
            Assert.Null((await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == old!.Id)).ManageTokenHash);
            Assert.NotNull((await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == recent!.Id)).ManageTokenHash);
            Assert.NotNull((await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == future!.Id)).ManageTokenHash);
            return 0;
        });
    }

    [Fact]
    public async Task Used_and_expired_invitations_are_deleted_but_open_ones_stay()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        await salon.WithDbAsync(async db =>
        {
            var staff = new StaffMember { DisplayName = "Anna" };
            db.StaffMembers.Add(staff);
            await db.SaveChangesAsync();
            StaffInvitation Invitation(string email, DateTimeOffset expires, DateTimeOffset? accepted) =>
                new() { StaffId = staff.Id, Email = email, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAtUtc = expires, AcceptedAtUtc = accepted };
            db.StaffInvitations.AddRange(
                Invitation("expired@example.com", Now.AddDays(-40), null),
                Invitation("accepted@example.com", Now.AddDays(-20), Now.AddDays(-35)),
                Invitation("just-expired@example.com", Now.AddDays(-5), null),
                Invitation("open@example.com", Now.AddDays(3), null),
                Invitation("fresh-accepted@example.com", Now.AddDays(3), Now.AddDays(-1)));
            await db.SaveChangesAsync();
            return 0;
        });

        var result = await setup.Service().RunAsync();

        Assert.Equal(2, result.Invitations);
        Assert.Equal(["fresh-accepted@example.com", "just-expired@example.com", "open@example.com"], (await salon.WithDbAsync(db => db.StaffInvitations.Select(i => i.Email).ToListAsync())).Order());
    }

    [Fact]
    public async Task Old_sent_and_failed_mails_lose_their_content_but_keep_their_traces()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        await salon.WithDbAsync(async db =>
        {
            EmailOutbox Mail(EmailStatus status, int ageDays)
            {
                var mail = new EmailOutbox { ToEmail = "kim@example.com", Type = "booking.confirmed", Language = "de", Subject = "Betreff", PayloadJson = """{"CustomerName":"Kim"}""", BodyHtml = "<p>Kim</p>", BodyText = "Kim", Status = status, Attempts = 1 };
                return mail;
            }

            var mails = new[] { Mail(EmailStatus.Sent, 40), Mail(EmailStatus.Failed, 40), Mail(EmailStatus.Sent, 5), Mail(EmailStatus.Pending, 40) };
            db.EmailOutbox.AddRange(mails);
            await db.SaveChangesAsync();
            var ages = new[] { 40, 40, 5, 40 };
            for (var i = 0; i < mails.Length; i++)
            {
                var id = mails[i].Id;
                var created = Now.AddDays(-ages[i]);
                await db.EmailOutbox.Where(m => m.Id == id).ExecuteUpdateAsync(set => set.SetProperty(m => m.CreatedAtUtc, created));
            }

            return 0;
        });

        var result = await setup.Service().RunAsync();

        Assert.Equal(2, result.Mails);
        var stored = await salon.WithDbAsync(db => db.EmailOutbox.AsNoTracking().OrderBy(m => m.CreatedAtUtc).ToListAsync());
        // Oldest first: the two old finished mails are empty, the old pending one waits to be sent, the recent one is complete.
        var emptied = stored.Where(m => m.PayloadJson == null).ToList();
        Assert.Equal(2, emptied.Count);
        Assert.All(emptied, m => Assert.Equal((string.Empty, string.Empty, null, "booking.confirmed", 1), (m.ToEmail, m.Subject, m.BodyHtml, m.Type, m.Attempts)));
        Assert.Equal([EmailStatus.Sent, EmailStatus.Failed], emptied.Select(m => m.Status).Order());
        Assert.Equal(2, stored.Count(m => m.PayloadJson != null));
        Assert.Contains(stored, m => m.Status == EmailStatus.Pending && m.PayloadJson != null);
    }

    [Fact]
    public async Task The_whole_run_counts_what_it_did_and_a_second_run_finds_nothing()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        await AddCustomerAsync(salon, "Alt Kunde", Now.AddMonths(-30));

        var first = await setup.Service().RunAsync();
        var second = await setup.Service().RunAsync();

        Assert.Equal(1, first.Customers);
        Assert.Equal(0, second.Total);
    }
}
