using Microsoft.Extensions.Logging;
using Testo.Contracts.Execution;
using Testo.Contracts.Planning;

namespace Testo.Application.Planning;

/// <summary>
/// Asks a planner for a plan, judges it, and asks once more if it was wrong. The whole of what the
/// application does with a model.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small, and deliberately the only place a model is spoken to. Everything that keeps
/// a plan safe is either in the type (ADR-0019 §2) or in <see cref="PlanValidator"/>, and this
/// class exists to make sure neither can be skipped: there is no path from a provider's answer to
/// a browser that does not come through here.
/// </para>
/// <para>
/// One retry, no more. A model given its own validation errors usually fixes them; a model that
/// fails twice is not going to be argued into it, and each attempt is money. Both attempts are
/// charged to the run's budget, which is why the cost is carried even when the plan is refused.
/// </para>
/// </remarks>
public sealed class PlanningService(IScenarioPlanner planner, ILogger<PlanningService> logger)
{
    /// <summary>How many times a planner is asked for the same plan, including the first.</summary>
    public const int MaxAttempts = 2;

    /// <summary>
    /// Plans the scenario against the application as a crawl found it.
    /// </summary>
    /// <param name="scenario">What the person wants tested, in their words.</param>
    /// <param name="site">What the crawl found. Untrusted input.</param>
    /// <param name="budget">The run's budget. Tokens and money are spent here; the rest is not.</param>
    /// <param name="secretNames">The names of the values this run holds, so a plan can say "the password".</param>
    /// <param name="cancellationToken">The usual.</param>
    public async Task<PlannedResult> PlanAsync(
        string scenario,
        SiteSnapshot site,
        RunBudget budget,
        IReadOnlyList<string> secretNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(budget);

        var request = PlanRequest.First(scenario, site, secretNames, budget.MaxSteps, budget.MaxModelTokens);
        var spent = PlanningCost.None;
        IReadOnlyList<string> errors = [PlanningErrors.NoPlanner];
        IReadOnlyList<string> notes = [];

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var answer = await planner.PlanAsync(request, cancellationToken).ConfigureAwait(false);
            spent += answer.Cost;

            if (!answer.Succeeded)
            {
                // The provider itself, not the plan: nothing to re-ask about, so stop.
                PlanningLog.ProviderRefused(logger, planner.Model, string.Join(", ", answer.Errors));
                return new PlannedResult(null, answer.Errors, [], spent, planner.Model);
            }

            var review = PlanValidator.Review(answer.Plan, site, budget.MaxSteps);

            if (review.Allowed)
            {
                // The planner's own notes and the validator's are both for a person, and both are
                // kept: one says what the model assumed, the other what it named without seeing.
                notes = [.. answer.Plan!.Notes, .. review.Notes];

                if (Overspent(spent, budget) is { } overspent)
                {
                    return new PlannedResult(null, [overspent], notes, spent, planner.Model);
                }

                PlanningLog.Planned(logger, planner.Model, answer.Plan.Actions.Count, attempt, spent.TotalTokens);
                return new PlannedResult(answer.Plan, [], notes, spent, planner.Model);
            }

            errors = review.Errors;
            PlanningLog.Refused(logger, planner.Model, attempt, string.Join(", ", errors));

            if (Overspent(spent, budget) is { } spentUp)
            {
                return new PlannedResult(null, [spentUp], review.Notes, spent, planner.Model);
            }

            request = request.Retry(errors);
        }

        // Twice wrong. The run ends saying planning failed; it does not improvise, and it does not
        // run the valid prefix of an invalid plan (ADR-0019 §3).
        return new PlannedResult(null, errors, notes, spent, planner.Model);
    }

    /// <summary>
    /// Whether the budget is spent, and which one. Checked after each attempt rather than before:
    /// what a call costs is only known once it has been made.
    /// </summary>
    private static string? Overspent(PlanningCost spent, RunBudget budget) =>
        spent.TotalTokens > budget.MaxModelTokens || spent.Cost > budget.MaxCostUsd
            ? PlanningErrors.BudgetSpent
            : null;
}

/// <summary>A plan that may run, or why none will, and what the attempt cost either way.</summary>
/// <param name="Plan">The plan, when there is one that passed.</param>
/// <param name="Errors">Resource keys saying why there is none.</param>
/// <param name="Notes">What a person should know about the plan that is about to run.</param>
/// <param name="Cost">Tokens, money and time, across every attempt.</param>
/// <param name="Model">Which model answered.</param>
public sealed record PlannedResult(
    TestPlan? Plan,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Notes,
    PlanningCost Cost,
    string Model)
{
    /// <summary>Whether there is a plan that may run.</summary>
    public bool Succeeded => Plan is not null;
}

/// <summary>What planning says to the log. Source-generated, as the analyzers require.</summary>
internal static partial class PlanningLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information,
        Message = "{Model} planned {Actions} action(s) on attempt {Attempt}, spending {Tokens} token(s).")]
    public static partial void Planned(ILogger logger, string model, int actions, int attempt, int tokens);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning,
        Message = "{Model}'s plan was refused on attempt {Attempt}: {Errors}")]
    public static partial void Refused(ILogger logger, string model, int attempt, string errors);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error,
        Message = "{Model} could not be asked for a plan: {Errors}")]
    public static partial void ProviderRefused(ILogger logger, string model, string errors);
}
