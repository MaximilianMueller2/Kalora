using Kalora.Domain.Common;

namespace Kalora.Domain.Staff;

/// <summary>A calendar column. Can exist without a login (<see cref="UserId"/> is optional).</summary>
public class StaffMember : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Calendar color as hex, e.g. "#7C5CFC".</summary>
    public string Color { get; set; } = "#7C5CFC";

    public Guid? UserId { get; set; }

    public bool IsBookableOnline { get; set; } = true;

    public bool AutoConfirm { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
