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
        builder.HasIndex(m => m.UserId);
    }
}
