using Testo.Contracts.Execution;
using Testo.Contracts.Planning;

namespace Testo.Application.Tests.Planning;

/// <summary>
/// The rule that a partial plan does not report a pass.
/// </summary>
/// <remarks>
/// It exists because the first measured benchmark run produced exactly this: plans that said, in
/// their own notes, that they tested half of what was asked, passed that half, and were reported
/// as passes. An assurance wider than its evidence is the mistake this product is against, and a
/// polite admission in prose that nobody reads is not a safeguard.
/// </remarks>
public class PlanOutcomeTests
{
    [Fact]
    public void Reconcile_APartialPlanThatPassed_IsInconclusive()
    {
        Assert.Equal(
            RunStatus.Inconclusive,
            PlanOutcome.Reconcile(RunStatus.Passed, PlanCoverage.Partial));
    }

    [Fact]
    public void Reconcile_AFullPlanThatPassed_IsStillAPass()
    {
        Assert.Equal(RunStatus.Passed, PlanOutcome.Reconcile(RunStatus.Passed, PlanCoverage.Full));
    }

    [Fact]
    public void Reconcile_APartialPlanThatFailed_IsStillAFailure()
    {
        // The half it did test is broken, which is a real finding about the application. Turning
        // it into "inconclusive" would hide a defect to make a point about coverage.
        Assert.Equal(RunStatus.Failed, PlanOutcome.Reconcile(RunStatus.Failed, PlanCoverage.Partial));
    }

    [Theory]
    [InlineData(RunStatus.TimedOut)]
    [InlineData(RunStatus.BudgetExceeded)]
    [InlineData(RunStatus.InfrastructureError)]
    [InlineData(RunStatus.Cancelled)]
    public void Reconcile_AnEndingThatSaysNothingEitherWay_IsLeftAlone(RunStatus status)
    {
        Assert.Equal(status, PlanOutcome.Reconcile(status, PlanCoverage.Partial));
    }
}
