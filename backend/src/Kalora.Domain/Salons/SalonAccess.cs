namespace Kalora.Domain.Salons;

/// <summary>Whether a salon may change its data and take online bookings; see <see cref="Salon.AccessAt"/>.</summary>
public enum SalonAccess
{
    Full,
    TrialExpired,
    Suspended,
    Cancelled,
}
