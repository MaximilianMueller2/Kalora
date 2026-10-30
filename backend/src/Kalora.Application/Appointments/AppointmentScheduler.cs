using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Appointments;

/// <summary>
/// Lets unanswered requests expire and queues the reminders. The appointments of all salons are read here on purpose,
/// because the job runs without a tenant (listed in the architecture test). Which confirmed appointments are over but
/// not yet marked as attended or missed is not stored: it is a query (<see cref="IAppointmentService.ListAwaitingOutcomeAsync"/>).
/// Run it in one instance only; a second one at the same moment is stopped by the row version of the appointments.
/// </summary>
internal sealed partial class AppointmentScheduler(
    IAppDbContext db,
    AppointmentMails mails,
    IOptions<SchedulerOptions> options,
    TimeProvider timeProvider,
    ILogger<AppointmentScheduler> logger) : IAppointmentScheduler
{
    public async Task<SchedulerResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var expired = await ExpireOpenRequestsAsync(cancellationToken);
        var reminders = await SendRemindersAsync(cancellationToken);
        if (expired + reminders > 0)
        {
            LogRun(logger, expired, reminders);
        }

        return new SchedulerResult(expired, reminders);
    }

    private async Task<int> ExpireOpenRequestsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var due = await db.AllTenants<Appointment>()
            .Where(a => a.Status == AppointmentStatus.Requested && a.ExpiresAtUtc != null && a.ExpiresAtUtc <= now)
            .OrderBy(a => a.ExpiresAtUtc)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        return await ProcessAsync(due, appointment =>
        {
            if (!appointment.TryMoveTo(AppointmentStatus.Expired, now))
            {
                return null;
            }

            db.AuditLogs.Add(new AuditLog
            {
                SalonId = appointment.SalonId,
                ActorUserId = null,
                Action = "appointment.expired",
                EntityType = nameof(Appointment),
                EntityId = appointment.Id,
                Details = JsonSerializer.Serialize(new { from = nameof(AppointmentStatus.Requested), to = nameof(AppointmentStatus.Expired) }),
            });
            return EmailTypes.BookingExpired;
        }, cancellationToken);
    }

    private async Task<int> SendRemindersAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lead = TimeSpan.FromHours(options.Value.ReminderHoursBefore);
        var due = await db.AllTenants<Appointment>()
            .Where(a => a.Status == AppointmentStatus.Confirmed
                && a.ReminderSentAtUtc == null
                && a.StartUtc > now
                && a.StartUtc <= now + lead
                // Booked or confirmed inside the last 24 hours: the confirmation mail is the reminder.
                && a.ConfirmedAtUtc != null && a.ConfirmedAtUtc <= a.StartUtc - lead)
            .OrderBy(a => a.StartUtc)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        return await ProcessAsync(due, appointment =>
        {
            appointment.ReminderSentAtUtc = now;
            return EmailTypes.BookingReminder;
        }, cancellationToken);
    }

    /// <summary>
    /// Applies <paramref name="change"/> to each appointment (it returns the mail to queue, or null to skip) and saves them one by one,
    /// so a conflict with a person answering at the same moment costs only that appointment.
    /// </summary>
    private async Task<int> ProcessAsync(List<Appointment> appointments, Func<Appointment, string?> change, CancellationToken cancellationToken)
    {
        if (appointments.Count == 0)
        {
            return 0;
        }

        var salons = await db.AllSalons().AsNoTracking().Where(s => appointments.Select(a => a.SalonId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        var staff = await db.AllTenants<StaffMember>().AsNoTracking().Where(s => appointments.Select(a => a.StaffId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.DisplayName, cancellationToken);
        var customers = await db.AllTenants<Customer>().AsNoTracking().Where(c => appointments.Select(a => a.CustomerId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var done = 0;
        foreach (var appointment in appointments)
        {
            var mailType = change(appointment);
            if (mailType is null)
            {
                continue;
            }

            if (salons.TryGetValue(appointment.SalonId, out var salon) && customers.TryGetValue(appointment.CustomerId, out var customer))
            {
                mails.QueueForCustomer(mailType, salon, appointment, staff.GetValueOrDefault(appointment.StaffId, string.Empty), customer);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                done++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Answered by a person in the meantime; the next run sees the new state.
                db.ChangeTracker.Clear();
                LogLostRace(logger, appointment.Id);
            }
        }

        return done;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduler: {Expired} request(s) expired, {Reminders} reminder(s) queued")]
    private static partial void LogRun(ILogger logger, int expired, int reminders);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduler: appointment {AppointmentId} was changed meanwhile and is skipped")]
    private static partial void LogLostRace(ILogger logger, Guid appointmentId);
}
