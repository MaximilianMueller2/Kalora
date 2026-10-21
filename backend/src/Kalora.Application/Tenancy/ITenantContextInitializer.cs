namespace Kalora.Application.Tenancy;

/// <summary>Sets the tenant once per request; used by the API pipeline, never by services.</summary>
public interface ITenantContextInitializer
{
    void Initialize(Guid salonId);
}
