namespace Testo.Benchmark;

/// <summary>
/// Checks the instrument before trusting what it measures.
/// </summary>
/// <remarks>
/// <para>
/// A report that says "0 false positives" only means something if a false positive is a thing this
/// harness can see. So: take a scenario with a real defect, switch the defect on, give the harness
/// a planner that writes a plan designed to pass without testing anything, and insist that the
/// harness calls it a false positive.
/// </para>
/// <para>
/// It needs no model and no key, so CI runs it on every change. If the day comes when this stops
/// failing the lazy planner, the benchmark's reassuring numbers are the first thing to disbelieve.
/// </para>
/// </remarks>
internal static class SelfCheck
{
    /// <summary>Runs the check. True when the harness behaved.</summary>
    public static async Task<bool> RunAsync(string origin)
    {
        // A scenario with a defect to find, so that "passed" is the wrong answer.
        var scenario = BenchmarkScenarios.All.First(candidate => candidate.Defect is not null);

        Console.WriteLine($"Self-check: a planner that only pretends, against '{scenario.Id}' with its defect on.");

        var harness = new Harness(origin, new LazyPlanner(), repetitions: 1, recordingDirectory: null);

        using var stopping = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var measurements = await harness.RunAsync([scenario], stopping.Token).ConfigureAwait(false);

        var pretending = measurements
            .Where(measurement => measurement.DefectOn && !string.Equals(measurement.Source, "baseline", StringComparison.Ordinal))
            .ToList();

        var baseline = measurements
            .Where(measurement => measurement.DefectOn && string.Equals(measurement.Source, "baseline", StringComparison.Ordinal))
            .ToList();

        var caught = pretending.Count > 0 && pretending.All(measurement => measurement.Verdict == Verdict.FalsePositive);
        var baselineCorrect = baseline.Count > 0 && baseline.All(measurement => measurement.Verdict == Verdict.Correct);

        Console.WriteLine(caught
            ? "  The pretending plan was recorded as a false positive. The instrument works."
            : "  The pretending plan was NOT recorded as a false positive. Every number this harness "
              + "reports is now suspect.");

        Console.WriteLine(baselineCorrect
            ? "  The hand-written plan found the defect, as it should."
            : "  The hand-written plan did NOT find the defect, so the shop or the executor is wrong.");

        return caught && baselineCorrect;
    }
}
