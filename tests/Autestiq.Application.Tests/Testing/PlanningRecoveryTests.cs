using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Testing;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Testing;
using Autestiq.Persistence;

namespace Autestiq.Application.Tests.Testing;

/// <summary>
/// Milestone 5.1 R1 (R-17): every planning call is recorded as an attempt before the model is asked and counts at its
/// ceiling until its outcome is known; a test left in Planning past its lease is failed and never planned again on its
/// own; and one planning step per deployment holds the lock. Crashes are simulated where they can fall: before the
/// call, after a call that may have been billed, and while the answer is being saved.
/// </summary>
public sealed class PlanningRecoveryTests : IAsyncLifetime
{
    private const string OurShop = "http://127.0.0.1:5090";
    private const decimal Ceiling = 0.25m;   // RunBudget.Default.MaxCostUsd, with the month's budget larger
    private const decimal Answer = 0.05m;    // what the fake planner reports

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly SiteSnapshot Shop = new(
        OurShop,
        [new PageSnapshot("/", "Catalogue", "Catalogue", [new SnapshotElement("button", "Add to basket", "add-pot", NamedBy: TargetKind.TestId)])],
        Truncated: false);

    private static readonly TestPlan GoodPlan = new(
        "Basket",
        [
            new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new PlannedAction(ActionKind.AssertVisible, "The add button is there", ActionTarget.ByTestId("add-pot")),
        ],
        []);

    private readonly MovableClock _clock = new(DateTimeOffset.UtcNow);
    private TestDatabase _database = null!;
    private Guid _northwind;
    private (Guid Project, Guid Environment)? _place;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_northwind, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Plan_RecordsAnAttemptBeforeTheCall_AndItsAnsweredOutcomeAfter()
    {
        var testCase = await ExploredAsync();
        var planner = new ScriptedPlanner(_ => Task.CompletedTask);

        using (var step = Step(planner, out var services))
        await using (services)
        {
            Assert.True(await step.PlanNextAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, planner.AttemptsSeenDuringCall);  // the attempt row existed while the model was being asked
        }

        await using var database = _database.NewContext();
        var attempt = await database.PlanningAttempts.IgnoreQueryFilters().SingleAsync(row => row.TestCaseId == testCase, TestContext.Current.CancellationToken);
        var outcome = await database.PlanningAttemptOutcomes.IgnoreQueryFilters().SingleAsync(row => row.AttemptId == attempt.Id, TestContext.Current.CancellationToken);

        Assert.Equal((_northwind, Ceiling), (attempt.OrganizationId, attempt.CeilingUsd));
        Assert.Equal((PlanningAttemptEnd.Answered, Answer), (outcome.End, outcome.CostUsd));
        Assert.Equal(TestCaseStatus.Planned, (await TestCaseAsync(testCase)).Status);
    }

    [Fact]
    public async Task Budget_CountsAnAttemptWithoutAnOutcome_AtItsCeiling()
    {
        var testCase = await ExploredAsync();
        var planner = new ScriptedPlanner(_ => throw new HttpRequestException("the connection dropped after sending"));

        using (var step = Step(planner, out var services))
        await using (services)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        await using var database = _database.NewContext();
        var left = await AiBudget.LeftAsync(database, _northwind, new PlanningOptions(), _clock.GetUtcNow(), TestContext.Current.CancellationToken);

        Assert.Equal(new PlanningOptions().OrganizationMonthlyBudgetUsd - Ceiling, left);
        Assert.Equal(TestCaseStatus.Planning, (await TestCaseAsync(testCase)).Status);
    }

