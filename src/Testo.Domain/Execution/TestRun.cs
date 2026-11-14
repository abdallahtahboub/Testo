using Testo.Contracts.Execution;
using Testo.Domain.Common;

namespace Testo.Domain.Execution;

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

    /// <summary>Set when a person asks for the run to stop; the dispatcher acts on it.</summary>
    public bool CancellationRequested { get; set; }

    /// <summary>What the browser fetched, for the record.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>The steps, in order.</summary>
    public ICollection<TestRunStep> Steps { get; } = [];
}
