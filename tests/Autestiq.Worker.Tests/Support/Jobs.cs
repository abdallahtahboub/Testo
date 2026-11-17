using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// Builds the three kinds of job and runs them, so a test says what it is about rather than how a
/// job is assembled.
/// </summary>
internal static class Jobs
{
    /// <summary>Runs one of the scenarios we wrote.</summary>
    public static Task<WorkerResult> ScriptedAsync(string origin, string scenarioId, RunBudget? budget = null) =>
        RunAsync(Job(origin, scenarioId, WorkerTask.Scripted, budget), out _);

    /// <summary>Runs one of the scenarios we wrote, and says where its evidence went.</summary>
    public static Task<WorkerResult> ScriptedAsync(string origin, string scenarioId, out string artifacts, RunBudget? budget = null) =>
        RunAsync(Job(origin, scenarioId, WorkerTask.Scripted, budget), out artifacts);

    /// <summary>Walks the application and describes it.</summary>
    public static Task<WorkerResult> ExploreAsync(
        string origin,
        RunBudget? budget = null,
        bool exploreStates = false,
        bool submitForms = false,
        Func<Task>? startAfresh = null,
        string? named = null,
        CancellationToken? cancellation = null) =>
        new ScenarioRunner(TimeProvider.System) { StartAfresh = startAfresh }.RunAsync(
            Job(named ?? origin, "explore", WorkerTask.Explore, budget) with { ExploreStates = exploreStates, SubmitForms = submitForms },
            cancellation ?? TestContext.Current.CancellationToken);

    /// <summary>Carries out a plan.</summary>
    public static Task<WorkerResult> PlannedAsync(
        string origin,
        IReadOnlyList<PlannedAction> plan,
        IReadOnlyDictionary<string, string>? secrets = null,
        RunBudget? budget = null) =>
        RunAsync(Job(origin, "planned", WorkerTask.Planned, budget) with { Plan = plan, Secrets = secrets }, out _);

    /// <summary>Carries out a plan that was approved as one that does not change data.</summary>
    public static Task<WorkerResult> ReadOnlyAsync(string origin, IReadOnlyList<PlannedAction> plan) =>
        RunAsync(Job(origin, "planned", WorkerTask.Planned, null) with { Plan = plan, ReadOnly = true }, out _);

    /// <summary>Carries out a plan, and says where its evidence went.</summary>
    public static Task<WorkerResult> PlannedAsync(
        string origin,
        IReadOnlyList<PlannedAction> plan,
        out string artifacts,
        IReadOnlyDictionary<string, string>? secrets = null) =>
        RunAsync(Job(origin, "planned", WorkerTask.Planned, null) with { Plan = plan, Secrets = secrets }, out artifacts);

    /// <summary>Checks an origin's proof file. Trusted-local only when a test says so: a real job never is.</summary>
    public static Task<WorkerResult> VerifyAsync(string origin, string proof, bool trustLocal) =>
        RunAsync(
            Job(origin, "verify-origin", WorkerTask.VerifyOrigin, null) with
            {
                OriginProof = proof,
                AllowedOrigins = trustLocal ? [] : [origin],
                TrustedLocalOrigins = trustLocal ? [origin] : [],
            },
            out _);

    private static WorkerJob Job(string origin, string scenarioId, WorkerTask task, RunBudget? budget) => new(
        RunId: Guid.CreateVersion7(),
        scenarioId,
        origin,
        AllowedOrigins: [],

        // Ours, loopback, and named as trusted by the job. A customer-supplied target never is.
        TrustedLocalOrigins: [origin],
        budget ?? RunBudget.Default,
        ArtifactDirectory: Directory.CreateTempSubdirectory("autestiq-worker-test-").FullName,
        task);

    private static Task<WorkerResult> RunAsync(WorkerJob job, out string artifacts)
    {
        artifacts = job.ArtifactDirectory;
        return new ScenarioRunner(TimeProvider.System).RunAsync(job, TestContext.Current.CancellationToken);
    }
}
