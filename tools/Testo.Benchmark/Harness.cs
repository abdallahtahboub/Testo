using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Testo.Ai;
using Testo.Application.Planning;
using Testo.Contracts.Execution;
using Testo.Contracts.Planning;
using Testo.DemoShop;
using Testo.Worker;

namespace Testo.Benchmark;

/// <summary>What became of one run.</summary>
public enum Verdict
{
    /// <summary>It said what the truth was: passed on a sound shop, failed on a broken one.</summary>
    Correct = 1,

    /// <summary>It passed while the defect was switched on. The number that decides whether this is believable.</summary>
    FalsePositive = 2,

    /// <summary>It reported a problem in an application that had none.</summary>
    FalseNegative = 3,

    /// <summary>It never reached a verdict: out of time, over budget, could not run, or could not be planned.</summary>
    Inconclusive = 4,

    /// <summary>
    /// It failed the broken shop, and it also failed the sound one. It detected nothing.
    /// </summary>
    /// <remarks>
    /// Without this, such a run counts as a correct verdict and the table flatters the planner:
    /// the first real measurement scored five of six defects "caught", and four of those five were
    /// plans that fail against a working application too. A test that always fails is right about
    /// a broken application for the same reason a stopped clock is right twice a day.
    /// </remarks>
    FailedRegardless = 5,
}

/// <summary>One execution of one plan.</summary>
/// <param name="Verdict">What to make of it.</param>
/// <param name="Status">What the run actually reported.</param>
/// <param name="Elapsed">How long the execution took.</param>
public sealed record Attempt(Verdict Verdict, RunStatus Status, TimeSpan Elapsed);

/// <summary>Everything measured about one scenario, in one defect state, for one planner.</summary>
/// <param name="Scenario">Which scenario.</param>
/// <param name="DefectOn">Whether its defect was switched on.</param>
/// <param name="Source">"baseline" or the model's name.</param>
/// <param name="Attempts">One per repetition.</param>
/// <param name="Planning">What planning cost, once for this scenario and state.</param>
/// <param name="PlanningErrors">Why there was no plan, when there was none.</param>
/// <param name="Notes">What the planner and the validator wanted a person to know.</param>
public sealed record Measurement(
    BenchmarkScenario Scenario,
    bool DefectOn,
    string Source,
    IReadOnlyList<Attempt> Attempts,
    PlanningCost Planning,
    IReadOnlyList<string> PlanningErrors,
    IReadOnlyList<string> Notes)
{
    /// <summary>Whether every repetition agreed. Disagreement is flake, and it is the application's or ours.</summary>
    public bool Flaked => Attempts.Select(attempt => attempt.Status).Distinct().Count() > 1;

    /// <summary>The verdict of the first repetition, which is the one a person would have seen.</summary>
    public Verdict Verdict => Attempts.Count > 0 ? Attempts[0].Verdict : Verdict.Inconclusive;

    /// <summary>How long an execution took, on average.</summary>
    public TimeSpan Average => Attempts.Count == 0
        ? TimeSpan.Zero
        : TimeSpan.FromMilliseconds(Attempts.Average(attempt => attempt.Elapsed.TotalMilliseconds));
}

/// <summary>
/// Runs the benchmark: every scenario, in each defect state it has, with the baseline plan and
/// with a planner's, repeated enough times to see flake.
/// </summary>
/// <remarks>
/// <para>
/// Planning happens once per scenario and defect state; execution is repeated. That is a choice
/// worth knowing about: it means <em>flake rate measures execution</em> — the application and our
/// runner — and not the model's own variation between answers. Model variation is a separate
/// question with a separate answer, and mixing the two would produce a number that cannot be
/// acted on.
/// </para>
/// <para>
/// The defect switch is process-wide, so runs are serial. That also keeps the timings honest:
/// four browsers on one machine measure the machine.
/// </para>
/// </remarks>
public sealed class Harness(string origin, IScenarioPlanner? planner, int repetitions, string? recordingDirectory)
{
    /// <summary>
    /// Told what is happening as it happens.
    /// </summary>
    /// <remarks>
    /// A full run is ninety browser launches and eighteen model calls, which is ten minutes of
    /// nothing if the results are only printed at the end. The first person to run it with a
    /// planner watched a blank terminal and reasonably assumed it had hung.
    /// </remarks>
    public Action<string>? Say { get; set; }

    /// <summary>Called as each measurement finishes, so a long run reports itself.</summary>
    public Action<Measurement>? Measured { get; set; }

    private readonly PlanningService? _planning =
        planner is null ? null : new PlanningService(planner, NullLogger<PlanningService>.Instance);

    /// <summary>Runs everything and returns one measurement per scenario, state and source.</summary>
    public async Task<IReadOnlyList<Measurement>> RunAsync(
        IReadOnlyList<BenchmarkScenario> scenarios,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        var measurements = new List<Measurement>();

        foreach (var scenario in scenarios)
        {
            // A scenario with no defect has one state to be in; one with a defect has two, and the
            // second is where a false positive can happen.
            var states = scenario.Defect is null ? new[] { false } : [false, true];

            foreach (var defectOn in states)
            {
                Switch(scenario, defectOn);

                Report(measurements, await MeasureAsync(
                    scenario, defectOn, "baseline", scenario.Baseline, PlanningCost.None, [], [], cancellationToken)
                    .ConfigureAwait(false));

                if (_planning is null)
                {
                    continue;
                }

                // _planning is not null, so neither is the planner it was built from.
                var model = planner!.Model;

                Say?.Invoke($"  {scenario.Id,-34} defect={(defectOn ? "on " : "off")} "
                    + $"{model,-18} looking at the application, then planning…");

                var planned = await PlanAsync(scenario, cancellationToken).ConfigureAwait(false);

                var measured = planned.Plan is { } plan
                    ? await MeasureAsync(
                        scenario, defectOn, model, plan.Actions, planned.Cost, [], planned.Notes, cancellationToken)
                        .ConfigureAwait(false)
                    : new Measurement(scenario, defectOn, model, [], planned.Cost, planned.Errors, planned.Notes);

                Report(measurements, Honestly(measured, measurements));
            }
        }

        Defects.Set([]);
        return measurements;
    }

