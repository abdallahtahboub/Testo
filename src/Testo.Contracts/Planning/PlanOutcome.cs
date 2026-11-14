using Testo.Contracts.Execution;

namespace Testo.Contracts.Planning;

/// <summary>
/// Reconciles what a run reported with how much of the scenario its plan covered.
/// </summary>
/// <remarks>
/// <para>
/// In <c>Contracts</c> because every caller that executes a plan needs the same answer, and two
/// copies of this rule would eventually be one copy and one bug. It is a pure function of two
/// values: no logging, no services, nothing to configure.
/// </para>
/// <para>
/// Only a pass is affected. A partial plan that fails has still found something real — the half it
/// did test is broken — and a run that timed out or could not start says nothing either way.
/// </para>
/// </remarks>
public static class PlanOutcome
{
    /// <summary>The status a run should end with, given its plan's own account of its coverage.</summary>
    public static RunStatus Reconcile(RunStatus status, PlanCoverage coverage) =>
        status == RunStatus.Passed && coverage == PlanCoverage.Partial
            ? RunStatus.Inconclusive
            : status;
}
