using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
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
    }
}
