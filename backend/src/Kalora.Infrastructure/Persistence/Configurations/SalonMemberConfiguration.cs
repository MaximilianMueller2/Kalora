using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class SalonMemberConfiguration : IEntityTypeConfiguration<SalonMember>
{
    public void Configure(EntityTypeBuilder<SalonMember> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => new { m.SalonId, m.UserId }).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        // A user belongs to exactly one salon (see ADR 0002).
        builder.HasIndex(m => m.UserId).IsUnique();
    }
}
