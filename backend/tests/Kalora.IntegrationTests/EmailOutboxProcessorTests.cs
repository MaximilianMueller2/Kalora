using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Email;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class EmailOutboxProcessorTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Queue<SendOutcome> Outcomes { get; } = new();

        public SendOutcome Default { get; set; } = SendOutcome.Sent;

        public Task<SendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(Outcomes.Count > 0 ? Outcomes.Dequeue() : Default);
        }
    }

    private sealed class Setup
    {
        public required string ConnectionString { get; init; }

        public required FakeTimeProvider Clock { get; init; }

        public RecordingSender Sender { get; } = new();

        public int BatchSize { get; set; } = 20;

        public EmailOutboxProcessor Processor()
        {
            var db = TestDb.CreateContext(ConnectionString, Clock);
            var options = Options.Create(new MailOptions { FromAddress = "noreply@kalora.test", Worker = { BatchSize = BatchSize } });
            return new EmailOutboxProcessor(db, new EmailRenderer(), Sender, options, Clock, NullLogger<EmailOutboxProcessor>.Instance);
        }

        public async Task<Salon> AddSalonAsync(string name = "Haarwerk")
        {
            var salon = new Salon { Name = name, Slug = $"s-{Guid.NewGuid():N}" };
            await using var db = TestDb.CreateContext(ConnectionString, Clock, salon.Id);
            db.Salons.Add(salon);
            await db.SaveChangesAsync();
            return salon;
        }

        public async Task<EmailOutbox> QueueAsync(Salon salon, string type = EmailTypes.BookingConfirmed, string? payload = null, Action<EmailOutbox>? change = null)
        {
            var mail = new EmailOutbox
            {
                SalonId = salon.Id,
                ToEmail = "kim@example.com",
                Type = type,
                Language = "de",
                PayloadJson = payload ?? JsonSerializer.Serialize(new { SalonName = salon.Name, ServiceName = "Färben", StartUtc = Start.AddDays(1), TimeZone = "Europe/Berlin", ManageUrl = "https://kalora.app/x/appointment/T" }),
            };
            change?.Invoke(mail);
            await using var db = TestDb.CreateContext(ConnectionString, Clock, salon.Id);
            db.EmailOutbox.Add(mail);
            await db.SaveChangesAsync();
            return mail;
        }

        public async Task<EmailOutbox> ReloadAsync(EmailOutbox mail)
        {
            await using var db = TestDb.CreateContext(ConnectionString, Clock, mail.SalonId);
            return await db.EmailOutbox.AsNoTracking().SingleAsync(m => m.Id == mail.Id);
        }
    }

    private async Task<Setup> NewSetupAsync() => new()
    {
        ConnectionString = await postgres.CreateMigratedDatabaseAsync(),
        Clock = new FakeTimeProvider(Start),
    };

    [Fact]
    public async Task A_due_mail_is_rendered_sent_and_marked_as_sent()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync("Haarwerk Müller");
        var mail = await setup.QueueAsync(salon);

        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(new ProcessResult(1, 0, 0), result);
        var sent = Assert.Single(setup.Sender.Sent);
        Assert.Equal("kim@example.com", sent.To);
        Assert.Equal("Dein Termin bei Haarwerk Müller ist bestätigt", sent.Subject);
        Assert.Contains("Färben", sent.Text);

        var stored = await setup.ReloadAsync(mail);
        Assert.Equal(EmailStatus.Sent, stored.Status);
        Assert.Equal(1, stored.Attempts);
        Assert.Equal(Start, stored.SentAtUtc);
        Assert.Null(stored.LastError);
        Assert.Null(stored.NextAttemptAtUtc);
        Assert.Equal(sent.Subject, stored.Subject);
        Assert.Equal(sent.Html, stored.BodyHtml);
        Assert.Equal(sent.Text, stored.BodyText);
    }

    [Fact]
    public async Task A_sent_mail_is_not_sent_again()
    {
        var setup = await NewSetupAsync();
        await setup.QueueAsync(await setup.AddSalonAsync());

        await setup.Processor().ProcessDueAsync();
        var second = await setup.Processor().ProcessDueAsync();

        Assert.Equal(0, second.Total);
        Assert.Single(setup.Sender.Sent);
    }

    [Fact]
    public async Task A_mail_that_is_not_due_yet_waits()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        var later = await setup.QueueAsync(salon, change: m => m.NextAttemptAtUtc = Start.AddMinutes(5));

        Assert.Equal(0, (await setup.Processor().ProcessDueAsync()).Total);

        setup.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(1, (await setup.Processor().ProcessDueAsync()).Sent);
        Assert.Equal(EmailStatus.Sent, (await setup.ReloadAsync(later)).Status);
    }

    [Fact]
    public async Task Mails_of_all_salons_are_handled_by_the_one_background_job()
    {
        var setup = await NewSetupAsync();
        var first = await setup.AddSalonAsync("Eins");
        var second = await setup.AddSalonAsync("Zwei");
        await setup.QueueAsync(first);
        await setup.QueueAsync(second);

        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(2, result.Sent);
        Assert.Equal(["Dein Termin bei Eins ist bestätigt", "Dein Termin bei Zwei ist bestätigt"], setup.Sender.Sent.Select(m => m.Subject).Order());
    }

    [Fact]
    public async Task One_run_sends_at_most_one_batch_oldest_first()
    {
        var setup = await NewSetupAsync();
        setup.BatchSize = 2;
        var salon = await setup.AddSalonAsync();
        var third = await setup.QueueAsync(salon, change: m => m.NextAttemptAtUtc = Start.AddMinutes(-1));
        var first = await setup.QueueAsync(salon, change: m => m.NextAttemptAtUtc = Start.AddMinutes(-30));
        var second = await setup.QueueAsync(salon, change: m => m.NextAttemptAtUtc = Start.AddMinutes(-10));

        Assert.Equal(2, (await setup.Processor().ProcessDueAsync()).Sent);

        Assert.Equal(EmailStatus.Sent, (await setup.ReloadAsync(first)).Status);
        Assert.Equal(EmailStatus.Sent, (await setup.ReloadAsync(second)).Status);
        Assert.Equal(EmailStatus.Pending, (await setup.ReloadAsync(third)).Status);
        Assert.Equal(1, (await setup.Processor().ProcessDueAsync()).Sent);
    }

    [Fact]
    public async Task A_temporary_error_schedules_another_attempt_after_a_minute()
    {
        var setup = await NewSetupAsync();
        var mail = await setup.QueueAsync(await setup.AddSalonAsync());
        setup.Sender.Outcomes.Enqueue(SendOutcome.Temporary("421 try later"));

        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(new ProcessResult(0, 1, 0), result);
        var stored = await setup.ReloadAsync(mail);
        Assert.Equal(EmailStatus.Pending, stored.Status);
        Assert.Equal(1, stored.Attempts);
        Assert.Equal(Start.AddMinutes(1), stored.NextAttemptAtUtc);
        Assert.Equal("421 try later", stored.LastError);
        Assert.Null(stored.SentAtUtc);
        Assert.Null(stored.BodyHtml);
        Assert.Equal(0, (await setup.Processor().ProcessDueAsync()).Total); // not before the minute is over

        setup.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, (await setup.Processor().ProcessDueAsync()).Sent);
        var done = await setup.ReloadAsync(mail);
        Assert.Equal(EmailStatus.Sent, done.Status);
        Assert.Equal(2, done.Attempts);
        Assert.Null(done.LastError);
    }

    [Fact]
    public async Task Repeated_temporary_errors_back_off_and_end_as_failed_after_five_attempts()
    {
        var setup = await NewSetupAsync();
        var mail = await setup.QueueAsync(await setup.AddSalonAsync());
        setup.Sender.Default = SendOutcome.Temporary("connection refused");
        TimeSpan[] waits = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

        foreach (var wait in waits)
        {
            var before = setup.Clock.GetUtcNow();
            Assert.Equal(1, (await setup.Processor().ProcessDueAsync()).Retried);
            Assert.Equal(before + wait, (await setup.ReloadAsync(mail)).NextAttemptAtUtc);
            setup.Clock.Advance(wait);
        }

        var last = await setup.Processor().ProcessDueAsync();

        Assert.Equal(new ProcessResult(0, 0, 1), last);
        var stored = await setup.ReloadAsync(mail);
        Assert.Equal(EmailStatus.Failed, stored.Status);
        Assert.Equal(5, stored.Attempts);
        Assert.Null(stored.NextAttemptAtUtc);
        Assert.Equal("connection refused", stored.LastError);
        Assert.Equal(5, setup.Sender.Sent.Count);

        setup.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, (await setup.Processor().ProcessDueAsync()).Total); // failed mails stay failed
    }

    [Fact]
    public async Task A_permanent_error_fails_at_once()
    {
        var setup = await NewSetupAsync();
        var mail = await setup.QueueAsync(await setup.AddSalonAsync());
        setup.Sender.Outcomes.Enqueue(SendOutcome.Permanent("550 mailbox unknown"));

        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(new ProcessResult(0, 0, 1), result);
        var stored = await setup.ReloadAsync(mail);
        Assert.Equal(EmailStatus.Failed, stored.Status);
        Assert.Equal(1, stored.Attempts);
        Assert.Null(stored.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Addresses_in_server_messages_are_not_stored()
    {
        var setup = await NewSetupAsync();
        var mail = await setup.QueueAsync(await setup.AddSalonAsync());
        setup.Sender.Outcomes.Enqueue(SendOutcome.Permanent("550 5.1.1 <kim@example.com>: user kim@example.com does not exist"));

        await setup.Processor().ProcessDueAsync();

        var stored = await setup.ReloadAsync(mail);
        Assert.DoesNotContain("kim@example.com", stored.LastError);
        Assert.Contains("<address>", stored.LastError);
    }

    [Fact]
    public async Task A_mail_that_cannot_be_rendered_fails_without_calling_the_mail_server()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        var unknown = await setup.QueueAsync(salon, type: "booking.unknown");
        var broken = await setup.QueueAsync(salon, payload: "{not json");

        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(new ProcessResult(0, 0, 2), result);
        Assert.Empty(setup.Sender.Sent);
        Assert.StartsWith("Template error", (await setup.ReloadAsync(unknown)).LastError);
        Assert.StartsWith("Template error", (await setup.ReloadAsync(broken)).LastError);
    }

    [Fact]
    public async Task A_mail_stuck_in_sending_is_queued_again_after_ten_minutes()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync();
        var stuck = await setup.QueueAsync(salon, change: m => { m.Status = EmailStatus.Sending; m.Attempts = 1; });

        Assert.Equal(0, (await setup.Processor().ProcessDueAsync()).Total); // might still be in flight

        setup.Clock.Advance(TimeSpan.FromMinutes(11));
        var result = await setup.Processor().ProcessDueAsync();

        Assert.Equal(1, result.Sent);
        var stored = await setup.ReloadAsync(stuck);
        Assert.Equal(EmailStatus.Sent, stored.Status);
        Assert.Equal(2, stored.Attempts);
    }

    [Fact]
    public async Task Reminders_and_cancellations_render_with_the_stored_payload_language()
    {
        var setup = await NewSetupAsync();
        var salon = await setup.AddSalonAsync("Salon Ay");
        await setup.QueueAsync(salon, type: EmailTypes.BookingReminder, change: m => m.Language = "tr");

        await setup.Processor().ProcessDueAsync();

        Assert.Contains("Salon Ay", Assert.Single(setup.Sender.Sent).Subject);
        Assert.Contains("lang=\"tr\"", setup.Sender.Sent[0].Html);
    }
}
