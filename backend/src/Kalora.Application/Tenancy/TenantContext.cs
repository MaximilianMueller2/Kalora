namespace Kalora.Application.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    public Guid? SalonId { get; private set; }

    /// <summary>Initializing twice with another salon would let one request mix tenants, so it is refused.</summary>
    public void Initialize(Guid salonId)
    {
        if (SalonId is { } current && current != salonId)
        {
            throw new InvalidOperationException("The tenant of a request cannot be changed once it is set.");
        }

        SalonId = salonId;
    }
}
