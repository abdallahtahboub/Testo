using Testo.Contracts.Common;

namespace Testo.Contracts.Execution;

/// <summary>
/// Starting, stopping and reading test runs. Everything here is scoped to the current
/// organization by the tenant filter; a run id from another organization simply does not exist.
/// </summary>
public interface IExecutionService
{
    /// <summary>The scenarios a person may start. Scripted and fixed in this milestone.</summary>
    Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The project the run pages work with: the organization's first by name, or <c>null</c> when
    /// the organization has none.
    /// </summary>
    /// <remarks>
    /// A project switcher arrives with the app shell in Milestone 5. Until then a page still needs
    /// somewhere to ask which project it is looking at, and asking here keeps that one question in
    /// one place instead of spreading a guess across pages.
    /// </remarks>
    Task<Guid?> GetCurrentProjectAsync(CancellationToken cancellationToken = default);

    /// <summary>The runs of a project, newest first.</summary>
    Task<PagedResult<TestRunSummary>> ListRunsAsync(
        Guid projectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One run with its steps, or <c>null</c> when there is no such run in this organization.</summary>
    Task<TestRunDetail?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a run. Requires <see cref="Identity.Permission.RunTests"/>. Returns the run's id.
    /// </summary>
    Task<OperationResult<Guid>> StartRunAsync(
        Guid projectId,
        string scenarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Stops a run that is queued or running. Requires <see cref="Identity.Permission.RunTests"/>.</summary>
    Task<OperationResult> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Queues the same scenario again, as the next attempt of the given run.</summary>
    Task<OperationResult<Guid>> RerunAsync(Guid runId, CancellationToken cancellationToken = default);
}
