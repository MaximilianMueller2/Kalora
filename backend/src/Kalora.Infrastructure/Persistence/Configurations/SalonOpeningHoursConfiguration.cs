using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class SalonOpeningHoursConfiguration : IEntityTypeConfiguration<SalonOpeningHours>
{
    public void Configure(EntityTypeBuilder<SalonOpeningHours> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(h => h.Weekday).HasConversion<short>();
        builder.HasIndex(h => new { h.SalonId, h.Weekday });
        builder.ToTable(t => t.HasCheckConstraint("ck_salon_opening_hours_end_after_start", "\"end\" > \"start\""));
    }
}
