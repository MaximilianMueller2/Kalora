using Kalora.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.HasIndex(c => new { c.SalonId, c.SortOrder });
    }
}
