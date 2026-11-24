using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Testing;

namespace Autestiq.Persistence.Configurations;

internal sealed class PlanningAttemptConfiguration : IEntityTypeConfiguration<PlanningAttempt>
{
    public void Configure(EntityTypeBuilder<PlanningAttempt> builder)
    {
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Model).HasMaxLength(100).IsRequired();
        builder.Property(attempt => attempt.CeilingUsd).HasPrecision(18, 6);

        // The budgets add up a month's attempts across organizations; a test's attempts are looked up by test.
        builder.HasIndex(attempt => attempt.CreatedAt);
        builder.HasIndex(attempt => new { attempt.OrganizationId, attempt.TestCaseId });

        // Through the test's own organization, so an attempt cannot be filed under another organization's test.
        builder.HasOne<TestCase>()
            .WithMany()
            .HasForeignKey(attempt => new { attempt.OrganizationId, attempt.TestCaseId })
            .HasPrincipalKey(testCase => new { testCase.OrganizationId, testCase.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanningAttemptOutcomeConfiguration : IEntityTypeConfiguration<PlanningAttemptOutcome>
{
    public void Configure(EntityTypeBuilder<PlanningAttemptOutcome> builder)
    {
        builder.HasKey(outcome => outcome.Id);
        builder.Property(outcome => outcome.End).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(outcome => outcome.CostUsd).HasPrecision(18, 6);

        // An attempt ends once, whatever the application does.
        builder.HasIndex(outcome => outcome.AttemptId).IsUnique();

        builder.HasOne<PlanningAttempt>()
            .WithMany()
            .HasForeignKey(outcome => new { outcome.OrganizationId, outcome.AttemptId })
            .HasPrincipalKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
