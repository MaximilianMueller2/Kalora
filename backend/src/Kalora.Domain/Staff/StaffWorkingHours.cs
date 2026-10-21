using Kalora.Domain.Common;

namespace Kalora.Domain.Staff;

/// <summary>A working block on a weekday, valid from <see cref="ValidFrom"/> so schedules can change over time.</summary>
public class StaffWorkingHours : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public DayOfWeek Weekday { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }
}
