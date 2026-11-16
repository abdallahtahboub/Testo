using Microsoft.EntityFrameworkCore;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Usage;
using Autestiq.Persistence;

namespace Autestiq.Application.Costs;

/// <summary>One organization's month, or the whole product's, in a cost report.</summary>
/// <param name="Organization">The organization's slug, or <c>null</c> for the total.</param>
/// <param name="Runs">Runs whose result was written back that month.</param>
/// <param name="BillableRuns">Of those, the runs that are billable.</param>
/// <param name="BillableMinutes">Billable browser time, rounded up to the minute once for the month.</param>
/// <param name="Cost">What those runs cost us, in <paramref name="Currency"/>.</param>
/// <param name="OurFailuresCost">The part of it spent on runs that are not billable: ours to carry.</param>
/// <param name="Currency">The currency of the costs.</param>
/// <param name="FromEstimates">Whether any of the cost was priced with an estimate.</param>
public sealed record CostLine(
    string? Organization,
    int Runs,
    int BillableRuns,
    long BillableMinutes,
    decimal Cost,
    decimal OurFailuresCost,
    string Currency,
    bool FromEstimates)
{
    /// <summary>Cost per run, or <c>null</c> without runs.</summary>
    public decimal? CostPerRun => Runs == 0 ? null : Math.Round(Cost / Runs, 6);

    /// <summary>Cost per billable minute, all cost included, or <c>null</c> without billable minutes.</summary>
    public decimal? CostPerBillableMinute => BillableMinutes == 0 ? null : Math.Round(Cost / BillableMinutes, 6);

    /// <summary>The share of the cost spent on our own failures, from 0 to 1, or <c>null</c> without cost.</summary>
    public decimal? OurFailuresShare => Cost == 0 ? null : Math.Round(OurFailuresCost / Cost, 4);
}

/// <summary>
/// A month's cost, per organization and in total (Milestone 3.8, ADR-0029): the evidence for setting plan prices
/// against the business model's margin target. For operators; it reads across every organization.
/// </summary>
public static class CostReport
{
    /// <summary>The month's lines, one per organization with any cost or usage, then the total.</summary>
    public static async Task<IReadOnlyList<CostLine>> ForMonthAsync(AutestiqDbContext database, string period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // An operator's report reads every organization's usage and cost: that is what it is for.
        var usage = await database.UsageEvents.IgnoreQueryFilters()
            .Where(used => used.Period == period && (used.Category == UsageCategory.Run || used.Category == UsageCategory.BrowserSeconds))
            .Select(used => new { used.OrganizationId, used.Category, used.Quantity, used.Billable })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // As above: across every organization, on purpose.
        var costs = await database.RunCosts.IgnoreQueryFilters()
            .Where(cost => cost.Period == period)
            .Select(cost => new { cost.OrganizationId, cost.Amount, cost.Billable, cost.Estimate, cost.Currency })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var organizations = usage.Select(used => used.OrganizationId).Concat(costs.Select(cost => cost.OrganizationId)).Distinct().ToList();

        var slugs = await database.Organizations
            .Where(organization => organizations.Contains(organization.Id))
            .ToDictionaryAsync(organization => organization.Id, organization => organization.Slug, cancellationToken)
            .ConfigureAwait(false);

        var currency = costs.Select(cost => cost.Currency).FirstOrDefault() ?? Domain.Costs.PriceCatalog.Currency;

        if (costs.Any(cost => cost.Currency != currency))
        {
            throw new InvalidOperationException($"The month {period} holds costs in more than one currency; they cannot be added up.");
        }

        CostLine LineFor(string? name, Func<Guid, bool> includes)
        {
            var used = usage.Where(row => includes(row.OrganizationId)).ToList();
            var cost = costs.Where(row => includes(row.OrganizationId)).ToList();

            return new CostLine(
                name,
                Runs: (int)used.Where(row => row.Category == UsageCategory.Run).Sum(row => row.Quantity),
                BillableRuns: (int)used.Where(row => row.Category == UsageCategory.Run && row.Billable).Sum(row => row.Quantity),
                BillableMinutes: UsageMeter.MinutesFor(used.Where(row => row.Category == UsageCategory.BrowserSeconds && row.Billable).Sum(row => row.Quantity)),
                Cost: cost.Sum(row => row.Amount),
                OurFailuresCost: cost.Where(row => !row.Billable).Sum(row => row.Amount),
                Currency: currency,
                FromEstimates: cost.Any(row => row.Estimate));
        }

        var lines = organizations
            .Select(id => LineFor(slugs.GetValueOrDefault(id, id.ToString()), organization => organization == id))
            .OrderByDescending(line => line.Cost)
            .ThenBy(line => line.Organization, StringComparer.Ordinal)
            .ToList();

        // Each organization's month is rounded up once, as its plan counts it; the total is the sum of those.
        lines.Add(LineFor(null, _ => true) with { BillableMinutes = lines.Sum(line => line.BillableMinutes) });
        return lines;
    }
}
