using Autestiq.Contracts.Execution;
using Autestiq.Domain.Common;

namespace Autestiq.Domain.Execution;

/// <summary>One execution of a scenario against a target.</summary>
public sealed class TestRun : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The project the run belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The run's number within its project: what people say and paste into chat.</summary>
    public int Number { get; set; }

    /// <summary>Which scripted scenario ran.</summary>
    public required string ScenarioId { get; set; }

    /// <summary>The one origin the scenario ran against.</summary>
    public required string TargetOrigin { get; set; }

    /// <summary>Where it is, or how it ended.</summary>
    public RunStatus Status { get; set; } = RunStatus.Queued;

    /// <summary>1 for a first run, 2 for its first rerun, and so on.</summary>
    public int Attempt { get; set; } = 1;

    /// <summary>The run this one repeats, if it is a rerun.</summary>
    public Guid? RerunOf { get; set; }

    /// <summary>What went wrong, when something did.</summary>
    public string? FailureReason { get; set; }

    /// <summary>When it was asked for.</summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>When a worker picked it up.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When it stopped.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Browser seconds held against the organization's monthly quota while the run goes: its whole time budget, set
    /// when it is claimed and released when its result is written back. Counted only while the run is running, so a
    /// run that is reaped or cancelled releases its reservation with nothing else to do.
    /// </summary>
    public long ReservedSeconds { get; set; }

    /// <summary>
    /// What the run cost us, in the price book's currency: the sum of its cost rows, written in the same save as they
    /// are (ADR-0029). <c>null</c> until its result is written back.
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>Who asked for the run, for the per-person limit on starts. Empty for a run nobody started by hand.</summary>
    public Guid? RequestedBy { get; set; }

    /// <summary>
    /// When the dispatcher carrying the run out last said it still was. A running run whose last word is older
    /// than the lease has lost its dispatcher, and is reaped as an infrastructure error rather than left running.
    /// </summary>
    public DateTimeOffset? HeartbeatAt { get; set; }

    /// <summary>Set when a person asks for the run to stop; the dispatcher acts on it.</summary>
    public bool CancellationRequested { get; set; }

    /// <summary>What the browser fetched, for the record.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>The steps, in order.</summary>
    public ICollection<TestRunStep> Steps { get; } = [];
}
