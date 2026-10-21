using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class StaffWorkingHoursConfiguration : IEntityTypeConfiguration<StaffWorkingHours>
{
    public void Configure(EntityTypeBuilder<StaffWorkingHours> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(h => h.Weekday).HasConversion<short>();
        builder.HasOne<StaffMember>().WithMany().HasForeignKey(h => h.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(h => new { h.StaffId, h.Weekday, h.ValidFrom });
        builder.ToTable(t => t.HasCheckConstraint("ck_staff_working_hours_end_after_start", "\"end\" > \"start\""));
    }
}
