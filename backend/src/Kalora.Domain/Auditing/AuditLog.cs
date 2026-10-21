using Kalora.Domain.Common;

namespace Kalora.Domain.Auditing;

/// <summary>Who changed what and when. Entries must not contain personal data beyond IDs.</summary>
public class AuditLog : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    /// <summary>The acting user; null for the system (scheduler) or customers acting via link.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Stable action key, e.g. "appointment.confirmed".</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? Details { get; set; }
}
