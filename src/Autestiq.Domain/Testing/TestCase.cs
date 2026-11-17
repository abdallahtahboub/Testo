using Autestiq.Contracts.Testing;
using Autestiq.Domain.Common;

namespace Autestiq.Domain.Testing;

/// <summary>
/// A test a person described in a sentence, and the plan the AI wrote for it (Milestone 5, part 2). The exploration
/// that the plan rests on is an ordinary run (<see cref="Execution.TestRun.TestCaseId"/>), so every gate a run passes
/// applies to it.
/// </summary>
public sealed class TestCase : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The project it belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The environment it is explored and planned against.</summary>
    public Guid EnvironmentId { get; set; }

    /// <summary>Numbered within the project.</summary>
    public int Number { get; set; }

    /// <summary>What the person asked for, in their words.</summary>
    public required string Description { get; set; }

    /// <summary>Where it stands.</summary>
    public TestCaseStatus Status { get; set; } = TestCaseStatus.Exploring;

    /// <summary>A resource key, when it failed.</summary>
    public string? FailureReason { get; set; }

    /// <summary>The exploration's result, as JSON: what the planner is shown. Customer data.</summary>
    public string? Snapshot { get; set; }

    /// <summary>The plan, as JSON, once planned.</summary>
    public string? Plan { get; set; }

    /// <summary>The notes about the plan, with their sources, as JSON.</summary>
    public string? Notes { get; set; }

    /// <summary>Exactly what was sent to the model about the application, per attempt, as JSON.</summary>
    public string? SentToModel { get; set; }

    /// <summary>The model that planned it.</summary>
    public string? Model { get; set; }

    /// <summary>Tokens spent planning.</summary>
    public int? ModelTokens { get; set; }

    /// <summary>What planning cost, in US dollars, at the price book's prices. Counted against the AI budgets.</summary>
    public decimal? ModelCost { get; set; }

    /// <summary>When the planning step took it, so two planners never take the same one.</summary>
    public DateTimeOffset? PlanningStartedAt { get; set; }

    /// <summary>When planning ended, with a plan or without; the month it counts against.</summary>
    public DateTimeOffset? PlannedAt { get; set; }

    /// <summary>Who described it.</summary>
    public Guid? RequestedBy { get; set; }

    /// <summary>When an Owner or QA lead approved the plan to run (ADR-0030, part 3); null until then, or once withdrawn.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Who approved it.</summary>
    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// The SHA-256 of the plan that was approved, in hex. A run carries out only a plan with this fingerprint, so an
    /// approval is of one plan and never of whatever the test holds later.
    /// </summary>
    public string? ApprovedPlanHash { get; set; }

    /// <summary>
    /// Whether the approval allows changing data: given only with an explicit acknowledgement, and required for a plan
    /// that may change data. Without it a run is read-only, held in the browser by the worker.
    /// </summary>
    public bool ApprovedToChangeData { get; set; }
}
