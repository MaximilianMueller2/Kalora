using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Subscriptions;

/// <summary>
/// The salons of all tenants are read here on purpose (listed in the architecture test): the job has no tenant.
/// Each mail goes out once per trial end; a stamp on the salon remembers it, and extending the trial clears the stamps.
/// A trial that is already inside the last day gets only the "tomorrow" mail. The mail is queued together with the stamp, so a
/// crash in between sends it again rather than never.
/// </summary>
internal sealed partial class TrialReminderService(
    IAppDbContext db,
    IEmailOutbox outbox,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider,
    ILogger<TrialReminderService> logger) : ITrialReminderService
{
    private static readonly TimeSpan WeekAhead = TimeSpan.FromDays(7);
    private static readonly TimeSpan DayAhead = TimeSpan.FromDays(1);

    public async Task<TrialReminderResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var salons = await db.AllSalons()
            .Where(s => s.Status == SalonStatus.Trial && s.TrialEndsAtUtc != null && s.TrialEndsAtUtc > now && s.TrialEndsAtUtc <= now + WeekAhead
                && (s.TrialWeekReminderSentAtUtc == null || s.TrialDayReminderSentAtUtc == null))
            .ToListAsync(cancellationToken);
        if (salons.Count == 0)
        {
            return new TrialReminderResult(0, 0);
        }

        var owners = await (
            from member in db.AllTenants<SalonMember>()
            join user in db.Users on member.UserId equals user.Id
            where member.Role == SalonRole.Owner && salons.Select(s => s.Id).Contains(member.SalonId) && user.Email != null
            select new { member.SalonId, Email = user.Email!, user.DisplayName, user.PreferredLanguage }).ToListAsync(cancellationToken);
        var ownersBySalon = owners.ToLookup(o => o.SalonId);

        int week = 0, day = 0;
        foreach (var salon in salons)
        {
            var left = salon.TrialEndsAtUtc!.Value - now;
            string? type = null;
            if (left <= DayAhead && salon.TrialDayReminderSentAtUtc is null)
            {
                type = EmailTypes.TrialEndsTomorrow;
                salon.TrialDayReminderSentAtUtc = now;
                salon.TrialWeekReminderSentAtUtc ??= now;
                day++;
            }
            else if (left > DayAhead && salon.TrialWeekReminderSentAtUtc is null)
            {
                type = EmailTypes.TrialEndsSoon;
                salon.TrialWeekReminderSentAtUtc = now;
                week++;
            }

            if (type is null)
            {
                continue;
            }

            foreach (var owner in ownersBySalon[salon.Id])
            {
                outbox.Enqueue(salon.Id, owner.Email, type, string.IsNullOrEmpty(owner.PreferredLanguage) ? salon.DefaultLanguage : owner.PreferredLanguage, new
                {
                    SalonName = salon.Name,
                    owner.DisplayName,
                    ExpiresAtUtc = salon.TrialEndsAtUtc,
                    salon.TimeZone,
                    AppUrl = AppLinks.Dashboard(appOptions.Value.PublicBaseUrl),
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogRun(logger, week, day);
        return new TrialReminderResult(week, day);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trial reminders: {Week} week mail(s), {Day} day mail(s) queued")]
    private static partial void LogRun(ILogger logger, int week, int day);
}
