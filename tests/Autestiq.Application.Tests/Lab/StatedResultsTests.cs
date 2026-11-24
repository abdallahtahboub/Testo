using Autestiq.Benchmark;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Application.Tests.Lab;

/// <summary>
/// Scoring each stated expected result on its own (Milestone 5.1 R5): which step checks it, how that step ended, and
/// what the baseline says the truth is. A false pass must never be counted as anything else.
/// </summary>
public class StatedResultsTests
{
    private static readonly PlannedAction[] Plan =
    [
        new(ActionKind.Goto, "Open the basket", Path: "/basket"),
        new(ActionKind.AssertText, "The total", ActionTarget.ByTestId("total"), Text: "£42.75"),
        new(ActionKind.AssertVisible, "The thank-you", ActionTarget.ByText("Thanks for your order")),
    ];

    private static WorkerStepResult Step(int order, StepStatus status) => new(order, "step", status, null, null, null, null);

    [Fact]
    public void Check_FindsTheStepsThatStateEachResult_AndHowTheyEnded()
    {
        ExpectedResult[] expected = [new("£42.75"), new("Thanks for your order"), new("Order number")];
        var steps = new[] { Step(1, StepStatus.Passed), Step(2, StepStatus.Failed), Step(3, StepStatus.Skipped) };

        var checks = StatedResults.Check(expected, Plan, steps);

        Assert.Equal(StatedCheck.Failed, checks["£42.75"]);
        Assert.Equal(StatedCheck.NotReached, checks["Thanks for your order"]);
        Assert.Equal(StatedCheck.NotChecked, checks["Order number"]);
    }

    [Theory]
    [InlineData(StatedCheck.Passed, false, StatedCheck.Passed, StatedScore.Confirmed)]
    [InlineData(StatedCheck.Failed, false, StatedCheck.Passed, StatedScore.WrongFail)]
    [InlineData(StatedCheck.Failed, true, StatedCheck.Failed, StatedScore.Caught)]
    [InlineData(StatedCheck.Passed, true, StatedCheck.Failed, StatedScore.FalsePass)]
    [InlineData(StatedCheck.Passed, true, StatedCheck.Passed, StatedScore.Confirmed)]
    [InlineData(StatedCheck.Failed, true, StatedCheck.Passed, StatedScore.WrongFail)]
    [InlineData(StatedCheck.Passed, true, StatedCheck.NotReached, StatedScore.NotScored)]
    [InlineData(StatedCheck.NotChecked, true, StatedCheck.Failed, StatedScore.NotChecked)]
    [InlineData(StatedCheck.NotReached, false, StatedCheck.Passed, StatedScore.Inconclusive)]
    [InlineData(StatedCheck.NotReached, true, StatedCheck.Failed, StatedScore.Inconclusive)]
    [InlineData(StatedCheck.NotReached, true, StatedCheck.NotReached, StatedScore.NotScored)]
    public void Score_JudgesAgainstTheBaseline(StatedCheck planner, bool defectOn, StatedCheck baseline, StatedScore expected)
    {
        Assert.Equal(expected, StatedResults.Score(planner, defectOn, baseline));
    }

    [Fact]
    public void Score_OnTheSoundApplication_NeverNeedsTheBaseline()
    {
        // Every stated result holds where there is no defect.
        Assert.Equal(StatedScore.Confirmed, StatedResults.Score(StatedCheck.Passed, defectOn: false, baseline: null));
    }

    [Fact]
    public void Stated_DerivesTheResultsFromWhatTheBaselineChecks_ByRule()
    {
        var scenario = new BenchmarkScenario("basket", "Check the basket", Plan);

        var derived = Assert.Single(StatedResults.Stated([scenario]));

        Assert.Equal("basket+stated", derived.Id);
        Assert.Equal([new ExpectedResult("£42.75")], derived.Expected);
        Assert.Equal(scenario.Ask, derived.Ask);
        Assert.Equal(scenario.Baseline, derived.Baseline);
    }

    [Fact]
    public void Stated_KeepsResultsAScenarioAlreadyStates()
    {
        var scenario = new BenchmarkScenario("order", "Place an order", Plan, Expected: [new ExpectedResult("Thanks for your order", "/done")]);

        Assert.Equal(scenario.Expected, Assert.Single(StatedResults.Stated([scenario])).Expected);
    }

    [Fact]
    public void Provenance_SaysWhatWasMeasured_AndFlagsUncommittedChanges()
    {
        var markdown = new Provenance("abc123", Clean: false, Tag: null, "claude-sonnet-5-5", "0123456789AB", "acting, with test ids", "seeded").Markdown();

        Assert.Contains("`abc123` **with uncommitted changes**", markdown, StringComparison.Ordinal);
        Assert.Contains("not a release-candidate measurement", markdown, StringComparison.Ordinal);
        Assert.Contains("`0123456789AB`", markdown, StringComparison.Ordinal);
    }
}
