using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>What became of one stated expected result in one run: whether a step checked it, and how that step ended.</summary>
public enum StatedCheck
{
    /// <summary>No step of the plan checks it.</summary>
    NotChecked,

    /// <summary>Every step that checks it passed.</summary>
    Passed,

    /// <summary>A step that checks it failed.</summary>
    Failed,

    /// <summary>A step checks it, but the run ended before that step finished.</summary>
    NotReached,
}

/// <summary>How a planner did on one stated result, judged against the hand-written baseline (Milestone 5.1 R5).</summary>
public enum StatedScore
{
    /// <summary>The result holds, and the plan's check of it passed.</summary>
    Confirmed,

    /// <summary>The defect breaks the result, and the plan's check of it failed.</summary>
    Caught,

    /// <summary>The defect breaks the result, and the plan's check of it passed: the mistake that matters most.</summary>
    FalsePass,

    /// <summary>The result holds, and the plan's check of it failed.</summary>
    WrongFail,

    /// <summary>The plan does not check it: partial coverage of what the tester said.</summary>
    NotChecked,

    /// <summary>The plan checks it, but the run ended first: no verdict on it.</summary>
    Inconclusive,

    /// <summary>The baseline did not reach it on the broken application, so whether the defect breaks it is unknown.</summary>
    NotScored,
}

/// <summary>
/// The stated expected results of a run, checked and scored one by one (Milestone 5.1 R5). Ground truth comes from the
/// hand-written baseline, never from a judgement: on the sound application every stated result holds; on the broken
/// one, a result is broken when the baseline's own check of it fails, holds when it passes, and is not scored when the
/// baseline stopped before it.
/// </summary>
public static class StatedResults
{
    /// <summary>
    /// The unseen-stated set: each scenario as it is, with the texts its baseline checks stated by the tester. The
    /// stated results are derived by rule, not written for the run, so no one chose them after seeing a plan.
    /// </summary>
    public static IReadOnlyList<BenchmarkScenario> Stated(IReadOnlyList<BenchmarkScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        return [.. scenarios.Select(scenario => scenario with
        {
            Id = scenario.Id + "+stated",
            Expected = scenario.Expected is { Count: > 0 } given
                ? given
                : [.. scenario.Baseline
                    .Where(action => action.Kind == ActionKind.AssertText && action.Text is { Length: > 0 })
                    .Select(action => action.Text!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(ExpectedResults.MaxCount)
                    .Select(text => new ExpectedResult(text))],
        })];
    }

    /// <summary>For each stated result, whether the plan's steps checked it and how they ended.</summary>
    public static IReadOnlyDictionary<string, StatedCheck> Check(
        IReadOnlyList<ExpectedResult>? expected, IReadOnlyList<PlannedAction> plan, IReadOnlyList<WorkerStepResult> steps)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(steps);

        var checks = new Dictionary<string, StatedCheck>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in expected ?? [])
        {
            // Step n carries out action n - 1 (WorkerStepResult.Order starts at 1).
            var statuses = plan
                .Select((action, index) => (action, index))
                .Where(pair => ExpectedResults.States(pair.action, [result]))
                .Select(pair => steps.FirstOrDefault(step => step.Order == pair.index + 1)?.Status)
                .ToList();

            checks[result.Text] = statuses.Count == 0 ? StatedCheck.NotChecked
                : statuses.Any(status => status == StepStatus.Failed) ? StatedCheck.Failed
                : statuses.All(status => status == StepStatus.Passed) ? StatedCheck.Passed
                : StatedCheck.NotReached;
        }

        return checks;
    }

    /// <summary>A planner's score on one stated result, given what the baseline found on the same variant.</summary>
    public static StatedScore Score(StatedCheck planner, bool defectOn, StatedCheck? baseline)
    {
        if (planner == StatedCheck.NotChecked)
        {
            return StatedScore.NotChecked;
        }

        bool? broken = !defectOn ? false
            : baseline switch
            {
                StatedCheck.Failed => true,
                StatedCheck.Passed => false,
                _ => null,
            };

        return (broken, planner) switch
        {
            // Neither the plan nor the baseline got that far (an earlier step had failed): there is nothing to score.
            (null, _) => StatedScore.NotScored,
            (_, StatedCheck.NotReached) => StatedScore.Inconclusive,
            (false, StatedCheck.Passed) => StatedScore.Confirmed,
            (false, _) => StatedScore.WrongFail,
            (true, StatedCheck.Failed) => StatedScore.Caught,
            (true, _) => StatedScore.FalsePass,
        };
    }

    /// <summary>Every planner score in a run, each stated result of each measurement once (its first execution).</summary>
    public static IReadOnlyList<(Measurement Measurement, string Text, StatedScore Score)> Scores(IReadOnlyList<Measurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        var scores = new List<(Measurement, string, StatedScore)>();

        foreach (var measurement in measurements.Where(m => m.Source != "baseline" && m.Stated is not null))
        {
            var baseline = measurements.FirstOrDefault(m => m.Source == "baseline"
                && m.Scenario.Id == measurement.Scenario.Id && m.DefectOn == measurement.DefectOn)?.Stated;

            foreach (var (text, check) in measurement.Stated!) // not null: filtered above.
            {
                StatedCheck? truth = baseline is not null && baseline.TryGetValue(text, out var found) ? found : null;
                scores.Add((measurement, text, Score(check, measurement.DefectOn, truth)));
            }
        }

        return scores;
    }
}
