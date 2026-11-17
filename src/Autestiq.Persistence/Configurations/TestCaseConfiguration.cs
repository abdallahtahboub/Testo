using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Projects;
using Autestiq.Domain.Testing;

namespace Autestiq.Persistence.Configurations;

internal sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.HasKey(testCase => testCase.Id);

        builder.Property(testCase => testCase.Description).HasMaxLength(1000).IsRequired();
        builder.Property(testCase => testCase.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(testCase => testCase.FailureReason).HasMaxLength(200);
        builder.Property(testCase => testCase.Model).HasMaxLength(100);
        builder.Property(testCase => testCase.ModelCost).HasPrecision(18, 6);

        builder.HasIndex(testCase => new { testCase.OrganizationId, testCase.ProjectId, testCase.Number }).IsUnique();

        // The planning step looks for these across organizations; the budgets add up a month's cost.
        builder.HasIndex(testCase => new { testCase.Status, testCase.PlanningStartedAt });
        builder.HasIndex(testCase => new { testCase.OrganizationId, testCase.PlannedAt });

        // Through the project's (organization_id, id) key, so a test cannot belong to another organization's project.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(testCase => new { testCase.OrganizationId, testCase.ProjectId })
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // An environment with tests is not removed from under them.
        builder.HasOne<TestEnvironment>()
            .WithMany()
            .HasForeignKey(testCase => testCase.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
