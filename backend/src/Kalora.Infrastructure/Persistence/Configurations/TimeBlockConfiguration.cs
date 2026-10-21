using Kalora.Domain.Appointments;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class TimeBlockConfiguration : IEntityTypeConfiguration<TimeBlock>
{
    public void Configure(EntityTypeBuilder<TimeBlock> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(b => b.Label).HasMaxLength(200);
        builder.HasOne<StaffMember>().WithMany().HasForeignKey(b => b.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.StaffId, b.StartUtc });
        builder.ToTable(t => t.HasCheckConstraint("ck_time_blocks_end_after_start", "end_utc > start_utc"));
    }
}
