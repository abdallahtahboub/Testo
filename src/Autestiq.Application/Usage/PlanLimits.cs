namespace Autestiq.Application.Usage;

/// <summary>
/// The usage settings that are not part of any plan: rate limits that guard against abuse (ADR-0027). What a plan
/// allows is in the plan catalog (ADR-0028), not here.
/// </summary>
public sealed class UsageOptions
{
    /// <summary>The configuration section this is bound from.</summary>
    public const string SectionName = "Usage";

    /// <summary>Starts of runs, re-runs and proof checks one organization may make in a minute.</summary>
    public int StartsPerOrganizationPerMinute { get; set; } = 30;

    /// <summary>Starts one person may make in a minute.</summary>
    public int StartsPerUserPerMinute { get; set; } = 10;
}
