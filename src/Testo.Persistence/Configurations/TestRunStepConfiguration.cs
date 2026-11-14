using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Execution;

namespace Testo.Persistence.Configurations;

internal sealed class TestRunStepConfiguration : IEntityTypeConfiguration<TestRunStep>
{
    public void Configure(EntityTypeBuilder<TestRunStep> builder)
    {
        builder.HasKey(step => step.Id);

        builder.Property(step => step.Description).HasMaxLength(500).IsRequired();
        builder.Property(step => step.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(step => step.Message).HasMaxLength(4000);

        // Steps are read in order, for one run.
        builder.HasIndex(step => new { step.OrganizationId, step.TestRunId, step.Order }).IsUnique();

        // The foreign key to the run is configured from the run's side, as a composite.
    }
}
