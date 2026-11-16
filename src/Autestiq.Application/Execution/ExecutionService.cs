using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Autestiq.Application.Audit;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Projects;
using Autestiq.Persistence;

namespace Autestiq.Application.Execution;

/// <summary>Starting, stopping and reading test runs.</summary>
/// <remarks>
/// Everything here reads through the tenant filter, so a run id from another organization does not
/// exist rather than being refused — which is the behaviour that cannot be got wrong by forgetting
/// a check.
/// </remarks>
internal sealed class ExecutionService(
    AutestiqDbContext database,
    ArtifactLinks links,
    IOptionsMonitor<ExecutionOptions> options,
    IOptionsMonitor<Usage.UsageOptions> usage,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
    : IExecutionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Contracts.Execution.Scenarios.All);

    /// <inheritdoc />
    public async Task<Guid?> GetCurrentProjectAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return null;
        }

        // Through the tenant filter, so this is the organization's own project and no other.
        return await database.Projects
            .Where(project => project.Status == ProjectStatus.Active)
            .OrderBy(project => project.Name)
            .Select(project => (Guid?)project.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TestRunSummary>> ListRunsAsync(
        Guid projectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return PagedResult.Empty<TestRunSummary>();
        }

        var query = database.TestRuns
            .Where(run => run.ProjectId == projectId)
            .OrderByDescending(run => run.QueuedAt);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var runs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(run => Summarize(run))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TestRunSummary>(runs, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<TestRunDetail?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return null;
        }

        var run = await database.TestRuns
            .Include(candidate => candidate.Steps)
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        // Read once, so that a run with twenty steps asks one question rather than twenty.
        var screenshots = await database.Artifacts
            .Where(artifact => artifact.TestRunId == runId && artifact.Kind == ArtifactKind.Screenshot)
            .ToDictionaryAsync(artifact => artifact.Id, cancellationToken)
            .ConfigureAwait(false);

        var steps = run.Steps
            .OrderBy(step => step.Order)
            .Select(step => new TestRunStepDetail(
                step.Order,
                step.Description,
                step.Status,
                step.StartedAt,
                step.FinishedAt,
                step.Message,
                Link(step.ScreenshotArtifactId, screenshots)))
            .ToList();

        return new TestRunDetail(Summarize(run), run.TargetOrigin, run.FailureReason, run.RerunOf, steps);
    }

    /// <inheritdoc />
    public async Task<OperationResult<Guid>> StartRunAsync(
        Guid projectId,
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.RunTests))
        {
            return OperationResult.Failure<Guid>("Execution_Error_NotAllowed");
        }

        if (!Contracts.Execution.Scenarios.Exists(scenarioId))
        {
            return OperationResult.Failure<Guid>("Execution_Error_NoSuchScenario");
        }

        var project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure<Guid>("Execution_Error_NoSuchProject");
        }

        if (options.CurrentValue.IsPaused(project.OrganizationId))
        {
            return OperationResult.Failure<Guid>("Execution_Error_Paused");
        }

        if (await UsedUpAsync(project.OrganizationId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Execution_Error_MinutesUsedUp");
        }

        return OperationResult.With(
            await QueueAsync(project.Id, scenarioId, attempt: 1, rerunOf: null, cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<OperationResult> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.RunTests))
        {
            return OperationResult.Failure("Execution_Error_NotAllowed");
        }

        var run = await database.TestRuns
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return OperationResult.Failure("Execution_Error_NoSuchRun");
        }

        if (run.Status.IsFinished())
        {
            return OperationResult.Failure("Execution_Error_AlreadyFinished");
        }

        // The dispatcher notices and stops the worker. A run that has not started yet never will.
        run.CancellationRequested = true;

        if (run.Status == RunStatus.Queued)
        {
            run.Status = RunStatus.Cancelled;
            run.FinishedAt = timeProvider.GetUtcNow();
        }

        Record(AuditActions.RunCancelled, run);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult<Guid>> RerunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.RunTests))
        {
            return OperationResult.Failure<Guid>("Execution_Error_NotAllowed");
        }

        var original = await database.TestRuns
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (original is null)
        {
            return OperationResult.Failure<Guid>("Execution_Error_NoSuchRun");
        }

        if (options.CurrentValue.IsPaused(original.OrganizationId))
        {
            return OperationResult.Failure<Guid>("Execution_Error_Paused");
        }

        if (await UsedUpAsync(original.OrganizationId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Execution_Error_MinutesUsedUp");
        }

        return OperationResult.With(
            await QueueAsync(
                    original.ProjectId,
                    original.ScenarioId,
                    original.Attempt + 1,
                    original.RerunOf ?? original.Id,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task<Guid> QueueAsync(
        Guid projectId,
        string scenarioId,
        int attempt,
        Guid? rerunOf,
        CancellationToken cancellationToken)
    {
        var organizationId = organizationContext.RequireOrganizationId();

        // Numbered within the project, which is how people refer to a run out loud.
        var lastNumber = await database.TestRuns
            .Where(run => run.ProjectId == projectId)
            .MaxAsync(run => (int?)run.Number, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var run = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = lastNumber + 1,
            ScenarioId = scenarioId,
            TargetOrigin = options.CurrentValue.TargetOrigin,
            Status = RunStatus.Queued,
            Attempt = attempt,
            RerunOf = rerunOf,
            QueuedAt = timeProvider.GetUtcNow(),
        };

        database.TestRuns.Add(run);
        Record(AuditActions.RunStarted, run);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return run.Id;
    }

    /// <summary>Whether the organization has used all of this month's browser minutes: the hard stop (ADR-0027).</summary>
    private async Task<bool> UsedUpAsync(Guid organizationId, CancellationToken cancellationToken) =>
        (await RunDispatcher.RoomForAsync(database, organizationId, usage.CurrentValue, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)).UsedUp;

    private void Record(string action, TestRun run) =>
        new AuditWriter(database, timeProvider).Record(
            run.OrganizationId,
            currentUser.UserId,
            currentUser.Email,
            action,
            nameof(TestRun),
            run.Id,
            $"{run.ScenarioId} #{run.Number}");

    private static TestRunSummary Summarize(TestRun run) => new(
        run.Id,
        run.Number,
        run.ScenarioId,
        Contracts.Execution.Scenarios.NameOf(run.ScenarioId),
        run.Status,
        run.Attempt,
        run.CreatedBy?.ToString() ?? "",
        run.QueuedAt,
        run.StartedAt,
        run.FinishedAt);

    private ArtifactLink? Link(Guid? artifactId, Dictionary<Guid, Domain.Execution.Artifact> screenshots) =>
        artifactId is { } id && screenshots.TryGetValue(id, out var artifact)
            ? new ArtifactLink(
                id,
                $"/app/artifacts/{id}?token={links.Sign(id)}",
                artifact.ContentType,
                links.ExpiresAt())
            : null;
}

/// <summary>How runs are carried out.</summary>
public sealed class ExecutionOptions
{
    /// <summary>The configuration section this is bound from.</summary>
    public const string SectionName = "Execution";

    /// <summary>
    /// Where runs point. One origin, ours, until proof of origin control exists and a customer may
    /// name their own (ADR-0018).
    /// </summary>
    public string TargetOrigin { get; set; } = "http://127.0.0.1:5090";

    /// <summary>
    /// Our own applications on the worker's machine, which a run may reach on loopback. Configuration only:
    /// a run's stored target is never added (R-11). Empty means the one configured <see cref="TargetOrigin"/>.
    /// </summary>
    public List<string> LocalOrigins { get; init; } = [];

    /// <summary>The origins a run may reach on loopback: <see cref="LocalOrigins"/>, or the configured target.</summary>
    public IReadOnlyList<string> TrustedLocalOrigins() => LocalOrigins.Count > 0 ? [.. LocalOrigins] : [TargetOrigin];

    /// <summary>
    /// The kill switch for every organization: no run starts, none is claimed, and running ones are stopped.
    /// Read live, so changing the configuration takes effect without a restart.
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>The kill switch for some organizations only. The others carry on.</summary>
    public List<Guid> PausedOrganizations { get; init; } = [];

    /// <summary>Whether an organization's runs are stopped, by either switch.</summary>
    public bool IsPaused(Guid organizationId) => Paused || PausedOrganizations.Contains(organizationId);

    /// <summary>How often a dispatcher says a run it is carrying out is still going.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a running run may go without a heartbeat before it is taken to have lost its dispatcher and is
    /// reaped. Several heartbeats long, so that one slow write does not end a healthy run.
    /// </summary>
    public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How long a verified proof of origin control counts before the origin must be checked again.</summary>
    public TimeSpan ProofValidity { get; set; } = TimeSpan.FromDays(30);

    /// <summary>How often a dispatcher looks for runs that lost theirs.</summary>
    public TimeSpan ReapInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How workers run (ADR-0026). <see cref="WorkerIsolation.Container"/> is required before any origin that is not
    /// ours may be a target; <see cref="WorkerIsolation.Process"/> is for development, tests and the benchmark.
    /// </summary>
    public WorkerIsolation Isolation { get; set; } = WorkerIsolation.Process;

    /// <summary>The worker image, for <see cref="WorkerIsolation.Container"/>.</summary>
    public string WorkerImage { get; set; } = "autestiq-worker:latest";

    /// <summary>The Docker command line, for <see cref="WorkerIsolation.Container"/>.</summary>
    public string Docker { get; set; } = "docker";

    /// <summary>Where the worker executable is, for the dispatcher to launch.</summary>
    public string? WorkerPath { get; set; }

    /// <summary>How many runs may be carried out at once.</summary>
    public int MaxConcurrentRuns { get; set; } = 2;
}
