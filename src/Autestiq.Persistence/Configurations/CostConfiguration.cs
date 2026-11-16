using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Costs;

namespace Autestiq.Persistence.Configurations;

internal sealed class PriceVersionConfiguration : IEntityTypeConfiguration<PriceVersion>
{
    public void Configure(EntityTypeBuilder<PriceVersion> builder)
    {
        builder.HasKey(version => version.Id);
        builder.HasIndex(version => version.Number).IsUnique();
        builder.Property(version => version.Currency).HasMaxLength(3).IsRequired();
        builder.Property(version => version.PublishedBy).HasMaxLength(200).IsRequired();
        builder.Property(version => version.Note).HasMaxLength(1000).IsRequired();

        builder.HasMany(version => version.Prices).WithOne().HasForeignKey(price => price.PriceVersionId).OnDelete(DeleteBehavior.Restrict);

        // The version the product shipped with, so that every database prices runs the same way from the start.
        var first = PriceCatalog.FirstVersion;
        builder.HasData(new { first.Id, first.Number, first.EffectiveFrom, first.Currency, first.PublishedBy, first.Note, first.CreatedAt, first.UpdatedAt });
    }
}

internal sealed class UnitPriceConfiguration : IEntityTypeConfiguration<UnitPrice>
{
    public void Configure(EntityTypeBuilder<UnitPrice> builder)
    {
        builder.HasKey(price => price.Id);
        builder.Property(price => price.Item).HasMaxLength(100).IsRequired();
        builder.Property(price => price.Price).HasPrecision(18, 9);
        builder.HasIndex(price => new { price.PriceVersionId, price.Item }).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("ck_unit_prices_per_positive", "per > 0"));

        builder.HasData(PriceCatalog.FirstPrices.Select(price => new
        {
            price.Id,
            price.PriceVersionId,
            price.Item,
            price.Price,
            price.Per,
            price.Estimate,
            price.CreatedAt,
            price.UpdatedAt,
        }));
    }
}

internal sealed class RunCostConfiguration : IEntityTypeConfiguration<RunCost>
{
    public void Configure(EntityTypeBuilder<RunCost> builder)
    {
        builder.HasKey(cost => cost.Id);
        builder.Property(cost => cost.Item).HasMaxLength(100).IsRequired();
        builder.Property(cost => cost.Price).HasPrecision(18, 9);
        builder.Property(cost => cost.Amount).HasPrecision(18, 6);
        builder.Property(cost => cost.Currency).HasMaxLength(3).IsRequired();
        builder.Property(cost => cost.Period).HasMaxLength(7).IsRequired();
        builder.Property(cost => cost.IdempotencyKey).HasMaxLength(200).IsRequired();

        // Recorded once: the same row written twice is refused by the database, whatever the application does.
        builder.HasIndex(cost => new { cost.OrganizationId, cost.IdempotencyKey }).IsUnique();
        builder.HasIndex(cost => new { cost.OrganizationId, cost.Period });
        builder.HasIndex(cost => cost.RunId);

        builder.HasOne<PriceVersion>().WithMany().HasForeignKey(cost => cost.PriceVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
