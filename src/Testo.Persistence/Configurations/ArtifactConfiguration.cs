using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Execution;

namespace Testo.Persistence.Configurations;

internal sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.HasKey(artifact => artifact.Id);

        builder.Property(artifact => artifact.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(artifact => artifact.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(artifact => artifact.ContentType).HasMaxLength(100).IsRequired();

        // Artifacts are read for one run, and expired by age (90 days, domain-model.md §7).
        builder.HasIndex(artifact => new { artifact.OrganizationId, artifact.TestRunId });
        builder.HasIndex(artifact => new { artifact.OrganizationId, artifact.CapturedAt });

        builder.HasOne<TestRun>()
            .WithMany()
            .HasForeignKey(artifact => new { artifact.OrganizationId, artifact.TestRunId })
            .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
