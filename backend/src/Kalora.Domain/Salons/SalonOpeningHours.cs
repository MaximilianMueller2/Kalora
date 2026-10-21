using Kalora.Domain.Common;

namespace Kalora.Domain.Salons;

/// <summary>One opening block on a weekday (local salon time); a day may have several blocks.</summary>
public class SalonOpeningHours : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public DayOfWeek Weekday { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }
}
