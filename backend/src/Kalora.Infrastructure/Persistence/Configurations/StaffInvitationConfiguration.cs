using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(i => i.Email).HasMaxLength(320);
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TokenHash).HasMaxLength(64);
        builder.HasOne<StaffMember>().WithMany().HasForeignKey(i => i.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => i.StaffId);
    }
}
