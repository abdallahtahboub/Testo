using Testo.Contracts.Planning;

namespace Testo.Benchmark;

/// <summary>
/// A planner that writes a plan which always passes: it looks at the catalogue and checks that the
/// catalogue is the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// This is not a joke and it is not a straw man. It is the <strong>calibration</strong> of the
/// whole benchmark. A table that reports "0 false positives" is worthless unless a false positive
/// is something the harness can actually see, and the cheapest way for an AI tester to score well
/// on a naive benchmark is exactly this: produce plans that technically assert something and never
/// go near the behaviour under test.
/// </para>
/// <para>
/// So the benchmark runs this deliberately useless planner against a broken shop and insists on
/// being told it passed — that is, insists on catching it. If the harness ever stops catching it,
/// every other number in the report has lost its meaning, and the self-check fails rather than
/// letting a flattering zero stand.
/// </para>
/// </remarks>
internal sealed class LazyPlanner : IScenarioPlanner
{
    /// <inheritdoc />
    public string Model => "lazy (calibration)";

    /// <inheritdoc />
    public Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Valid by every rule the validator has: in-origin, has an assertion, every action
        // complete. And it tests nothing whatsoever.
        var plan = new TestPlan(
            "A plan that will pass",
            [
                new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
                new PlannedAction(
                    ActionKind.AssertVisible,
                    "Check that the search box exists",
                    ActionTarget.ByTestId("search")),
            ],
            ["This plan was written to pass, not to test."]);

        return Task.FromResult(PlanResult.From(plan, PlanningCost.None with { Attempts = 1 }, Model));
    }
}
