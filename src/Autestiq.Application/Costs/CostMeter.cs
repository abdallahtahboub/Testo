using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Costs;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Usage;
using Autestiq.Persistence;

namespace Autestiq.Application.Costs;

/// <summary>
/// What a run cost us (Milestone 3.8, ADR-0029): each measured quantity at the price in force when the run finished.
/// One place for the rules, so the cost rows, a run's total and the report all agree.
/// </summary>
public static class CostMeter
{
    /// <summary>
    /// The price version in force at an instant: the one with the latest <see cref="PriceVersion.EffectiveFrom"/> not
    /// after it, with its prices. <c>null</c> before the first version took effect.
    /// </summary>
    public static Task<PriceVersion?> InForceAsync(AutestiqDbContext database, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        return database.PriceVersions
            .Include(version => version.Prices)
            .Where(version => version.EffectiveFrom <= at)
            .OrderByDescending(version => version.EffectiveFrom)
            .ThenByDescending(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The cost rows for a run's usage, one per item the price version prices. A quantity it has no price for is left
    /// out and named in <paramref name="unpriced"/>, so that a missing price shows up instead of costing nothing.
    /// </summary>
    public static IReadOnlyList<RunCost> ForRun(TestRun run, IEnumerable<UsageEvent> usage, PriceVersion prices, ICollection<string>? unpriced = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(prices);

        var rows = new List<RunCost>();

        foreach (var used in usage)
        {
            if (ItemFor(used.Category) is not { } item)
            {
                continue;
            }

            if (prices.Prices.FirstOrDefault(price => price.Item == item) is not { } price)
            {
                unpriced?.Add(item);
                continue;
            }

            rows.Add(new RunCost
            {
                OrganizationId = run.OrganizationId,
                RunId = run.Id,
                Item = item,
                Quantity = used.Quantity,
                Price = price.Price,
                Per = price.Per,
                Amount = price.CostOf(used.Quantity),
                Currency = prices.Currency,
                PriceVersionId = prices.Id,
                Estimate = price.Estimate,
                Billable = used.Billable,
                Period = used.Period,
                IdempotencyKey = KeyFor(run.Id, item),
            });
        }

        return rows;
    }

    /// <summary>What a usage category is priced as, or <c>null</c> for one that costs nothing in itself, such as a run.</summary>
    public static string? ItemFor(UsageCategory category) => category switch
    {
        UsageCategory.BrowserSeconds => CostItems.BrowserSecond,

        // Evidence is priced for one month of keeping it, until retention (gap G12) says how long it is kept.
        UsageCategory.ArtifactBytes => CostItems.StorageByteMonth,
        _ => null,
    };

    /// <summary>What makes a run's cost of one item the same row however often it is recorded.</summary>
    public static string KeyFor(Guid runId, string item) =>
        string.Create(CultureInfo.InvariantCulture, $"run:{runId:N}:cost:{item}");
}
