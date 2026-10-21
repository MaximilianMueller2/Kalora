using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class SalonClosureConfiguration : IEntityTypeConfiguration<SalonClosure>
{
    public void Configure(EntityTypeBuilder<SalonClosure> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(c => c.Reason).HasMaxLength(500);
        builder.HasIndex(c => new { c.SalonId, c.Date }).IsUnique();
    }
}
