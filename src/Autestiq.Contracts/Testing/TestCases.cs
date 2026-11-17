using Autestiq.Contracts.Common;
using Autestiq.Contracts.Planning;

namespace Autestiq.Contracts.Testing;

/// <summary>Where a described test stands (Milestone 5, part 2).</summary>
public enum TestCaseStatus
{
    /// <summary>The site is being explored, so that something can be planned against it.</summary>
    Exploring = 1,

    /// <summary>Explored; the AI is writing the plan.</summary>
    Planning = 2,

    /// <summary>A plan exists and passed the validator. It runs only once a person approves it (part 3).</summary>
    Planned = 3,

    /// <summary>No plan; the reason says why.</summary>
    Failed = 4,
}

/// <summary>Where a note about a plan comes from, so that observed, documented and assumed are never mixed.</summary>
public enum PlanNoteSource
{
    /// <summary>Checked by Autestiq's own software against what the exploration saw.</summary>
    CheckedByAutestiq = 1,

    /// <summary>Written by the AI: its reasoning, assumptions and what it noticed. Unverified until someone checks it.</summary>
    Ai = 2,
}

/// <summary>A note about a plan, and where it comes from.</summary>
public sealed record PlanNote(string Text, PlanNoteSource Source);

/// <summary>A described test, as a list shows it.</summary>
public sealed record TestCaseSummary(Guid TestCaseId, int Number, string Description, string EnvironmentName, TestCaseStatus Status, DateTimeOffset CreatedAt);

/// <summary>A described test with its plan.</summary>
/// <param name="TestCaseId">Its id.</param>
/// <param name="Number">Its number within the project.</param>
/// <param name="Description">What the person asked for, in their words: the documented expectation.</param>
/// <param name="EnvironmentName">The environment it was explored against.</param>
/// <param name="Origin">That environment's origin.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="FailureReason">A resource key, when it failed.</param>
/// <param name="Steps">The plan's steps, once planned.</param>
/// <param name="Coverage">Whether the plan covers the whole description.</param>
/// <param name="Notes">The notes about the plan, each with its source.</param>
/// <param name="Model">The model that planned it.</param>
/// <param name="Cost">What planning cost, in US dollars, at the price book's prices.</param>
/// <param name="SentToModel">Exactly the text about the application that was sent to the model, for each attempt.</param>
/// <param name="ExplorationRunId">The run that explored the site.</param>
/// <param name="StepsThatMayChangeData">The 1-based numbers of the plan's steps that may change data (<see cref="PlanEffects"/>).</param>
/// <param name="NeedsSecret">Whether the plan types a secret, which no plan may do until a secret store exists.</param>
/// <param name="Approval">The plan's approval, or <c>null</c> when it has none.</param>
/// <param name="Runs">The runs of the approved plan, newest first.</param>
/// <param name="Explored">
/// What exploring did on the site, when it was allowed to act (ADR-0032): each control pressed and form sent, as the
/// explorer recorded them, in the order first done. Empty when exploring only followed links.
/// </param>
public sealed record TestCaseDetail(
    Guid TestCaseId,
    int Number,
    string Description,
    string EnvironmentName,
    string Origin,
    TestCaseStatus Status,
    string? FailureReason,
    IReadOnlyList<PlannedAction> Steps,
    PlanCoverage Coverage,
    IReadOnlyList<PlanNote> Notes,
    string? Model,
    decimal? Cost,
    IReadOnlyList<string> SentToModel,
    Guid? ExplorationRunId,
    IReadOnlyList<int> StepsThatMayChangeData,
    bool NeedsSecret,
    PlanApproval? Approval,
    IReadOnlyList<TestCaseRun> Runs,
    IReadOnlyList<string> Explored);

/// <summary>Who approved a plan, when, and whether the approval allows changing data.</summary>
public sealed record PlanApproval(DateTimeOffset ApprovedAt, string? ApprovedBy, bool ToChangeData);

/// <summary>One run of an approved plan.</summary>
public sealed record TestCaseRun(Guid RunId, int Number, Execution.RunStatus Status, DateTimeOffset QueuedAt, DateTimeOffset? FinishedAt);

/// <summary>
/// Describing a test and getting the AI's plan for it (Milestone 5, part 2). Describing needs
/// <see cref="Identity.Permission.ManageTestCases"/>, the project's AI processing permission, an environment whose
/// kind allows runs, and room in the AI budgets. Failures are resource keys.
/// </summary>
public interface ITestCaseService
{
    /// <summary>A project's described tests, newest first.</summary>
    Task<IReadOnlyList<TestCaseSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>One described test, or <c>null</c> when this organization has none with that id.</summary>
    Task<TestCaseDetail?> GetAsync(Guid testCaseId, CancellationToken cancellationToken = default);

    /// <summary>Describes a test: queues an exploration of the environment, after which the AI plans it.</summary>
    Task<OperationResult<Guid>> DescribeAsync(Guid projectId, Guid environmentId, string description, CancellationToken cancellationToken = default);
    /// <summary>
    /// Approves the test's plan to run (Milestone 5, part 3). Needs <see cref="Identity.Permission.ApproveTestArtifacts"/>
    /// (Owner, QA lead). Refused for a plan that is partial or types a secret, and for a plan that may change data unless
    /// <paramref name="allowChangingData"/> says the approver accepts that.
    /// </summary>
    Task<OperationResult> ApproveAsync(Guid testCaseId, bool allowChangingData, CancellationToken cancellationToken = default);

    /// <summary>Withdraws a plan's approval, so it runs no more until approved again.</summary>
    Task<OperationResult> WithdrawApprovalAsync(Guid testCaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a run of the approved plan. Needs <see cref="Identity.Permission.RunTests"/> and the same site, quota and
    /// rate checks any run has. The dispatcher checks the approval again before a browser starts.
    /// </summary>
    Task<OperationResult<Guid>> RunAsync(Guid testCaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The approved plan as a test file to download (Milestone 5, part 4), written by the fixed templates of
    /// <see cref="Export.ITestExporter"/> with a fixed header saying what was approved. Needs
    /// <see cref="Identity.Permission.Read"/> and a plan that is approved and still the plan that was approved. Audited.
    /// </summary>
    Task<OperationResult<Export.ExportedTest>> DownloadAsync(Guid testCaseId, CancellationToken cancellationToken = default);
}
