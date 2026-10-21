using Kalora.Domain.Common;

namespace Kalora.Domain.Staff;

public class StaffAbsence : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public AbsenceType Type { get; set; }
}
