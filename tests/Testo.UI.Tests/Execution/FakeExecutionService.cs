using Testo.Contracts.Common;
using Testo.Contracts.Execution;

namespace Testo.UI.Tests.Execution;

/// <summary>
/// An in-test <see cref="IExecutionService"/>, as the testing rules ask: the page tests say what
/// the service answers, so they do not depend on seed data or on a database.
/// </summary>
internal sealed class FakeExecutionService : IExecutionService
{
    public Guid? CurrentProject { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public IReadOnlyList<ScenarioSummary> Scenarios { get; set; } =
        [new ScenarioSummary("demo-shop-basket", "Basket keeps its items", "Adds two items and checks the basket.")];

    public PagedResult<TestRunSummary> Runs { get; set; } = PagedResult.Empty<TestRunSummary>();

    public TestRunDetail? Run { get; set; }

    public OperationResult<Guid> StartAnswer { get; set; } = OperationResult.With(Guid.NewGuid());

    public OperationResult CancelAnswer { get; set; } = OperationResult.Success;

    public OperationResult<Guid> RerunAnswer { get; set; } = OperationResult.With(Guid.NewGuid());

    /// <summary>Every call, in order, so a test can prove a click reached the service once.</summary>
    public List<string> Calls { get; } = [];

    public Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Scenarios);

    public Task<Guid?> GetCurrentProjectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CurrentProject);

    public Task<PagedResult<TestRunSummary>> ListRunsAsync(
        Guid projectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"list:{projectId}");
        return Task.FromResult(Runs);
    }

    public Task<TestRunDetail?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"get:{runId}");
        return Task.FromResult(Run);
    }

    public Task<OperationResult<Guid>> StartRunAsync(
        Guid projectId,
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"start:{scenarioId}");
        return Task.FromResult(StartAnswer);
    }

    public Task<OperationResult> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"cancel:{runId}");
        return Task.FromResult(CancelAnswer);
    }

    public Task<OperationResult<Guid>> RerunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"rerun:{runId}");
        return Task.FromResult(RerunAnswer);
    }

    /// <summary>A finished run with one step, for the run page tests.</summary>
    public static TestRunDetail Finished(
        Guid runId,
        RunStatus status = RunStatus.Failed,
        string? failureReason = "Basket total was 53.25 and the checkout charged 42.00.") =>
        new(
            new TestRunSummary(
                runId,
                7,
                "demo-shop-checkout-total",
                "Checkout charges the basket total",
                status,
                Attempt: 1,
                StartedBy: "someone",
                QueuedAt: new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
                StartedAt: new DateTimeOffset(2026, 11, 13, 9, 0, 1, TimeSpan.Zero),
                FinishedAt: new DateTimeOffset(2026, 11, 13, 9, 0, 9, TimeSpan.Zero)),
            TargetOrigin: "http://127.0.0.1:5090",
            FailureReason: failureReason,
            RerunOf: null,
            Steps:
            [
                new TestRunStepDetail(
                    1,
                    "Open the shop",
                    StepStatus.Passed,
                    new DateTimeOffset(2026, 11, 13, 9, 0, 1, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 13, 9, 0, 3, TimeSpan.Zero),
                    Message: null,
                    Screenshot: new ArtifactLink(
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        "/app/artifacts/22222222-2222-2222-2222-222222222222?token=t",
                        "image/png",
                        new DateTimeOffset(2026, 11, 13, 9, 10, 0, TimeSpan.Zero))),
            ]);
}
