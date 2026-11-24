using Microsoft.EntityFrameworkCore;
using Autestiq.Domain.Costs;
using Autestiq.Persistence;

namespace Autestiq.Application.Testing;

/// <summary>
/// What AI planning has cost this month, by its attempts (Milestone 5.1 R1), for the product and for one organization, and what a model's tokens cost
/// now (ADR-0030). Checked before every planning call, so the budgets hold before money is spent, not after.
/// </summary>
public static class AiBudget
{
    /// <summary>The start of the calendar month an instant falls in, in UTC.</summary>
    public static DateTimeOffset MonthStart(DateTimeOffset now) => new(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// What is left of this month's budgets: the smaller of the product's and the organization's, never below zero.
    /// </summary>
    public static async Task<decimal> LeftAsync(AutestiqDbContext database, Guid organizationId, PlanningOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var from = MonthStart(now);

        // Across every organization on purpose: the product's budget is the product's, whoever spent it. Every attempt
        // counts, re-plans included, and one whose outcome is not recorded counts at its ceiling: it may have been
        // billed (Milestone 5.1 R1, R-17).
        var spent = await database.PlanningAttempts.IgnoreQueryFilters()
            .Where(attempt => attempt.CreatedAt >= from)
            .Select(attempt => new
            {
                Organization = attempt.OrganizationId,
                Cost = database.PlanningAttemptOutcomes.IgnoreQueryFilters()
                    .Where(outcome => outcome.AttemptId == attempt.Id)
                    .Select(outcome => (decimal?)outcome.CostUsd)
                    .FirstOrDefault() ?? attempt.CeilingUsd,
            })
            .GroupBy(row => row.Organization)
            .Select(group => new { Organization = group.Key, Cost = group.Sum(row => row.Cost) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var product = options.MonthlyBudgetUsd - spent.Sum(row => row.Cost);
        var organization = options.OrganizationMonthlyBudgetUsd - spent.Where(row => row.Organization == organizationId).Sum(row => row.Cost);

        return Math.Max(0m, Math.Min(product, organization));
    }

    /// <summary>A model's prices per million input and output tokens in the price version in force, or <c>null</c> without one.</summary>
    public static async Task<(decimal Input, decimal Output)?> PricesAsync(AutestiqDbContext database, string model, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var version = await Costs.CostMeter.InForceAsync(database, now, cancellationToken).ConfigureAwait(false);

        var input = version?.Prices.FirstOrDefault(price => price.Item == CostItems.ModelInputToken(model));
        var output = version?.Prices.FirstOrDefault(price => price.Item == CostItems.ModelOutputToken(model));

        return input is null || output is null
            ? null
            : (input.Price * PriceCatalog.TokensPerMillion / input.Per, output.Price * PriceCatalog.TokensPerMillion / output.Per);
    }
}
