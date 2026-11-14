using Testo.Contracts.Common;

namespace Testo.Contracts.Planning;

/// <summary>
/// What a planner produced: the actions to carry out, and enough about where they came from to
/// judge them later.
/// </summary>
/// <param name="ScenarioName">What this plan is a plan for, in a person's words.</param>
/// <param name="Actions">The actions, in order.</param>
/// <param name="Notes">
/// Anything the planner wants a person to know — an assumption it made, an assertion it would
/// like changed. Never acted on; a suggestion is not an action (ADR-0019 §3).
/// </param>
public sealed record TestPlan(string ScenarioName, IReadOnlyList<PlannedAction> Actions, IReadOnlyList<string> Notes)
{
    /// <summary>How many of the actions check something.</summary>
    public int AssertionCount => Actions.Count(action => action.IsAssertion);
}

/// <summary>What one planning call cost.</summary>
/// <param name="InputTokens">Tokens sent.</param>
/// <param name="OutputTokens">Tokens received.</param>
/// <param name="Attempts">How many times the planner was asked, including retries after invalid answers.</param>
/// <param name="Cost">What it came to, in US dollars, by the provider's published price list.</param>
/// <param name="Elapsed">How long the planning took.</param>
public readonly record struct PlanningCost(
    int InputTokens,
    int OutputTokens,
    int Attempts,
    decimal Cost,
    TimeSpan Elapsed)
{
    /// <summary>Nothing spent: the fake and the recorded planners, and a run that never planned.</summary>
    public static PlanningCost None => new(0, 0, 0, 0m, TimeSpan.Zero);

    /// <summary>Every token, which is what a budget is about.</summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>Adds up two attempts at the same plan.</summary>
    public static PlanningCost operator +(PlanningCost first, PlanningCost second) => new(
        first.InputTokens + second.InputTokens,
        first.OutputTokens + second.OutputTokens,
        first.Attempts + second.Attempts,
        first.Cost + second.Cost,
        first.Elapsed + second.Elapsed);

    /// <summary>Adds up two attempts at the same plan.</summary>
    public PlanningCost Add(PlanningCost other) => this + other;
}

/// <summary>A plan, or why there is none, and what the attempt cost either way.</summary>
/// <param name="Plan">The plan, when there is one.</param>
/// <param name="Errors">Resource keys saying why there is none.</param>
/// <param name="Cost">What the attempt cost. Charged whether or not it worked.</param>
/// <param name="Model">Which model answered, for the record.</param>
public sealed record PlanResult(
    TestPlan? Plan,
    IReadOnlyList<string> Errors,
    PlanningCost Cost,
    string Model)
{
    /// <summary>Whether there is a plan.</summary>
    public bool Succeeded => Plan is not null;

    /// <summary>A plan that was produced and validated.</summary>
    public static PlanResult From(TestPlan plan, PlanningCost cost, string model) => new(plan, [], cost, model);

    /// <summary>No plan, and why. The cost is still reported: a failed attempt is still billed.</summary>
    public static PlanResult Failed(IReadOnlyList<string> errors, PlanningCost cost, string model) =>
        new(null, errors, cost, model);
}

/// <summary>Resource keys a planner or its validator may answer with.</summary>
/// <remarks>
/// Keys, not sentences: the application layer never decides what language a person reads
/// (<c>CLAUDE.md</c>, Architecture rules).
/// </remarks>
public static class PlanningErrors
{
    /// <summary>The model's answer was not the shape a plan has.</summary>
    public const string Unreadable = "Planning_Error_Unreadable";

    /// <summary>The answer named an action that does not exist.</summary>
    public const string UnknownAction = "Planning_Error_UnknownAction";

    /// <summary>The answer named something no page has.</summary>
    public const string UnknownTarget = "Planning_Error_UnknownTarget";

    /// <summary>The answer wanted to leave the run's own target.</summary>
    public const string ForeignOrigin = "Planning_Error_ForeignOrigin";

    /// <summary>The plan has more actions than the run's budget allows.</summary>
    public const string TooManyActions = "Planning_Error_TooManyActions";

