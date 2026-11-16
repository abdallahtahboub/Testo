using Autestiq.Application.Usage;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Execution;

namespace Autestiq.Application.Tests.Usage;

/// <summary>The rules of what a run used and what of it is billable (Milestone 3.6, decision B).</summary>
public class UsageMeterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static WorkerResult Result(RunStatus status, long bytes, double seconds = 10) =>
        new(Guid.CreateVersion7(), status, null, Start, Start.AddSeconds(seconds), [], bytes);

    [Theory]
    [InlineData(RunStatus.Passed, 1_000, false, true)]
    [InlineData(RunStatus.Failed, 1_000, false, true)]               // the application's failure is billable
    [InlineData(RunStatus.TimedOut, 1_000, false, true)]             // so is a test too slow for its budget
    [InlineData(RunStatus.Cancelled, 1_000, false, true)]            // a person stopped a run that had started
    [InlineData(RunStatus.InfrastructureError, 1_000, false, false)] // ours
    [InlineData(RunStatus.Cancelled, 1_000, true, false)]            // the operator's kill switch is ours too
    [InlineData(RunStatus.Failed, 0, false, false)]                  // never reached the application
    public void IsBillable_OnlyWhatReachedTheApplicationAndDidNotEndByOurFault(RunStatus status, long bytes, bool byOperator, bool expected)
    {
        Assert.Equal(expected, UsageMeter.IsBillable(Result(status, bytes), byOperator));
    }

    [Theory]
    [InlineData(10.0, 10)]
    [InlineData(10.2, 11)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    public void BrowserSeconds_AreExact_RoundedUpToTheSecond(double seconds, long expected)
    {
        Assert.Equal(expected, UsageMeter.BrowserSecondsOf(Result(RunStatus.Passed, 1, seconds)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(5_999, 100)]
    public void MinutesFor_RoundsTheMonthUpOnce(long seconds, long minutes)
    {
        Assert.Equal(minutes, UsageMeter.MinutesFor(seconds));
    }

    [Fact]
    public void ForRun_GivesOneEventPerCategory_WithKeysThatNameTheRun()
    {
        var run = new TestRun { OrganizationId = Guid.CreateVersion7(), ProjectId = Guid.CreateVersion7(), ScenarioId = "basket", TargetOrigin = "x" };

        var events = UsageMeter.ForRun(run, Result(RunStatus.Passed, 1_000, seconds: 42), billable: true, artifactBytes: 2_048, Start);

        Assert.Equal([UsageCategory.Run, UsageCategory.BrowserSeconds, UsageCategory.ArtifactBytes], events.Select(usage => usage.Category));
        Assert.Equal([1L, 42L, 2_048L], events.Select(usage => usage.Quantity));
        Assert.All(events, usage => Assert.Equal("2026-11", usage.Period));
        Assert.All(events, usage => Assert.True(usage.Billable));
        Assert.Equal(UsageMeter.ForRun(run, Result(RunStatus.Passed, 1), true, 0, Start)[0].IdempotencyKey, events[0].IdempotencyKey);
    }
}
