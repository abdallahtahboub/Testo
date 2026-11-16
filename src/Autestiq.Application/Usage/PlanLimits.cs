namespace Autestiq.Application.Usage;

/// <summary>What one plan allows (Milestone 3.6). From configuration until the plan catalog exists (Milestone 3.7).</summary>
public sealed class PlanLimit
{
    /// <summary>Billable browser minutes a month. When they are used up, new runs are refused until the next month.</summary>
    public long MonthlyBrowserMinutes { get; set; }

    /// <summary>How many of the organization's runs may be carried out at once.</summary>
    public int ConcurrentRuns { get; set; }
}

/// <summary>
/// The limits of every plan, by the name an organization's <c>Plan</c> holds. Not published: the figures are the
/// business model's hypotheses (docs/business/BUSINESS_MODEL.md), confirmed by the owner for configuration on
/// 2026-11-16, and the concurrency figures are proposed, not measured.
/// </summary>
public sealed class UsageOptions
{
    /// <summary>The configuration section this is bound from.</summary>
    public const string SectionName = "Usage";

    /// <summary>The plans, by lower-case name.</summary>
    public Dictionary<string, PlanLimit> Plans { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["developer"] = new() { MonthlyBrowserMinutes = 100, ConcurrentRuns = 1 },
        ["starter"] = new() { MonthlyBrowserMinutes = 500, ConcurrentRuns = 2 },
        ["growth"] = new() { MonthlyBrowserMinutes = 2_500, ConcurrentRuns = 5 },
        ["scale"] = new() { MonthlyBrowserMinutes = 10_000, ConcurrentRuns = 10 },
    };

    /// <summary>Plan names that mean another: every organization starts on "trial", which is the Developer plan.</summary>
    public Dictionary<string, string> Aliases { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["trial"] = "developer",
    };

    /// <summary>Starts of runs, re-runs and proof checks one organization may make in a minute.</summary>
    public int StartsPerOrganizationPerMinute { get; set; } = 30;

    /// <summary>Starts one person may make in a minute.</summary>
    public int StartsPerUserPerMinute { get; set; } = 10;

    /// <summary>
    /// The limits of a plan by the name an organization holds. An unknown name gets the smallest plan, never an
    /// unlimited one: a typo in a plan name must not become free use.
    /// </summary>
    public PlanLimit For(string? plan)
    {
        var name = plan is not null && Aliases.TryGetValue(plan, out var alias) ? alias : plan;

        if (name is not null && Plans.TryGetValue(name, out var limit))
        {
            return limit;
        }

        return Plans.Values.OrderBy(candidate => candidate.MonthlyBrowserMinutes).FirstOrDefault()
            ?? new PlanLimit { MonthlyBrowserMinutes = 0, ConcurrentRuns = 0 };
    }
}
