using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(s => s.DisplayName).HasMaxLength(100);
        builder.Property(s => s.Color).HasMaxLength(7);
        builder.HasIndex(s => new { s.SalonId, s.SortOrder });
    }
}
