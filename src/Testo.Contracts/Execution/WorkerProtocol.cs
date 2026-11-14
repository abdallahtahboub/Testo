namespace Testo.Contracts.Execution;

/// <summary>
/// What the application hands a worker. Deliberately the whole of what the worker knows.
/// </summary>
/// <remarks>
/// There is no organization in here, and that is the point (ADR-0018). The worker returns a result
/// naming its run; the application re-reads the organization from the stored run. A worker that a
/// hostile page fully owns learns this job and nothing about any other customer.
/// </remarks>
/// <param name="RunId">The run, which is all the worker needs to name itself by.</param>
/// <param name="ScenarioId">Which scripted scenario to run.</param>
/// <param name="TargetOrigin">The one origin the scenario runs against, such as <c>http://127.0.0.1:5090</c>.</param>
/// <param name="AllowedOrigins">Every origin the browser may reach. Everything else is refused by the proxy.</param>
/// <param name="TrustedLocalOrigins">
/// Origins we ship ourselves that are allowed to resolve to a local address. A customer-named
/// target is never in here; the demo application is.
/// </param>
/// <param name="Budget">What the run may spend before it is stopped.</param>
/// <param name="ArtifactDirectory">Where to write screenshots. The application collects them afterwards.</param>
public sealed record WorkerJob(
    Guid RunId,
    string ScenarioId,
    string TargetOrigin,
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> TrustedLocalOrigins,
    RunBudget Budget,
    string ArtifactDirectory);

/// <summary>What a run may spend. Exceeding any one of these ends the run with a status that says which.</summary>
/// <param name="WallClock">How long the whole run may take.</param>
/// <param name="MaxSteps">How many steps it may execute.</param>
/// <param name="MaxBytesDownloaded">How much the browser may fetch in total.</param>
public sealed record RunBudget(TimeSpan WallClock, int MaxSteps, long MaxBytesDownloaded)
{
    /// <summary>What a run gets unless told otherwise.</summary>
    public static RunBudget Default { get; } = new(TimeSpan.FromMinutes(2), 100, 50 * 1024 * 1024);
}

/// <summary>What a worker hands back.</summary>
/// <param name="RunId">The run this is the result of. The application checks it against the job it sent.</param>
/// <param name="Status">How the run ended. Never <see cref="RunStatus.Queued"/> or <see cref="RunStatus.Running"/>.</param>
/// <param name="FailureReason">What went wrong, when something did.</param>
/// <param name="StartedAt">When the worker began.</param>
/// <param name="FinishedAt">When it stopped.</param>
/// <param name="Steps">Every step, in order, including the ones skipped after a failure.</param>
/// <param name="BytesDownloaded">What the browser fetched, for the budget and the record.</param>
public sealed record WorkerResult(
    Guid RunId,
    RunStatus Status,
    string? FailureReason,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<WorkerStepResult> Steps,
    long BytesDownloaded);

/// <summary>One step, as the worker saw it.</summary>
/// <param name="Order">Position, from 1.</param>
/// <param name="Description">What the step does, in words.</param>
/// <param name="Status">What became of it.</param>
/// <param name="StartedAt">When it started, if it did.</param>
/// <param name="FinishedAt">When it ended, if it did.</param>
/// <param name="Message">What the worker has to say about it, usually only on failure.</param>
/// <param name="ScreenshotFile">
/// The screenshot's file name inside the job's artifact directory, when one was taken. Never a
/// path: the application decides where things live.
/// </param>
public sealed record WorkerStepResult(
    int Order,
    string Description,
    StepStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Message,
    string? ScreenshotFile);
