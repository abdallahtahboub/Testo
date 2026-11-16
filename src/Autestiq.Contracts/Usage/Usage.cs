namespace Autestiq.Contracts.Usage;

/// <summary>What a usage event measures (Milestone 3.6). Each has one fixed unit.</summary>
public enum UsageCategory
{
    /// <summary>Seconds a browser ran for a run. Recorded exactly; a month's total is rounded up to a minute once.</summary>
    BrowserSeconds = 1,

    /// <summary>One run. Billable only when it reached the customer's application and did not end by our fault.</summary>
    Run = 2,

    /// <summary>Bytes of evidence kept for a run, such as screenshots.</summary>
    ArtifactBytes = 3,

    /// <summary>Model tokens spent planning. Recorded once the product calls a model.</summary>
    ModelTokens = 4,
}

/// <summary>An organization's use of one month, against what its plan allows.</summary>
/// <param name="Month">The month, as <c>yyyy-MM</c> in UTC.</param>
/// <param name="BrowserMinutesUsed">Billable browser time, rounded up to the minute once for the whole month.</param>
/// <param name="BrowserMinutesAllowed">The plan's monthly allowance.</param>
/// <param name="BillableRuns">Runs that reached the customer's application and did not end by our fault.</param>
/// <param name="ConcurrentRunsAllowed">How many runs the plan lets run at once.</param>
public sealed record OrganizationUsage(
    string Month,
    long BrowserMinutesUsed,
    long BrowserMinutesAllowed,
    long BillableRuns,
    int ConcurrentRunsAllowed);

/// <summary>Reading an organization's usage. Requires <see cref="Identity.Permission.Read"/>.</summary>
public interface IUsageService
{
    /// <summary>The current month's usage of the organization in context, or <c>null</c> without permission.</summary>
    Task<OrganizationUsage?> GetCurrentMonthAsync(CancellationToken cancellationToken = default);
}
