using Kalora.Domain.Common;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kalora.Infrastructure.Persistence;

/// <summary>
/// Write-side of tenant isolation. The query filter protects reads; this guards what gets saved.
/// Without a tenant (registration, background jobs) entities must carry an explicit SalonId.
/// </summary>
internal static class TenantSaveGuard
{
    public static void Enforce(ChangeTracker changeTracker, Guid? tenantId)
    {
        changeTracker.DetectChanges();

        foreach (var entry in changeTracker.Entries<ISalonOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    AssignOrVerifyOnAdd(entry, tenantId);
                    break;
                case EntityState.Modified:
                    // A detached `Update()` marks every property modified, including SalonId, and is refused on purpose:
                    // load the entity (through the filter), change it, save it.
                    if (entry.Property(nameof(ISalonOwned.SalonId)).IsModified)
                    {
                        throw new TenantViolationException($"{entry.Metadata.ShortName()}: SalonId must not be modified.");
                    }

                    VerifyTenant(entry.Entity.SalonId, tenantId, entry.Metadata.ShortName());
                    break;
                case EntityState.Deleted:
                    VerifyTenant(entry.Entity.SalonId, tenantId, entry.Metadata.ShortName());
                    break;
            }
        }

        foreach (var entry in changeTracker.Entries<Salon>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                VerifyTenant(entry.Entity.Id, tenantId, nameof(Salon));
            }
        }
    }

    private static void AssignOrVerifyOnAdd(EntityEntry<ISalonOwned> entry, Guid? tenantId)
    {
        var entity = entry.Entity;
        if (tenantId is { } tenant)
        {
            if (entity.SalonId == Guid.Empty)
            {
                entity.SalonId = tenant;
            }
            else if (entity.SalonId != tenant)
            {
                throw new TenantViolationException($"{entry.Metadata.ShortName()}: SalonId does not match the current tenant.");
            }
        }
        else if (entity.SalonId == Guid.Empty)
        {
            throw new TenantViolationException($"{entry.Metadata.ShortName()}: SalonId is required when no tenant is set.");
        }
    }

    private static void VerifyTenant(Guid entitySalonId, Guid? tenantId, string entityName)
    {
        if (tenantId is { } tenant && entitySalonId != tenant)
        {
            throw new TenantViolationException($"{entityName}: entity belongs to a different salon than the current tenant.");
        }
    }
}