    /// <summary>
    /// Downgrades a "correct" verdict on a broken shop when the same plan also failed the sound
    /// one, because then it found nothing — it fails at everything, and one of the things it fails
    /// at happened to be broken.
    /// </summary>
    /// <remarks>
    /// The defect-off run always happens first, so by the time the defect is switched on the
    /// evidence for this is already in hand.
    /// </remarks>
    private static Measurement Honestly(Measurement measurement, List<Measurement> earlier)
    {
        if (!measurement.DefectOn || measurement.Verdict != Verdict.Correct)
        {
            return measurement;
        }

        var sound = earlier.LastOrDefault(candidate =>
            !candidate.DefectOn
            && candidate.Scenario.Id == measurement.Scenario.Id
            && string.Equals(candidate.Source, measurement.Source, StringComparison.Ordinal));

        if (sound?.Verdict != Verdict.FalseNegative)
        {
            return measurement;
        }

        return measurement with
        {
            Attempts =
            [
                .. measurement.Attempts.Select(attempt => attempt.Verdict == Verdict.Correct
                    ? attempt with { Verdict = Verdict.FailedRegardless }
                    : attempt),
            ],
        };
    }

    /// <summary>Keeps a measurement and says so, so a long run is not a blank terminal.</summary>
    private void Report(List<Measurement> measurements, Measurement measurement)
    {
        measurements.Add(measurement);
        Measured?.Invoke(measurement);
    }

    /// <summary>
    /// Crawls the shop and asks for a plan. The crawl is part of what a planned run costs, so it
    /// happens here rather than being prepared once and shared.
    /// </summary>
    private async Task<PlannedResult> PlanAsync(BenchmarkScenario scenario, CancellationToken cancellationToken)
    {
        var explored = await new ScenarioRunner(TimeProvider.System)
            .RunAsync(Job(scenario, WorkerTask.Explore, null), cancellationToken)
            .ConfigureAwait(false);

        var site = explored.Snapshot ?? SiteSnapshot.Empty(origin);

        var result = await _planning!.PlanAsync(
            scenario.Ask,
            site,
            RunBudget.Default,
            scenario.Secrets?.Keys.ToList() ?? [],
            cancellationToken).ConfigureAwait(false);

        Record(scenario, result);
        return result;
    }

    /// <summary>
    /// Saves a successful plan beside the others, so that CI can replay a real answer rather than
    /// one we imagined a model would give.
    /// </summary>
    private void Record(BenchmarkScenario scenario, PlannedResult result)
    {
        if (recordingDirectory is null || result.Plan is not { } plan)
        {
            return;
        }

        Directory.CreateDirectory(recordingDirectory);

        File.WriteAllText(
            Path.Combine(recordingDirectory, RecordedPlanner.Key(scenario.Ask) + ".json"),
            PlanRecording.Write(plan));
    }

    private async Task<Measurement> MeasureAsync(
        BenchmarkScenario scenario,
        bool defectOn,
        string source,
        IReadOnlyList<PlannedAction> plan,
        PlanningCost planning,
        IReadOnlyList<string> planningErrors,
        IReadOnlyList<string> notes,
        CancellationToken cancellationToken)
    {
        var attempts = new List<Attempt>();

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            var clock = Stopwatch.StartNew();

            var result = await new ScenarioRunner(TimeProvider.System)
                .RunAsync(Job(scenario, WorkerTask.Planned, plan), cancellationToken)
                .ConfigureAwait(false);

            clock.Stop();

            attempts.Add(new Attempt(Judge(result.Status, defectOn), result.Status, clock.Elapsed));
        }

        return new Measurement(scenario, defectOn, source, attempts, planning, planningErrors, notes);
    }

    /// <summary>
    /// What to make of a status, given what was true of the shop at the time.
    /// </summary>
    /// <remarks>
    /// The asymmetry is the point. Passing a broken application is a false positive, and it is the
    /// expensive mistake: it converts an absence of testing into an assurance of quality. Failing
    /// a sound one is a false negative, which costs a person an afternoon. Anything that never
    /// reached a verdict is neither, and counting it as either would flatter or libel the planner.
    /// </remarks>
    private static Verdict Judge(RunStatus status, bool defectOn) => status switch
    {
        RunStatus.Passed => defectOn ? Verdict.FalsePositive : Verdict.Correct,
        RunStatus.Failed => defectOn ? Verdict.Correct : Verdict.FalseNegative,
        _ => Verdict.Inconclusive,
    };

    private static void Switch(BenchmarkScenario scenario, bool defectOn)
    {
        if (!Defects.Set(defectOn && scenario.Defect is { } defect ? [defect] : []))
        {
            throw new InvalidOperationException($"'{scenario.Defect}' is not a defect this shop has.");
        }
    }

    private WorkerJob Job(BenchmarkScenario scenario, WorkerTask task, IReadOnlyList<PlannedAction>? plan) => new(
        RunId: Guid.CreateVersion7(),
        scenario.Id,
        origin,
        AllowedOrigins: [],
        TrustedLocalOrigins: [origin],
        RunBudget.Default,
        Directory.CreateTempSubdirectory("testo-benchmark-").FullName,
        task,
        plan,
        scenario.Secrets);
}
