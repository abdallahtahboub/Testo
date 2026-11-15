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
    /// <summary>Runs the crawl once, against the target as it is, with exploration on, as the planner gets it.</summary>
    public static async Task<SiteSnapshot> ExploreAsync(string origin)
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
            ExploreStates: true);

        var result = await new ScenarioRunner(TimeProvider.System).RunAsync(job, CancellationToken.None);

        return result.Snapshot ?? SiteSnapshot.Empty(origin);
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

    private static string Percent(int part, int whole) =>
        whole == 0 ? "no steps" : $"{100.0 * part / whole:0}%";
}
