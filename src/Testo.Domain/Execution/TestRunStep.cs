using Testo.Contracts.Execution;
using Testo.Domain.Common;

namespace Testo.Domain.Execution;

/// <summary>One step of a run, as it happened.</summary>
public sealed class TestRunStep : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The run.</summary>
    public Guid TestRunId { get; set; }

    /// <summary>Position, from 1.</summary>
    public int Order { get; set; }

    /// <summary>What the step does, in words.</summary>
    public required string Description { get; set; }

    /// <summary>What became of it.</summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>When it started, if it did.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When it ended, if it did.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>What the worker said about it.</summary>
    public string? Message { get; set; }

    /// <summary>The screenshot taken at the end of the step, if one was.</summary>
    public Guid? ScreenshotArtifactId { get; set; }
}
