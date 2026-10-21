using Kalora.Domain.Common;

namespace Kalora.Domain.Salons;

/// <summary>A day on which the salon is closed (public holiday, company holiday).</summary>
public class SalonClosure : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public DateOnly Date { get; set; }

    public string? Reason { get; set; }
}
