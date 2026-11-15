using Microsoft.Playwright;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker.Scenarios;

namespace Autestiq.Worker;

/// <summary>
/// Turns a validated plan into the same kind of scenario the scripted ones are, so that a planned
/// run goes through exactly the apparatus Milestone 3.3 built: the proxy, the budgets, the step
/// statuses, the screenshots.
/// </summary>
/// <remarks>
/// <para>
/// Each action becomes one step. Nothing here interprets a string as code: every action is a switch
/// over a closed enum, and a target becomes a Playwright locator built from a test id or a role and
/// a name. There is no path by which a plan could ask for anything else, because there is no field
/// it could ask in (ADR-0019 §2).
/// </para>
/// <para>
/// A plan reaching here has already been validated against what the crawl observed. The worker
/// checks the few things it alone can check — a secret it was not given, an action missing what it
/// needs — and treats the rest as given, because a worker that re-derives policy is a second place
/// for policy to be wrong.
/// </para>
/// </remarks>
internal static class PlannedScenario
{
    /// <summary>Builds the scenario a plan describes.</summary>
    public static Scenario From(WorkerJob job, IReadOnlyList<PlannedAction> plan)
    {
        var secrets = job.Secrets ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var steps = plan
            .Select(action => new ScenarioStep(
                action.Description,
                (page, cancellation) => CarryOutAsync(page, action, secrets, cancellation)))
            .ToList();

        return new Scenario(job.ScenarioId, job.ScenarioId, "A plan, carried out as it was written.", steps);
    }

    private static async Task CarryOutAsync(
        IPage page,
        PlannedAction action,
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (action.Kind)
        {
            case ActionKind.Goto:
                await page.GotoAsync(Required(action.Path, action)).ConfigureAwait(false);
                break;

            case ActionKind.Click:
                await (await LocateAsync(page, action).ConfigureAwait(false)).ClickAsync().ConfigureAwait(false);
                break;

            case ActionKind.Type:
                await (await LocateAsync(page, action).ConfigureAwait(false)).FillAsync(TextToType(action, secrets)).ConfigureAwait(false);
                break;

            case ActionKind.Press:
                await (await LocateAsync(page, action).ConfigureAwait(false)).PressAsync(Required(action.Text, action)).ConfigureAwait(false);
                break;

            case ActionKind.WaitFor:
                await (await LocateAsync(page, action).ConfigureAwait(false)).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached })
                    .ConfigureAwait(false);
                break;

            case ActionKind.AssertText:
                await Assertions.Expect(await LocateAsync(page, action).ConfigureAwait(false))
                    .ToHaveTextAsync(Required(action.Text, action))
                    .ConfigureAwait(false);
                break;

            case ActionKind.AssertVisible:
                await Assertions.Expect(await LocateAsync(page, action).ConfigureAwait(false)).ToBeVisibleAsync().ConfigureAwait(false);
                break;

            case ActionKind.AssertNotVisible:
                // A waiting assertion, like the others: "not there" has to mean "not there once the
                // page has settled", or every absence passes on a page that has not finished.
                await Assertions.Expect(await LocateAsync(page, action).ConfigureAwait(false)).ToBeHiddenAsync().ConfigureAwait(false);
                break;

            case ActionKind.AssertUrl:
                // A path, not a URL: a plan cannot name another origin, so it cannot assert one
                // either. Checked as "ends with" so a query string does not fail an honest check.
                await Assertions.Expect(page).ToHaveURLAsync(
                    new System.Text.RegularExpressions.Regex(
                        System.Text.RegularExpressions.Regex.Escape(Required(action.Path, action)) + "(\\?.*)?$"))
                    .ConfigureAwait(false);
                break;

            default:
                throw new PlaywrightException($"The plan asks for '{action.Kind}', which this worker cannot do.");
        }
    }

    /// <summary>
    /// The text an action types: its own, or the value of a secret this run was given.
    /// </summary>
    /// <remarks>
    /// A secret the worker was not given is a fault in the run's configuration, and it fails the
    /// step by name. The name is safe to say; the value is never in a message, a log or a plan.
    /// </remarks>
    private static string TextToType(PlannedAction action, IReadOnlyDictionary<string, string> secrets)
    {
        if (action.SecretName is not { Length: > 0 } name)
        {
            return Required(action.Text, action);
        }

        return secrets.TryGetValue(name, out var value)
            ? value
            : throw new PlaywrightException($"This run was not given a value called '{name}'.");
    }

    /// <summary>A target becomes a locator, in the one place that decides how (<see cref="Targets"/>).</summary>
    private static Task<ILocator> LocateAsync(IPage page, PlannedAction action) =>
        Targets.LocateForStepAsync(
            page,
            action.Target ?? throw new PlaywrightException($"The step '{action.Description}' does not say what to act on."));

    private static string Required(string? value, PlannedAction action) =>
        value is { Length: > 0 }
            ? value
            : throw new PlaywrightException($"The step '{action.Description}' is missing something it cannot run without.");
}
