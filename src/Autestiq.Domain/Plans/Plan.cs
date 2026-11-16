using Autestiq.Domain.Common;

namespace Autestiq.Domain.Plans;

/// <summary>
/// A plan a customer can be on, such as Growth (Milestone 3.7). What it allows lives in its versions; the plan
/// itself is only a code and a name. Shared by every organization, outside the tenant boundary.
/// </summary>
public sealed class Plan : Entity
{
    /// <summary>The plan's code, lower case, never changed: <c>developer</c>, <c>starter</c>, <c>growth</c>, <c>scale</c>.</summary>
    public required string Code { get; set; }

    /// <summary>The plan's name as it is shown.</summary>
    public required string Name { get; set; }

    /// <summary>Its versions, the oldest first.</summary>
    public ICollection<PlanVersion> Versions { get; } = [];
}

/// <summary>
/// What a plan allows, from a date (Milestone 3.7, ADR-0028). Written once and never changed: a change to a plan is
/// a new version, and an organization keeps the version it holds until an operator moves it.
/// </summary>
public sealed class PlanVersion : Entity, IAppendOnly
{
    /// <summary>The plan it is a version of.</summary>
    public Guid PlanId { get; set; }

    /// <summary>1 for the first version of a plan, then 2, 3 and on.</summary>
    public int Number { get; set; }

    /// <summary>From when an organization may be moved onto it.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Billable browser minutes a month. When they are used up, new runs are refused until the next month.</summary>
    public long MonthlyBrowserMinutes { get; set; }

    /// <summary>How many of the organization's runs may be carried out at once.</summary>
    public int ConcurrentRuns { get; set; }

    /// <summary>How many projects the organization may have; <c>null</c> for no limit.</summary>
    public int? MaxProjects { get; set; }
}
