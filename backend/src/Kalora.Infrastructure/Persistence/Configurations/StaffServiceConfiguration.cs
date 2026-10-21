using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class StaffServiceConfiguration : IEntityTypeConfiguration<StaffService>
{
    public void Configure(EntityTypeBuilder<StaffService> builder)
    {
        builder.ConfigureSalonOwned();
        builder.HasOne<StaffMember>().WithMany().HasForeignKey(s => s.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Service>().WithMany().HasForeignKey(s => s.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.StaffId, s.ServiceId }).IsUnique();
        builder.HasIndex(s => s.ServiceId);
        builder.ToTable(t => t.HasCheckConstraint("ck_staff_services_duration_positive", "duration_override_minutes IS NULL OR duration_override_minutes > 0"));
    }
}
