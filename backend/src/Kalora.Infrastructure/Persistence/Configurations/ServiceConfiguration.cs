using Kalora.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.Currency).HasMaxLength(3).IsFixedLength();
        builder.HasOne<ServiceCategory>().WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.SalonId, s.CategoryId, s.SortOrder });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_services_price_not_negative", "price_cents >= 0");
            t.HasCheckConstraint("ck_services_duration_positive", "duration_minutes > 0");
            t.HasCheckConstraint("ck_services_buffers_not_negative", "buffer_before_minutes >= 0 AND buffer_after_minutes >= 0");
        });
    }
}
