namespace Autestiq.Domain.Plans;

/// <summary>
/// The plans and versions the product shipped with, by fixed id, so the migration that seeds them and the code that
/// refers to them agree. Figures confirmed by the owner on 2026-11-16 (ADR-0027, ADR-0028); not published, and no
/// prices until cost is measured (Milestone 3.8) and billing exists (Phase D).
/// </summary>
public static class PlanCatalog
{
    /// <summary>When the first versions took effect.</summary>
    public static readonly DateTimeOffset Launched = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    public static readonly Guid Developer = new("01a1e500-0000-7000-8000-000000000001");
    public static readonly Guid Starter = new("01a1e500-0000-7000-8000-000000000002");
    public static readonly Guid Growth = new("01a1e500-0000-7000-8000-000000000003");
    public static readonly Guid Scale = new("01a1e500-0000-7000-8000-000000000004");

    public static readonly Guid DeveloperV1 = new("01a1e500-0000-7000-8000-000000000101");
    public static readonly Guid StarterV1 = new("01a1e500-0000-7000-8000-000000000102");
    public static readonly Guid GrowthV1 = new("01a1e500-0000-7000-8000-000000000103");
    public static readonly Guid ScaleV1 = new("01a1e500-0000-7000-8000-000000000104");

    /// <summary>
    /// The version a new organization starts on. Changed on purpose when a new Developer version is published, so
    /// that which version new customers get is a decision, not a side effect; a test fails if a newer Developer
    /// version exists and this still names an older one.
    /// </summary>
    public static readonly Guid StartingVersion = DeveloperV1;

    /// <summary>The plans the product shipped with.</summary>
    public static IReadOnlyList<Plan> Plans { get; } =
    [
        new() { Id = Developer, Code = "developer", Name = "Developer", CreatedAt = Launched, UpdatedAt = Launched },
        new() { Id = Starter, Code = "starter", Name = "Starter", CreatedAt = Launched, UpdatedAt = Launched },
        new() { Id = Growth, Code = "growth", Name = "Growth", CreatedAt = Launched, UpdatedAt = Launched },
        new() { Id = Scale, Code = "scale", Name = "Scale", CreatedAt = Launched, UpdatedAt = Launched },
    ];

    /// <summary>Their first versions. The project limits are the business model's; Developer's single project is the owner's.</summary>
    public static IReadOnlyList<PlanVersion> FirstVersions { get; } =
    [
        Version(DeveloperV1, Developer, minutes: 100, concurrent: 1, projects: 1),
        Version(StarterV1, Starter, minutes: 500, concurrent: 2, projects: 3),
        Version(GrowthV1, Growth, minutes: 2_500, concurrent: 5, projects: 10),
        Version(ScaleV1, Scale, minutes: 10_000, concurrent: 10, projects: null),
    ];

    private static PlanVersion Version(Guid id, Guid plan, long minutes, int concurrent, int? projects) => new()
    {
        Id = id,
        PlanId = plan,
        Number = 1,
        EffectiveFrom = Launched,
        MonthlyBrowserMinutes = minutes,
        ConcurrentRuns = concurrent,
        MaxProjects = projects,
        CreatedAt = Launched,
        UpdatedAt = Launched,
    };
}
