using Kalora.Domain.Common;

namespace Kalora.Domain.Customers;

/// <summary>A salon's customer. Never shared between salons; each salon is responsible for its own customer data.</summary>
public class Customer : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Normalized to lower case; null once the customer has been anonymized.</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string PreferredLanguage { get; set; } = "de";

    public DateTimeOffset? LastVisitAtUtc { get; set; }

    public int NoShowCount { get; set; }

    public DateTimeOffset? MarketingConsentAtUtc { get; set; }

    public DateTimeOffset? AnonymizedAtUtc { get; set; }
}
