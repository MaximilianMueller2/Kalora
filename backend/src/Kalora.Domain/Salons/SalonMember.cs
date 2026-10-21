using Kalora.Domain.Common;

namespace Kalora.Domain.Salons;

/// <summary>Links a login user to a salon with a role.</summary>
public class SalonMember : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid UserId { get; set; }

    public SalonRole Role { get; set; }
}
