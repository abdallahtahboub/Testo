namespace Testo.Contracts.Execution;

/// <summary>One row of the run list.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Number">The run's number within its project, as people refer to it.</param>
/// <param name="ScenarioId">Which scripted scenario it ran.</param>
/// <param name="ScenarioName">The scenario's name, for display.</param>
/// <param name="Status">Where it is, or how it ended.</param>
/// <param name="Attempt">1 for a first run, 2 for its first rerun, and so on.</param>
/// <param name="StartedBy">Who started it.</param>
/// <param name="QueuedAt">When it was asked for.</param>
/// <param name="StartedAt">When a worker picked it up.</param>
/// <param name="FinishedAt">When it stopped.</param>
public sealed record TestRunSummary(
    Guid RunId,
    int Number,
    string ScenarioId,
    string ScenarioName,
    RunStatus Status,
    int Attempt,
    string StartedBy,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

/// <summary>Everything the run page shows.</summary>
/// <param name="Summary">The row.</param>
/// <param name="TargetOrigin">Where it ran against.</param>
/// <param name="FailureReason">What went wrong, in words, when something did. A resource key or a message from the worker.</param>
/// <param name="RerunOf">The run this one repeats, if it is a rerun.</param>
/// <param name="Steps">Every step, in order.</param>
public sealed record TestRunDetail(
    TestRunSummary Summary,
    string TargetOrigin,
    string? FailureReason,
    Guid? RerunOf,
    IReadOnlyList<TestRunStepDetail> Steps);

/// <summary>One step as the run page shows it.</summary>
/// <param name="Order">Position, from 1.</param>
/// <param name="Description">What the step does, in words.</param>
/// <param name="Status">What became of it.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="FinishedAt">When it ended.</param>
/// <param name="Message">What the worker said about it, usually only when it failed.</param>
/// <param name="Screenshot">The screenshot taken at the end of the step, if one was.</param>
public sealed record TestRunStepDetail(
    int Order,
    string Description,
    StepStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Message,
    ArtifactLink? Screenshot);

/// <summary>
/// A way to reach one artifact, for a short while. The link is issued by the application after the
/// ordinary permission check and expires; the artifact itself is never addressable directly.
/// </summary>
/// <param name="ArtifactId">The artifact.</param>
/// <param name="Url">Where to fetch it from, until <paramref name="ExpiresAt"/>.</param>
/// <param name="ContentType">What it is.</param>
/// <param name="ExpiresAt">When the link stops working.</param>
public sealed record ArtifactLink(Guid ArtifactId, string Url, string ContentType, DateTimeOffset ExpiresAt);

/// <summary>A scenario a person may start.</summary>
/// <param name="ScenarioId">Its id, as stored on the run.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">What it does and what it checks.</param>
public sealed record ScenarioSummary(string ScenarioId, string Name, string Description);
