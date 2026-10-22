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

    public PlanCode PlanCode { get; set; } = PlanCode.Solo;

    public DateTimeOffset? TrialEndsAtUtc { get; set; }

    public SalonStatus Status { get; set; } = SalonStatus.Trial;
}
