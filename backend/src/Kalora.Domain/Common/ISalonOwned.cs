namespace Kalora.Domain.Common;

/// <summary>Marks an entity that belongs to exactly one salon (tenant).</summary>
public interface ISalonOwned
{
    Guid SalonId { get; set; }
}
