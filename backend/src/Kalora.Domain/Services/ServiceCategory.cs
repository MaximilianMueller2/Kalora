using Kalora.Domain.Common;

namespace Kalora.Domain.Services;

public class ServiceCategory : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
