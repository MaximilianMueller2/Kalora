using Kalora.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalora.Infrastructure.Persistence.Configurations;

internal sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.ConfigureSalonOwned();
        builder.Property(e => e.ToEmail).HasMaxLength(320);
        builder.Property(e => e.Type).HasMaxLength(100);
        builder.Property(e => e.Language).HasMaxLength(10);
        builder.Property(e => e.Subject).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
    }
}
