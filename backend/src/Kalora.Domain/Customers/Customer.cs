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

    /// <summary>The name an anonymized customer keeps, so lists and appointments stay readable without a person behind them.</summary>
    public const string AnonymizedName = "Anonymisiert";

    /// <summary>
    /// Removes everything that points to a person: name, e-mail, phone and marketing consent. The visit history stays as numbers
    /// (last visit, no-show count); the appointments themselves are cleaned by the caller.
    /// </summary>
    public void Anonymize(DateTimeOffset now)
    {
        Name = AnonymizedName;
        Email = null;
        Phone = null;
        MarketingConsentAtUtc = null;
        AnonymizedAtUtc = now;
    }
}
