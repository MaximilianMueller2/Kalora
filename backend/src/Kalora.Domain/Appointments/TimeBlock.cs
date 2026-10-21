using Kalora.Domain.Common;

namespace Kalora.Domain.Appointments;

/// <summary>A blocker in a staff member's calendar (private appointment, walk-in, break).</summary>
public class TimeBlock : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public string? Label { get; set; }
}
