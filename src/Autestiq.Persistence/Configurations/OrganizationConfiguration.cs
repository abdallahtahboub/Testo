using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Identity;

namespace Autestiq.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        builder.Property(organization => organization.Slug).HasMaxLength(60).IsRequired();
        builder.Property(organization => organization.DataRegion).HasMaxLength(40).IsRequired();
        builder.Property(organization => organization.Status).HasConversion<string>().HasMaxLength(20);

        // The slug appears in URLs, so it has to identify exactly one organization.
        builder.HasIndex(organization => organization.Slug).IsUnique();

        // Lets later entities carry a foreign key of (organization_id, parent_id), so the database
        // itself refuses a row whose parent belongs to another customer (domain-model.md §4).
        builder.HasAlternateKey(organization => organization.Id);

        // Every organization is on a version that exists, and a version anyone is on cannot be removed.
        builder.HasOne<Domain.Plans.PlanVersion>()
            .WithMany()
            .HasForeignKey(organization => organization.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