    /// <summary>The plan checks nothing, so it cannot find a defect.</summary>
    public const string NoAssertions = "Planning_Error_NoAssertions";

    /// <summary>An action was missing something it cannot work without.</summary>
    public const string Incomplete = "Planning_Error_Incomplete";

    /// <summary>The planner spent the run's token or money budget.</summary>
    public const string BudgetSpent = "Planning_Error_BudgetSpent";

    /// <summary>The provider could not be reached, or would not answer.</summary>
    public const string ProviderUnavailable = "Planning_Error_ProviderUnavailable";

    /// <summary>No planner is configured, so nothing can be planned.</summary>
    public const string NoPlanner = "Planning_Error_NoPlanner";
}

/// <summary>Turns a scenario in a person's words into a plan a browser can carry out.</summary>
/// <remarks>
/// Provider-agnostic by construction: the implementations are an adapter per provider, plus a
/// recorded one that replays a stored answer so that tests cost nothing and never vary.
/// </remarks>
public interface IScenarioPlanner
{
    /// <summary>Which model this planner uses, for the record and for the benchmark table.</summary>
    string Model { get; }

    /// <summary>
    /// Plans the scenario. Never throws for a bad answer from the provider: an answer that cannot
    /// be used is a <see cref="PlanResult"/> saying so, because that is a thing worth recording
    /// rather than an exception worth losing.
    /// </summary>
    Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Everything a planner is allowed to know.</summary>
/// <remarks>
/// Deliberately short. It holds no credential, no organization and no run id: a planner is given a
/// scenario and a description of an application, and returns actions.
/// </remarks>
/// <param name="Scenario">What the person wants tested, in their words.</param>
/// <param name="Site">What a deterministic crawl found. Untrusted input (ADR-0019 §5).</param>
/// <param name="SecretNames">
/// The names of the values this run holds, so a plan can say "type the password" without the
/// planner ever seeing one.
/// </param>
/// <param name="MaxActions">The most actions a plan may have.</param>
/// <param name="MaxTokens">The most tokens this planning may spend.</param>
/// <param name="PreviousErrors">
/// On a retry, what was wrong with the last answer. Empty on the first attempt.
/// </param>
public sealed record PlanRequest(
    string Scenario,
    SiteSnapshot Site,
    IReadOnlyList<string> SecretNames,
    int MaxActions,
    int MaxTokens,
    IReadOnlyList<string> PreviousErrors)
{
    /// <summary>A first attempt, with nothing yet to learn from.</summary>
    public static PlanRequest First(
        string scenario,
        SiteSnapshot site,
        IReadOnlyList<string> secretNames,
        int maxActions,
        int maxTokens) =>
        new(scenario, site, secretNames, maxActions, maxTokens, []);

    /// <summary>The same request again, with what was wrong last time.</summary>
    public PlanRequest Retry(IReadOnlyList<string> errors) => this with { PreviousErrors = errors };
}

/// <summary>A plan as it was stored, with what it cost and whether a person has looked at it.</summary>
/// <param name="PlanId">The plan.</param>
/// <param name="ScenarioName">What it is a plan for.</param>
/// <param name="Model">Which model produced it.</param>
/// <param name="Actions">The actions, in order.</param>
/// <param name="Notes">What the planner wanted a person to know.</param>
/// <param name="Cost">What it cost to produce.</param>
/// <param name="CreatedAt">When it was produced.</param>
public sealed record StoredPlan(
    Guid PlanId,
    string ScenarioName,
    string Model,
    IReadOnlyList<PlannedAction> Actions,
    IReadOnlyList<string> Notes,
    PlanningCost Cost,
    DateTimeOffset CreatedAt);

/// <summary>Reading and keeping plans.</summary>
public interface IPlanService
{
    /// <summary>One plan, or <c>null</c> when there is no such plan in this organization.</summary>
    Task<StoredPlan?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>The plans of a project, newest first.</summary>
    Task<PagedResult<StoredPlan>> ListPlansAsync(
        Guid projectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
