using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Usage;

namespace Autestiq.Persistence.Configurations;

internal sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> builder)
    {
        builder.HasKey(usage => usage.Id);

        builder.Property(usage => usage.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(usage => usage.Period).HasMaxLength(7).IsRequired();
        builder.Property(usage => usage.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(usage => usage.Source).HasMaxLength(50).IsRequired();

        // Recorded once: the same event written twice is refused by the database, whatever the application does.
        builder.HasIndex(usage => new { usage.OrganizationId, usage.IdempotencyKey }).IsUnique();

        // A month's total per category, for the quota and the bill.
        builder.HasIndex(usage => new { usage.OrganizationId, usage.Period, usage.Category });
    }
}
