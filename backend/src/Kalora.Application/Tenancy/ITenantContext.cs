namespace Kalora.Application.Tenancy;

/// <summary>The salon (tenant) the current request acts for. Null means "no tenant": queries then return no salon data.</summary>
public interface ITenantContext
{
    Guid? SalonId { get; }
}
