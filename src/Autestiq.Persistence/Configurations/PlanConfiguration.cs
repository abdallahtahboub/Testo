using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Plans;

namespace Autestiq.Persistence.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Code).HasMaxLength(40).IsRequired();
        builder.Property(plan => plan.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(plan => plan.Code).IsUnique();

        builder.HasMany(plan => plan.Versions).WithOne().HasForeignKey(version => version.PlanId).OnDelete(DeleteBehavior.Restrict);

        // The plans the product shipped with, so that every database has the same catalog and the code can name them.
        builder.HasData(PlanCatalog.Plans.Select(plan => new { plan.Id, plan.Code, plan.Name, plan.CreatedAt, plan.UpdatedAt }));
    }
}

internal sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.HasKey(version => version.Id);
        builder.HasIndex(version => new { version.PlanId, version.Number }).IsUnique();

        builder.HasData(PlanCatalog.FirstVersions.Select(version => new
        {
            version.Id,
            version.PlanId,
            version.Number,
            version.EffectiveFrom,
            version.MonthlyBrowserMinutes,
            version.ConcurrentRuns,
            version.MaxProjects,
            version.CreatedAt,
            version.UpdatedAt,
        }));
    }
}
