using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Common;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Common;

/// <summary>The only way services reach the database; implemented by Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Salon> Salons { get; }

    DbSet<SalonOpeningHours> SalonOpeningHours { get; }

    DbSet<SalonClosure> SalonClosures { get; }

    DbSet<SalonMember> SalonMembers { get; }

    DbSet<StaffMember> StaffMembers { get; }

    DbSet<StaffWorkingHours> StaffWorkingHours { get; }

    DbSet<StaffAbsence> StaffAbsences { get; }

    DbSet<ServiceCategory> ServiceCategories { get; }

    DbSet<Service> Services { get; }

    DbSet<StaffService> StaffServices { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<TimeBlock> TimeBlocks { get; }

    DbSet<EmailOutbox> EmailOutbox { get; }

    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Bypasses tenant isolation: the salons of ALL tenants. Reserved for platform-admin services, slug resolution,
    /// registration and background jobs. An architecture test restricts which files may use it.
    /// </summary>
    IQueryable<Salon> AllSalons();

    /// <summary>Bypasses tenant isolation for <typeparamref name="T"/>. Same restrictions as <see cref="AllSalons"/>.</summary>
    IQueryable<T> AllTenants<T>() where T : class, ISalonOwned;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
