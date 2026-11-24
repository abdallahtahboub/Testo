using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autestiq.Ai;
using Autestiq.Application.Planning;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>
/// Experiment 0001: repair planning from verified experience
/// (<c>docs/ai-learning/experiments/0001-repair-planning-from-verified-experience.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each scenario is planned once against the version its test was written for (no change). That plan is then run
/// against a changed version (<see cref="AppChanges"/>), with the scenario's defect off and, where it has one, on.
/// Every failed run is repaired: condition B with the failure only, condition C with up to three verified repairs from
/// other applications. Each repaired plan is run on the same variant, and a repair made on the sound variant is also
/// run on the broken one, to see that it still catches the defect.
/// </para>
/// <para>
/// Ground truth is the switches: a change is not a fault, so a good repair passes the sound variant; a defect is, so a
/// repair that passes a broken variant is a false positive. The corpus phase collects verified repairs (condition B on
/// the training applications); the evaluate phase compares B and C on a held-out application, and refuses a corpus
/// that contains it.
/// </para>
/// </remarks>
internal sealed class Experiment0001(
    Harness harness,
    string target,
    string modelId,
    string phase,
    string corpusPath,
    string recordsPath,
    int repeats,
    Action<string> say)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<CorpusEntry> _corpus = [];

    /// <summary>The counts the experiment's thresholds are written in, per condition.</summary>
    public Dictionary<string, Tally> Tallies { get; } = new(StringComparer.Ordinal);

    public async Task RunAsync(IReadOnlyList<BenchmarkScenario> scenarios, CancellationToken cancellationToken)
    {
        if (phase == "evaluate")
        {
            LoadCorpus();
        }

        var promptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PlanPrompt.System)))[..12];

        foreach (var scenario in scenarios)
        {
            AppChanges.Current = null;
            harness.SwitchDefect(scenario, defectOn: false);

            say($"  {scenario.Id}: planning against the version the test was written for…");
            var first = await harness.PlanScenarioAsync(scenario, repair: null, cancellationToken).ConfigureAwait(false);

            if (first.Plan is not { } original)
            {
                Write(new Record(phase, target, scenario.Id, null, false, "A", 0, modelId, promptHash, "unplanned", null, null, null, first.Cost.Cost, []));
                continue;
            }

            foreach (var change in AppChanges.All)
            {
                AppChanges.Current = change;

                foreach (var defectOn in scenario.Defect is null ? new[] { false } : [false, true])
                {
                    harness.SwitchDefect(scenario, defectOn);
                    var (status, result) = await harness.RunPlanAsync(scenario, original.Actions, original.Coverage, cancellationToken).ConfigureAwait(false);
                    var verdict = Harness.VerdictFor(status, defectOn);

                    Count("A", defectOn, verdict, detectionKept: null);
                    Write(new Record(phase, target, scenario.Id, change, defectOn, "A", 0, modelId, promptHash, status.ToString(), verdict.ToString(), null, Reason(result), first.Cost.Cost, []));

                    if (status == RunStatus.Passed)
                    {
                        continue;
                    }

                    var (failedStep, failure, failedAction) = FailureOf(original.Actions, result);

                    foreach (var condition in phase == "corpus" ? new[] { "B" } : ["B", "C"])
                    {
                        for (var repeat = 1; repeat <= repeats; repeat++)
                        {
                            await RepairAsync(scenario, change, defectOn, condition, repeat, original, failedStep, failure, failedAction, promptHash, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        AppChanges.Current = null;

        if (phase == "corpus" && _corpus.Count > 0)
        {
            await File.AppendAllLinesAsync(corpusPath, _corpus.Select(entry => JsonSerializer.Serialize(entry, Json)), cancellationToken).ConfigureAwait(false);
            say($"  Added {_corpus.Count} verified repair(s) to {corpusPath}.");
        }
    }

    private async Task RepairAsync(
        BenchmarkScenario scenario,
        string change,
        bool defectOn,
        string condition,
        int repeat,
        TestPlan original,
        string failedStep,
        string failure,
        PlannedAction? failedAction,
        string promptHash,
        CancellationToken cancellationToken)
    {
        var examples = condition == "C" ? Retrieve(failedAction, failure) : [];

        harness.SwitchDefect(scenario, defectOn);
        var planned = await harness.PlanScenarioAsync(
            scenario,
            new PlanRepair(original.Actions, failedStep, failure, examples.Select(entry => entry.Text).ToList()),
            cancellationToken).ConfigureAwait(false);

        if (planned.Plan is not { } repaired)
        {
            Count(condition, defectOn, Verdict.Inconclusive, detectionKept: null);
            Write(new Record(phase, target, scenario.Id, change, defectOn, condition, repeat, modelId, promptHash, "unplanned", null, null, string.Join(", ", planned.Errors), planned.Cost.Cost, [.. examples.Select(entry => entry.Id)]));
            return;
        }

        var (status, result) = await harness.RunPlanAsync(scenario, repaired.Actions, repaired.Coverage, cancellationToken).ConfigureAwait(false);
        var verdict = Harness.VerdictFor(status, defectOn);

        // A repair made on the sound variant must still catch the defect, where the scenario has one.
        bool? detectionKept = null;

        if (!defectOn && scenario.Defect is not null)
        {
            harness.SwitchDefect(scenario, defectOn: true);
            var (broken, _) = await harness.RunPlanAsync(scenario, repaired.Actions, repaired.Coverage, cancellationToken).ConfigureAwait(false);
            detectionKept = broken == RunStatus.Failed;
            harness.SwitchDefect(scenario, defectOn: false);
        }

        Count(condition, defectOn, verdict, detectionKept);
        Write(new Record(phase, target, scenario.Id, change, defectOn, condition, repeat, modelId, promptHash, status.ToString(), verdict.ToString(), detectionKept, Reason(result), planned.Cost.Cost, [.. examples.Select(entry => entry.Id)]));

        // Verified: passes the changed sound version and, where there is a defect, still fails it.
        if (phase == "corpus" && !defectOn && verdict == Verdict.Correct && detectionKept != false)
        {
            _corpus.Add(new CorpusEntry(
                $"{target}/{scenario.Id}/{change}/{repeat}",
                target,
                failedAction?.Kind.ToString() ?? "",
                failedAction?.Target?.Kind.ToString() ?? "",
                FailureClass(failure),
                Example(failedStep, failure, repaired)));
        }
    }

    /// <summary>
    /// Up to three verified repairs, chosen from what can be observed about the failure: the kind of step that failed,
    /// how it named its target, and the kind of message. Never the change itself, which a real repair would not know.
    /// </summary>
    private List<CorpusEntry> Retrieve(PlannedAction? failedAction, string failure)
    {
        var kind = failedAction?.Kind.ToString() ?? "";
        var targetKind = failedAction?.Target?.Kind.ToString() ?? "";
        var failureClass = FailureClass(failure);

        return
        [
            .. _corpus
                .Select(entry => (Entry: entry, Score:
                    (entry.FailedKind == kind ? 2 : 0) + (entry.TargetKind == targetKind ? 1 : 0) + (entry.FailureClass == failureClass ? 1 : 0)))
                .Where(pair => pair.Score > 0)
                .OrderByDescending(pair => pair.Score)
                .ThenBy(pair => pair.Entry.Id, StringComparer.Ordinal)
                .Take(3)
                .Select(pair => pair.Entry),
        ];
    }

    private void LoadCorpus()
    {
        if (!File.Exists(corpusPath))
        {
            throw new InvalidOperationException($"No corpus at {corpusPath}: run the corpus phase on the training applications first.");
        }

        foreach (var line in File.ReadLines(corpusPath).Where(line => line.Length > 0))
        {
            var entry = JsonSerializer.Deserialize<CorpusEntry>(line, Json)
                ?? throw new InvalidOperationException("A corpus line could not be read.");

            // The held-out application must not be in what the planner may be shown.
            if (string.Equals(entry.Target, target, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The corpus contains '{target}', the application being evaluated: refused.");
            }

            _corpus.Add(entry);
        }

        say($"  Loaded {_corpus.Count} verified repair(s) from other applications.");
    }

    private static (string Step, string Failure, PlannedAction? Action) FailureOf(IReadOnlyList<PlannedAction> plan, WorkerResult result)
    {
        var failed = result.Steps.FirstOrDefault(step => step.Status == StepStatus.Failed);
        var action = failed is { Order: > 0 } && failed.Order <= plan.Count ? plan[failed.Order - 1] : null;

        return (failed?.Description ?? "(no step failed; the run ended " + result.Status + ")",
            failed?.Message ?? result.FailureReason ?? result.Status.ToString(),
            action);
    }

    private static string FailureClass(string failure) =>
        failure.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ? "timeout"
        : failure.Contains("expected to have text", StringComparison.OrdinalIgnoreCase) ? "text"
        : failure.Contains("expected to be visible", StringComparison.OrdinalIgnoreCase) ? "visible"
        : failure.Contains("expected to be hidden", StringComparison.OrdinalIgnoreCase) ? "hidden"
        : "other";

    private static string Example(string failedStep, string failure, TestPlan repaired) =>
        $"A step \"{failedStep}\" failed with \"{Short(failure, 140)}\"; the verified repaired plan was: "
        + string.Join(" | ", repaired.Actions.Take(10).Select(PlanPrompt.Step));

    private static string Short(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private static string? Reason(WorkerResult result) => result.FailureReason is { } reason ? Short(reason.Split('\n')[0], 200) : null;

    private void Count(string condition, bool defectOn, Verdict verdict, bool? detectionKept)
    {
        if (!Tallies.TryGetValue(condition, out var tally))
        {
            Tallies[condition] = tally = new Tally();
        }

        tally.Add(defectOn, verdict, detectionKept);
    }

    private void Write(Record record) =>
        File.AppendAllText(recordsPath, JsonSerializer.Serialize(record with { At = DateTimeOffset.UtcNow }, Json) + "\n");

    /// <summary>One experience: an attempt at one variant under one condition (docs/ai-learning/data-architecture.md).</summary>
    private sealed record Record(
        string Phase,
        string Target,
        string Scenario,
        string? Change,
        bool DefectOn,
        string Condition,
        int Repeat,
        string Model,
        string PromptHash,
        string Status,
        string? Verdict,
        bool? DetectionKept,
        string? Reason,
        decimal Cost,
        IReadOnlyList<string> Examples)
    {
        public DateTimeOffset At { get; init; }
    }

    /// <summary>A verified repair, with the observable features it is retrieved by.</summary>
    private sealed record CorpusEntry(string Id, string Target, string FailedKind, string TargetKind, string FailureClass, string Text);

    /// <summary>What the pre-registered thresholds are judged on.</summary>
    internal sealed class Tally
    {
        public int SoundRuns { get; private set; }

        public int SoundCorrect { get; private set; }

        public int BrokenRuns { get; private set; }

        public int FalsePositives { get; private set; }

        public int DetectionChecked { get; private set; }

        public int DetectionLost { get; private set; }

        public int Inconclusive { get; private set; }

        public void Add(bool defectOn, Verdict verdict, bool? detectionKept)
        {
            if (verdict == Verdict.Inconclusive)
            {
                Inconclusive++;
            }

            if (defectOn)
            {
                BrokenRuns++;
                FalsePositives += verdict == Verdict.FalsePositive ? 1 : 0;
            }
            else
            {
                SoundRuns++;
                SoundCorrect += verdict == Verdict.Correct && detectionKept != false ? 1 : 0;
            }

            if (detectionKept is { } kept)
            {
                DetectionChecked++;
                DetectionLost += kept ? 0 : 1;

                // A repair that passes the broken version is a false positive, wherever it was made.
                FalsePositives += kept ? 0 : 1;
            }
        }

        public override string ToString() =>
            $"sound: {SoundCorrect} of {SoundRuns} correct (passing and still catching the defect); "
            + $"broken: {BrokenRuns} run(s); false positives: {FalsePositives}; detection lost: {DetectionLost} of {DetectionChecked}; inconclusive: {Inconclusive}";
    }
}
