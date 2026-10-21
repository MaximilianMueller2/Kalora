using Kalora.Domain.Common;

namespace Kalora.Domain.Services;

public class Service : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Price in cents of <see cref="Currency"/>; never a floating-point amount.</summary>
    public int PriceCents { get; set; }

    /// <summary>True for "from" prices ("ab 25 €").</summary>
    public bool PriceIsFrom { get; set; }

    public string Currency { get; set; } = "EUR";

    public int DurationMinutes { get; set; }

    public int BufferBeforeMinutes { get; set; }

    public int BufferAfterMinutes { get; set; }

    public bool IsBookableOnline { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
