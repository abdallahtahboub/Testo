using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Execution;
using Testo.Domain.Projects;

namespace Testo.Persistence.Configurations;

internal sealed class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.HasKey(run => run.Id);

        builder.Property(run => run.ScenarioId).HasMaxLength(100).IsRequired();
        builder.Property(run => run.TargetOrigin).HasMaxLength(500).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(run => run.FailureReason).HasMaxLength(2000);

        // Runs are numbered within a project, which is how people refer to them.
        builder.HasIndex(run => new { run.OrganizationId, run.ProjectId, run.Number }).IsUnique();

        // The dispatcher asks for queued runs, and the run list is read by project, newest first.
        builder.HasIndex(run => new { run.OrganizationId, run.Status });
        builder.HasIndex(run => new { run.OrganizationId, run.ProjectId, run.QueuedAt });

        // Lets steps and artifacts point at (organization_id, run_id) as one foreign key, so a
        // step cannot belong to a run in another organization even if the application is wrong
        // (docs/domain-model.md §4, layer three).
        builder.HasAlternateKey(run => new { run.OrganizationId, run.Id });

        // And the run itself points at its project the same way.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(run => new { run.OrganizationId, run.ProjectId })
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(run => run.Steps)
            .WithOne()
            .HasForeignKey(step => new { step.OrganizationId, step.TestRunId })
            .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // A cancel and a completion can race; whoever saves second must know.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
