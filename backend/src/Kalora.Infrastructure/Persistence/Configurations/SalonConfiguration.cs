using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class SalonConfiguration : IEntityTypeConfiguration<Salon>
{
    public void Configure(EntityTypeBuilder<Salon> builder)
    {
        builder.ConfigureEntity();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Slug).HasMaxLength(63);
        builder.Property(s => s.Industry).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Street).HasMaxLength(200);
        builder.Property(s => s.PostalCode).HasMaxLength(20);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.TimeZone).HasMaxLength(64);
        builder.Property(s => s.DefaultLanguage).HasMaxLength(10);
        builder.Property(s => s.PlanCode).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => s.Slug).IsUnique();
    }
}
