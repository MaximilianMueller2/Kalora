using Kalora.Domain.Common;
using Kalora.Domain.Salons;

namespace Kalora.Domain.Staff;

/// <summary>An invitation to create a login for an existing staff member. Only the token hash is stored.</summary>
public class StaffInvitation : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>The role the invited person receives (Manager or Staff, never Owner).</summary>
    public SalonRole Role { get; set; }

    /// <summary>SHA-256 (hex) of the invitation token from the e-mail link.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? InvitedByUserId { get; set; }
}
