using Kalora.Domain.Common;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal static class EntityTypeBuilderExtensions
{
    public static void ConfigureEntity<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }

    public static void ConfigureSalonOwned<T>(this EntityTypeBuilder<T> builder) where T : Entity, ISalonOwned
    {
        builder.ConfigureEntity();
        builder.HasOne<Salon>().WithMany().HasForeignKey(e => e.SalonId).OnDelete(DeleteBehavior.Restrict);
    }
}
