using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker;

namespace Autestiq.Benchmark;

/// <summary>
/// How much of what the baseline plans need the crawl actually shows: a check that needs no model and
/// no key, and so can be run on every change to the crawl.
/// </summary>
/// <remarks>
/// <para>
/// A planner can only choose an element the crawl showed it, and it can only go to a page the crawl
/// reached. So a baseline step whose element is missing from the snapshot is a step no planner could
/// have written from that snapshot. The coverage figure is therefore a ceiling on what the crawl allows,
/// not a measure of the planner.
/// </para>
/// <para>
/// Steps that name an element count, as do steps that go to a page. Steps that only do something
/// (a click on an element the plan has already seen) count as the element they name, since a click
/// needs its element as much as an assertion does.
/// </para>
/// </remarks>
internal static class Coverage
{
    /// <summary>Explores the target once, the way a planner's exploration does, and says what that took.</summary>
    /// <param name="origin">Where the target is.</param>
    /// <param name="startAfresh">Puts the target back to its starting state, when it keeps any on its server.</param>
    public static async Task<(SiteSnapshot Snapshot, string Said)> ExploreAsync(string origin, Func<Task>? startAfresh)
    {
        var job = new WorkerJob(
            RunId: Guid.CreateVersion7(),
            ScenarioId: "coverage",
            TargetOrigin: origin,
            AllowedOrigins: [],
            TrustedLocalOrigins: [origin],
            Budget: Harness.ExplorationBudget,
            ArtifactDirectory: Directory.CreateTempSubdirectory("autestiq-coverage-").FullName,
            Task: WorkerTask.Explore,
            ExploreStates: true,
            SubmitForms: true);

        var result = await new ScenarioRunner(TimeProvider.System) { StartAfresh = startAfresh }
            .RunAsync(job, CancellationToken.None);

        var said = result.Steps.Count > 0 && result.Steps[0].Message is { Length: > 0 } message
            ? message
            : result.FailureReason ?? result.Status.ToString();

        // A crawl that saw nothing says why, rather than reporting an empty application.
        if (result.Snapshot is not { Pages.Count: > 0 })
        {
            said += $" ({result.Status}: {result.FailureReason ?? "no reason given"})";
        }

        return (result.Snapshot ?? SiteSnapshot.Empty(origin), said);
    }

    /// <summary>Writes, per scenario, the steps the crawl shows and the steps it does not.</summary>
    public static void Print(IReadOnlyList<BenchmarkScenario> scenarios, SiteSnapshot snapshot, TextWriter output)
    {
        output.WriteLine($"Coverage of the baseline plans by the crawl of {snapshot.Origin}");
        output.WriteLine($"{"scenario",-42} {"needs",5} {"seen",5}  not seen");

        var needed = 0;
        var seen = 0;
        var missingIds = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var scenario in scenarios)
        {
            var scenarioNeeded = 0;
            var scenarioSeen = 0;
            var gaps = new List<string>();

            foreach (var step in scenario.Baseline)
            {
                string? name;
                bool shown;

                if (step.Kind == ActionKind.Goto && step.Path is { } path)
                {
                    name = path;
                    shown = snapshot.HasPath(path);
                }
                else if (step.Target is { } target)
                {
                    name = target.Value;
                    shown = snapshot.Offers(target);
                }
                else
                {
                    continue;
                }

                scenarioNeeded++;

                if (shown)
                {
                    scenarioSeen++;
                }
                else
                {
                    gaps.Add(name);
                    missingIds.Add(name);
                }
            }

            needed += scenarioNeeded;
            seen += scenarioSeen;

            output.WriteLine($"{scenario.Id,-42} {scenarioNeeded,5} {scenarioSeen,5}  {string.Join(", ", gaps)}");
        }

        output.WriteLine();
        output.WriteLine($"Overall: the crawl shows {seen} of {needed} steps' elements and pages ({Percent(seen, needed)}).");

        if (missingIds.Count > 0)
        {
            output.WriteLine($"Never shown ({missingIds.Count} distinct): {string.Join(", ", missingIds)}");
        }
    }

    /// <summary>
    /// The same question for a copy of the application without test ids: for each step, is the element its
    /// baseline names by test id still shown, by its role and name or by its text?
    /// </summary>
    /// <param name="scenarios">The scenarios whose baselines are counted.</param>
    /// <param name="withoutIds">What the exploration shows of the copy without test ids.</param>
    /// <param name="withIds">What it shows of a copy with them, which says what each test id is.</param>
    /// <param name="output">Where to write.</param>
    public static void PrintWithoutTestIds(IReadOnlyList<BenchmarkScenario> scenarios, SiteSnapshot withoutIds, SiteSnapshot withIds, TextWriter output)
    {
        var known = withIds.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.TestId is { Length: > 0 })
            .GroupBy(element => element.TestId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var shown = withoutIds.Pages.SelectMany(page => page.Elements).ToList();

        output.WriteLine($"Coverage of the baseline plans by the crawl of {withoutIds.Origin}, served without test ids");
        output.WriteLine($"{"scenario",-42} {"needs",5} {"seen",5}  not seen");

        var needed = 0;
        var seen = 0;
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var scenario in scenarios)
        {
            var scenarioNeeded = 0;
            var scenarioSeen = 0;
            var gaps = new List<string>();

            foreach (var step in scenario.Baseline)
            {
                bool found;
                string name;

                if (step.Kind == ActionKind.Goto && step.Path is { } path)
                {
                    name = path;
                    found = withoutIds.HasPath(path);
                }
                else if (step.Target is { Kind: TargetKind.TestId } target)
                {
                    name = target.Value;

                    // An element the copy with test ids never showed cannot be looked for by what it says.
                    found = known.TryGetValue(target.Value, out var element) && shown.Any(candidate =>
                        candidate.Role == element.Role
                        && string.Equals(candidate.Name, element.Name, StringComparison.Ordinal)
                        && string.Equals(candidate.Text ?? candidate.Name, element.Text ?? element.Name, StringComparison.Ordinal));
                }
                else
                {
                    continue;
                }

                scenarioNeeded++;

                if (found)
                {
                    scenarioSeen++;
                }
                else
                {
                    gaps.Add(name);
                    missing.Add(name);
                }
            }

            needed += scenarioNeeded;
            seen += scenarioSeen;
            output.WriteLine($"{scenario.Id,-42} {scenarioNeeded,5} {scenarioSeen,5}  {string.Join(", ", gaps)}");
        }

        output.WriteLine();
        output.WriteLine($"Overall: without test ids the crawl shows {seen} of {needed} steps' elements and pages ({Percent(seen, needed)}).");

        if (missing.Count > 0)
        {
            output.WriteLine($"Never shown ({missing.Count} distinct): {string.Join(", ", missing)}");
        }
    }

    private static string Percent(int part, int whole) =>
        whole == 0 ? "no steps" : $"{100.0 * part / whole:0}%";
}
