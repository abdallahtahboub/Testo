namespace Autestiq.Application.Testing;

/// <summary>
/// The product's AI planning (Milestone 5, part 2; ADR-0030). The model key is not here: it is server configuration
/// read by the composition root, never stored in the database or shown anywhere.
/// </summary>
public sealed class PlanningOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "Planning";

    /// <summary>The model that plans. Its prices must be in the price book, or planning refuses.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>What planning may cost in a calendar month (UTC) across the whole product, in US dollars.</summary>
    public decimal MonthlyBudgetUsd { get; set; } = 10m;

    /// <summary>What planning may cost one organization in a calendar month (UTC), in US dollars.</summary>
    public decimal OrganizationMonthlyBudgetUsd { get; set; } = 2m;

    /// <summary>How long the planning step waits before looking again when there is nothing to plan.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a test may stay in Planning before it counts as interrupted (Milestone 5.1 R1). Longer than any
    /// planning call can take, retries included, so an answer still on its way is not overtaken.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How often the planning step looks for interrupted tests.</summary>
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromMinutes(1);
}
