using Testo.Contracts.Planning;

namespace Testo.Contracts.Execution;

/// <summary>What a worker has been asked to do.</summary>
public enum WorkerTask
{
    /// <summary>Run one of the scenarios we wrote in C#. The deterministic baseline.</summary>
    Scripted = 1,

    /// <summary>
    /// Walk the target and describe what is there, so that something can be planned against it.
    /// No model is involved, which is what makes the input to planning reproducible (ADR-0019 §5).
    /// </summary>
    Explore = 2,

    /// <summary>Carry out a plan, action by action, with no model in the loop.</summary>
    Planned = 3,
}

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
/// <param name="Task">What kind of work this is. Scripted unless said otherwise.</param>
/// <param name="Plan">
/// The actions to carry out, for <see cref="WorkerTask.Planned"/>. Produced before the browser
/// opened and validated before it was sent, so the worker neither calls a model nor holds the key
/// to one: ADR-0018 treats the worker as already compromised, and ADR-0019 §4 keeps our model
/// account out of its reach.
/// </param>
/// <param name="Secrets">
/// The values this one run may type, by name, so that a plan can say "type the password" without
/// containing one. Never logged, never put in a step's message, and masked out of screenshots.
/// </param>
public sealed record WorkerJob(
    Guid RunId,
    string ScenarioId,
    string TargetOrigin,
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> TrustedLocalOrigins,
    RunBudget Budget,
    string ArtifactDirectory,
    WorkerTask Task = WorkerTask.Scripted,
    IReadOnlyList<PlannedAction>? Plan = null,
    IReadOnlyDictionary<string, string>? Secrets = null);

/// <summary>What a run may spend. Exceeding any one of these ends the run with a status that says which.</summary>
/// <param name="WallClock">How long the whole run may take.</param>
/// <param name="MaxSteps">How many steps it may execute.</param>
/// <param name="MaxBytesDownloaded">How much the browser may fetch in total.</param>
/// <param name="MaxModelTokens">
/// How many tokens planning may spend. A planner that loops is a denial of service against
/// ourselves, and the easiest one to cause by accident (threat model §4.7).
/// </param>
/// <param name="MaxCostUsd">What planning may cost, in US dollars, by the provider's price list.</param>
/// <param name="MaxPagesCrawled">How many pages the explore step may visit before it stops and says so.</param>
public sealed record RunBudget(
    TimeSpan WallClock,
    int MaxSteps,
    long MaxBytesDownloaded,
    int MaxModelTokens = 40_000,
    decimal MaxCostUsd = 0.25m,
    int MaxPagesCrawled = 8)
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
/// <param name="Snapshot">
/// What the crawl found, for a <see cref="WorkerTask.Explore"/> job. Untrusted input: every string
/// in it was written by whoever wrote the application under test.
/// </param>
public sealed record WorkerResult(
    Guid RunId,
    RunStatus Status,
    string? FailureReason,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<WorkerStepResult> Steps,
    long BytesDownloaded,
    SiteSnapshot? Snapshot = null);

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
