using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class StaffAbsenceConfiguration : IEntityTypeConfiguration<StaffAbsence>
{
    public void Configure(EntityTypeBuilder<StaffAbsence> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<StaffMember>().WithMany().HasForeignKey(a => a.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.StaffId, a.StartUtc });
        builder.ToTable(t => t.HasCheckConstraint("ck_staff_absences_end_after_start", "end_utc > start_utc"));
    }
}
