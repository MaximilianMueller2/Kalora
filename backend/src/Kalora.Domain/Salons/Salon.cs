using Kalora.Domain.Common;

namespace Kalora.Domain.Salons;

/// <summary>The tenant. Its <see cref="Entity.Id"/> is the SalonId carried by all salon-owned tables.</summary>
public class Salon : Entity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public SalonIndustry Industry { get; set; }

    public string Street { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    /// <summary>IANA time zone ID; all local-time rules (opening hours, DST) use it.</summary>
    public string TimeZone { get; set; } = "Europe/Berlin";

    public string DefaultLanguage { get; set; } = "de";

    public int SlotIntervalMinutes { get; set; } = 15;

    public int MinLeadTimeMinutes { get; set; } = 120;

    public int BookingHorizonDays { get; set; } = 60;

    public int CancellationDeadlineHours { get; set; } = 24;

    public bool AutoConfirm { get; set; }

    /// <summary>How long a customer's request may stay unanswered before it expires.</summary>
    public int RequestExpiryHours { get; set; } = 24;

    public int RetentionMonths { get; set; } = 24;

    /// <summary>Legal notice of the salon, shown on its booking page.</summary>
    public string? ImprintText { get; set; }

    public string? PrivacyPolicyUrl { get; set; }

    /// <summary>
    /// The salon's own privacy notice (Markdown) shown on its booking page; without it the page shows the template with the salon's data.
    /// </summary>
    public string? PrivacyText { get; set; }

    public PlanCode PlanCode { get; set; } = PlanCode.Solo;

    public DateTimeOffset? TrialEndsAtUtc { get; set; }

    public SalonStatus Status { get; set; } = SalonStatus.Trial;

    /// <summary>Internal note of the platform admin (billing agreements, contact history). Never shown to the salon.</summary>
    public string? AdminNote { get; set; }

    /// <summary>When the "trial ends in a week" mail went out; reset when the trial is extended.</summary>
    public DateTimeOffset? TrialWeekReminderSentAtUtc { get; set; }

    /// <summary>When the "trial ends tomorrow" mail went out; reset when the trial is extended.</summary>
    public DateTimeOffset? TrialDayReminderSentAtUtc { get; set; }

    /// <summary>
    /// What the salon may do right now. Trial and Active run fully; a past-due salon keeps working until the admin acts;
    /// a salon whose trial is over, that is suspended or cancelled can only look at its data (and use its data-protection rights).
    /// The trial is not switched off by a job: it simply stops counting once <see cref="TrialEndsAtUtc"/> has passed.
    /// </summary>
    public SalonAccess AccessAt(DateTimeOffset now) => Status switch
    {
        SalonStatus.Suspended => SalonAccess.Suspended,
        SalonStatus.Cancelled => SalonAccess.Cancelled,
        SalonStatus.Trial when TrialEndsAtUtc is { } end && end <= now => SalonAccess.TrialExpired,
        _ => SalonAccess.Full,
    };
}
