namespace Testo.Contracts.Execution;

/// <summary>
/// Where a test run is, or how it ended. The names are stored, so they are assigned explicitly
/// and never reordered.
/// </summary>
/// <remarks>
/// Every way a run can stop has its own name. A run that hit its budget is not "failed", and a run
/// the infrastructure lost is not "failed" either; calling them that would make the failure rate
/// a lie about the application under test.
/// </remarks>
public enum RunStatus
{
    /// <summary>Waiting for a worker.</summary>
    Queued = 1,

    /// <summary>A worker has it.</summary>
    Running = 2,

    /// <summary>Every step passed.</summary>
    Passed = 3,

    /// <summary>A step failed: the application under test did not do what the scenario expected.</summary>
    Failed = 4,

    /// <summary>A person stopped it.</summary>
    Cancelled = 5,

    /// <summary>It ran out of wall-clock time.</summary>
    TimedOut = 6,

    /// <summary>It exceeded a budget other than time: steps, bytes, tokens or money.</summary>
    BudgetExceeded = 7,

    /// <summary>The worker, the proxy or the host failed. Nothing is known about the application under test.</summary>
    InfrastructureError = 8,
}

/// <summary>What became of one step.</summary>
public enum StepStatus
{
    /// <summary>Not reached yet.</summary>
    Pending = 1,

    /// <summary>In progress.</summary>
    Running = 2,

    /// <summary>Did what it set out to do.</summary>
    Passed = 3,

    /// <summary>Did not.</summary>
    Failed = 4,

    /// <summary>Not run, because an earlier step failed or the run was stopped.</summary>
    Skipped = 5,
}

/// <summary>Whether a run can be stopped or started again from where it is.</summary>
public static class RunStatusRules
{
    /// <summary>Whether a run in this status is still going.</summary>
    public static bool IsActive(this RunStatus status) => status is RunStatus.Queued or RunStatus.Running;

    /// <summary>Whether a run in this status has stopped, however it stopped.</summary>
    public static bool IsFinished(this RunStatus status) => !status.IsActive();
}
