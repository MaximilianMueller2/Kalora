using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Language).HasMaxLength(10);
        builder.Property(a => a.ServiceName).HasMaxLength(200);
        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(a => a.CustomerNote).HasMaxLength(2000);
        builder.Property(a => a.InternalNote).HasMaxLength(2000);
        builder.Property(a => a.ManageTokenHash).HasMaxLength(64);

        // Shadow column kept out of the domain model: the range the exclusion constraint operates on.
        builder.Property<NpgsqlRange<DateTime>>("BlockedRange")
            .HasColumnType("tstzrange")
            .HasComputedColumnSql("tstzrange(blocked_start_utc, blocked_end_utc, '[)')", stored: true);

        builder.HasOne<StaffMember>().WithMany().HasForeignKey(a => a.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Service>().WithMany().HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SalonId, a.StartUtc });
        builder.HasIndex(a => new { a.StaffId, a.StartUtc });
        builder.HasIndex(a => a.CustomerId);
        builder.HasIndex(a => a.ServiceId);
        builder.HasIndex(a => a.ManageTokenHash).IsUnique();
        builder.HasIndex(a => new { a.Status, a.ExpiresAtUtc });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_appointments_times", "end_utc > start_utc AND blocked_start_utc <= start_utc AND blocked_end_utc >= end_utc");
            t.HasCheckConstraint("ck_appointments_price_not_negative", "price_cents >= 0");
        });
    }
}
