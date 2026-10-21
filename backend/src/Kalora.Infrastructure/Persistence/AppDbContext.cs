using Kalora.Application.Common;
using Kalora.Application.Tenancy;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Common;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options), IAppDbContext
{
    private const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplySalonOwnedMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySalonOwned), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>Read by the query filters; EF turns it into a per-context parameter, so the filter follows each request's tenant.</summary>
    private Guid? CurrentSalonId => tenantContext.SalonId;

    public DbSet<Salon> Salons => Set<Salon>();

    public DbSet<SalonOpeningHours> SalonOpeningHours => Set<SalonOpeningHours>();

    public DbSet<SalonClosure> SalonClosures => Set<SalonClosure>();

    public DbSet<SalonMember> SalonMembers => Set<SalonMember>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<StaffWorkingHours> StaffWorkingHours => Set<StaffWorkingHours>();

    public DbSet<StaffAbsence> StaffAbsences => Set<StaffAbsence>();

    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<StaffService> StaffServices => Set<StaffService>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<TimeBlock> TimeBlocks => Set<TimeBlock>();

    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyTenantIsolation(modelBuilder);
    }

    public IQueryable<Salon> AllSalons() => Salons.IgnoreQueryFilters([TenantFilter]);

    public IQueryable<T> AllTenants<T>() where T : class, ISalonOwned => Set<T>().IgnoreQueryFilters([TenantFilter]);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TenantSaveGuard.Enforce(ChangeTracker, tenantContext.SalonId);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TenantSaveGuard.Enforce(ChangeTracker, tenantContext.SalonId);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantIsolation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Salon>().HasQueryFilter(TenantFilter, s => s.Id == CurrentSalonId);

        var salonOwnedTypes = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => typeof(ISalonOwned).IsAssignableFrom(type))
            .ToList();
        foreach (var type in salonOwnedTypes)
        {
            ApplySalonOwnedMethod.MakeGenericMethod(type).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplySalonOwned<T>(ModelBuilder modelBuilder) where T : class, ISalonOwned
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasQueryFilter(TenantFilter, e => e.SalonId == CurrentSalonId);

        // UPDATE/DELETE statements also match on salon_id, so a forged entity with a foreign ID affects no row.
        entity.Property(nameof(ISalonOwned.SalonId)).IsConcurrencyToken();
    }
}
