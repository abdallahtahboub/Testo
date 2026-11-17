using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Projects;

namespace Autestiq.Persistence.Configurations;

internal sealed class TestEnvironmentConfiguration : IEntityTypeConfiguration<TestEnvironment>
{
    public void Configure(EntityTypeBuilder<TestEnvironment> builder)
    {
        builder.ToTable("environments");
        builder.HasKey(environment => environment.Id);

        builder.Property(environment => environment.Name).HasMaxLength(100).IsRequired();
        builder.Property(environment => environment.Origin).HasMaxLength(500).IsRequired();

        // One project cannot name two environments alike, nor list one site twice.
        builder.HasIndex(environment => new { environment.OrganizationId, environment.ProjectId, environment.Name }).IsUnique();
        builder.HasIndex(environment => new { environment.OrganizationId, environment.ProjectId, environment.Origin }).IsUnique();

        // Through the project's (organization_id, id) key, so an environment cannot belong to another organization's project.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(environment => new { environment.OrganizationId, environment.ProjectId })
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
