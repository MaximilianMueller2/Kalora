using Kalora.Domain.Common;

namespace Kalora.Domain.Staff;

/// <summary>Skill: this staff member offers this service, optionally with their own duration.</summary>
public class StaffService : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public Guid ServiceId { get; set; }

    public int? DurationOverrideMinutes { get; set; }
}