    [Fact]
    public async Task Recover_AfterACallThatMayHaveBeenBilled_FailsTheTest_CountsTheCeiling_AndDoesNotAskAgain()
    {
        var testCase = await ExploredAsync();
        var crashing = new ScriptedPlanner(_ => throw new HttpRequestException("the process died here"));

        using (var step = Step(crashing, out var services))
        await using (services)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        _clock.Advance(new PlanningOptions().Lease + TimeSpan.FromSeconds(1));
        var restarted = new ScriptedPlanner(_ => Task.CompletedTask);

        using (var step = Step(restarted, out var services))
        await using (services)
        {
            Assert.False(await step.PlanNextAsync(TestContext.Current.CancellationToken));  // nothing left to plan
        }

        var stored = await TestCaseAsync(testCase);
        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_PlanningInterrupted"), (stored.Status, stored.FailureReason));
        Assert.Equal(0, restarted.Calls);

        await using var database = _database.NewContext();
        var outcome = await database.PlanningAttemptOutcomes.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((PlanningAttemptEnd.Interrupted, Ceiling, (int?)null), (outcome.End, outcome.CostUsd, outcome.Tokens));
        Assert.True(await database.AuditEntries.IgnoreQueryFilters().AnyAsync(entry => entry.Action == "test.planning_interrupted" && entry.SubjectId == testCase, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Recover_AfterACrashBeforeTheCall_FailsTheTest_AndCountsNothing()
    {
        var testCase = await ExploredAsync();

        // The step saved the test as Planning and died before recording an attempt: no request went out.
        await using (var database = _database.NewContext())
        {
            var stored = await database.TestCases.IgnoreQueryFilters().SingleAsync(row => row.Id == testCase, TestContext.Current.CancellationToken);
            stored.Status = TestCaseStatus.Planning;
            stored.PlanningStartedAt = _clock.GetUtcNow();

            using (_database.WriteScope.AllowWritingTo(_northwind))
            {
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        _clock.Advance(new PlanningOptions().Lease + TimeSpan.FromSeconds(1));

        using (var step = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var services))
        await using (services)
        {
            await step.PlanNextAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(TestCaseStatus.Failed, (await TestCaseAsync(testCase)).Status);

        await using var check = _database.NewContext();
        Assert.False(await check.PlanningAttemptOutcomes.IgnoreQueryFilters().AnyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new PlanningOptions().OrganizationMonthlyBudgetUsd, await AiBudget.LeftAsync(check, _northwind, new PlanningOptions(), _clock.GetUtcNow(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Recover_BeforeTheLeaseRunsOut_LeavesAPlanningTestAlone()
    {
        var testCase = await ExploredAsync();

        using (var step = Step(new ScriptedPlanner(_ => throw new HttpRequestException("died")), out var services))
        await using (services)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        _clock.Advance(new PlanningOptions().Lease - TimeSpan.FromSeconds(1));

        using (var step = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var services))
        await using (services)
        {
            await step.PlanNextAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(TestCaseStatus.Planning, (await TestCaseAsync(testCase)).Status);
    }

    [Fact]
    public async Task AnAnswerArrivingAfterRecovery_IsNotKept_AndTheAttemptEndsOnce()
    {
        var testCase = await ExploredAsync();
        PlanningDispatcher? recoverer = null;

        // While the model "answers", the lease runs out and recovery fails the test, as another instance after a
        // restart would. The answer that then arrives must not overwrite that.
        var slow = new ScriptedPlanner(async _ =>
        {
            _clock.Advance(new PlanningOptions().Lease + TimeSpan.FromSeconds(1));

            await using var services = _database.NewServices();
            using var scope = services.CreateScope();
            await recoverer!.RecoverAsync(
                scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(),
                scope.ServiceProvider.GetRequiredService<TenantWriteScope>(),
                TestContext.Current.CancellationToken);
        });

        using (recoverer = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var recovererServices))
        await using (recovererServices)
        using (var step = Step(slow, out var services))
        await using (services)
        {
            Assert.True(await step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        var stored = await TestCaseAsync(testCase);
        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_PlanningInterrupted", (string?)null), (stored.Status, stored.FailureReason, stored.Plan));

        await using var database = _database.NewContext();
        var outcome = await database.PlanningAttemptOutcomes.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((PlanningAttemptEnd.Interrupted, Ceiling), (outcome.End, outcome.CostUsd));
    }

    [Fact]
    public async Task ASecondPlanningStep_DoesNotPlan_WhileTheFirstHoldsTheLock_AndTakesOverWhenItStops()
    {
        var first = await ExploredAsync();
        var waiting = await ExploredAsync();
        var second = new ScriptedPlanner(_ => Task.CompletedTask);

        await using var secondServices = _database.NewServices(collection => collection.AddSingleton<IPlannerFactory>(new Planners(second)));
        using var secondStep = new PlanningDispatcher(
            secondServices.GetRequiredService<IServiceScopeFactory>(),
            new FixedOptions<PlanningOptions>(new PlanningOptions()),
            _clock,
            NullLogger<PlanningDispatcher>.Instance);

        using (var firstStep = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var services))
        await using (services)
        {
            // The first takes the lock with its first plan and holds it while it lives.
            Assert.True(await firstStep.PlanNextAsync(TestContext.Current.CancellationToken));
            Assert.Equal(TestCaseStatus.Planned, (await TestCaseAsync(first)).Status);

            Assert.False(await secondStep.PlanNextAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, second.Calls);
            Assert.Equal(TestCaseStatus.Exploring, (await TestCaseAsync(waiting)).Status);
        }

        // The first stopped: its connection closed and PostgreSQL released the lock.
        Assert.True(await secondStep.PlanNextAsync(TestContext.Current.CancellationToken));
        Assert.Equal((1, TestCaseStatus.Planned), (second.Calls, (await TestCaseAsync(waiting)).Status));
    }

    [Fact]
    public async Task APlanMadeAgain_CountsBothAttempts_NotOnlyTheLatest()
    {
        var testCase = await ExploredAsync();

        using (var step = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var services))
        await using (services)
        {
            Assert.True(await step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        // New expected results send it back to be planned from the same exploration.
        _database.Principal.Become(_northwind, Guid.CreateVersion7(), Role.Owner);
        await using (var services = _database.NewServices())
        {
            using var scope = services.CreateScope();
            var set = await scope.ServiceProvider.GetRequiredService<ITestCaseService>()
                .SetExpectedResultsAsync(testCase, [new ExpectedResult("Catalogue", null)], TestContext.Current.CancellationToken);
            Assert.True(set.Succeeded, string.Join(", ", set.Errors));
        }

        using (var step = Step(new ScriptedPlanner(_ => Task.CompletedTask), out var services))
        await using (services)
        {
            Assert.True(await step.PlanNextAsync(TestContext.Current.CancellationToken));
        }

        await using var database = _database.NewContext();
        Assert.Equal(
            new PlanningOptions().OrganizationMonthlyBudgetUsd - (2 * Answer),
            await AiBudget.LeftAsync(database, _northwind, new PlanningOptions(), _clock.GetUtcNow(), TestContext.Current.CancellationToken));
    }

    // --- Support ---

    private PlanningDispatcher Step(ScriptedPlanner planner, out ServiceProvider services)
    {
        services = _database.NewServices(collection => collection.AddSingleton<IPlannerFactory>(new Planners(planner)));
        planner.Database = _database;

        return new PlanningDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedOptions<PlanningOptions>(new PlanningOptions()),
            _clock,
            NullLogger<PlanningDispatcher>.Instance);
    }

    private async Task<TestCase> TestCaseAsync(Guid id)
    {
        await using var database = _database.NewContext();
        return await database.TestCases.IgnoreQueryFilters().SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);
    }

    /// <summary>A test on our own shop whose exploration has ended with something to plan from.</summary>
    private async Task<Guid> ExploredAsync()
    {
        _database.Principal.Become(_northwind, Guid.CreateVersion7(), Role.Owner);
        Guid testCase;

        await using (var services = _database.NewServices())
        {
            using var scope = services.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();
            if (_place is not { } place)
            {
                await projects.CreateAsync(new NewProject("Shop", "SHOP", null), TestContext.Current.CancellationToken);
                var created = (await projects.FindAsync("SHOP", TestContext.Current.CancellationToken))!.ProjectId;
                var staging = (await projects.AddEnvironmentAsync(created, new NewEnvironment("Staging", OurShop, EnvironmentKind.Test), TestContext.Current.CancellationToken)).Value;
                Assert.True((await projects.AllowAiProcessingAsync(created, acknowledged: true, TestContext.Current.CancellationToken)).Succeeded);
                _place = place = (created, staging);
            }

            var (project, environment) = place;
            var described = await scope.ServiceProvider.GetRequiredService<ITestCaseService>()
                .DescribeAsync(project, environment, "Check the basket adds up", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(described.Succeeded, string.Join(", ", described.Errors));
            testCase = described.Value;
        }

        await using var database = _database.NewContext();
        var stored = await database.TestCases.IgnoreQueryFilters().SingleAsync(row => row.Id == testCase, TestContext.Current.CancellationToken);
        var run = await database.TestRuns.IgnoreQueryFilters().SingleAsync(row => row.TestCaseId == testCase, TestContext.Current.CancellationToken);
        run.Status = RunStatus.Passed;
        stored.Snapshot = JsonSerializer.Serialize(Shop, Web);

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return testCase;
    }

    private sealed class Planners(IScenarioPlanner planner) : IPlannerFactory
    {
        public IScenarioPlanner? Create(string model, decimal inputPerMillion, decimal outputPerMillion) => planner;
    }

    /// <summary>A planner that runs a step of the test's choosing during the call, then answers with a good plan.</summary>
    private sealed class ScriptedPlanner(Func<PlanRequest, Task> during) : IScenarioPlanner
    {
        public int Calls { get; private set; }

        public int AttemptsSeenDuringCall { get; private set; }

        public TestDatabase? Database { get; set; }

        public string Model => "claude-sonnet-5-5";

        public async Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;

            if (Database is not null)
            {
                await using var database = Database.NewContext();
                AttemptsSeenDuringCall = await database.PlanningAttempts.IgnoreQueryFilters().CountAsync(cancellationToken);
            }

            await during(request);
            return PlanResult.From(GoodPlan, PlanningCost.None with { InputTokens = 10_000, OutputTokens = 3_000, Attempts = 1, Cost = Answer }, Model);
        }
    }

    /// <summary>A clock the test moves forward by hand.</summary>
    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
