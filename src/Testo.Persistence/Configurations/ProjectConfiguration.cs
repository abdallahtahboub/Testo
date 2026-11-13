using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Projects;

namespace Testo.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.KeyPrefix).HasMaxLength(10).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(2000);
        builder.Property(project => project.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Two projects in one organization cannot hand out the same keys.
        builder.HasIndex(project => new { project.OrganizationId, project.KeyPrefix }).IsUnique();

        builder.HasIndex(project => new { project.OrganizationId, project.Status });

        // Lets the entities of Milestone 5 carry (organization_id, project_id) as one foreign key.
        builder.HasAlternateKey(project => new { project.OrganizationId, project.Id });

        builder.HasOne<Testo.Domain.Identity.Organization>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
