using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Autestiq.Application.Execution;
using Autestiq.Application.Testing;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Testing;

namespace Autestiq.Application.Tests.Testing;

/// <summary>
/// Describing a test and planning it (Milestone 5, part 2; ADR-0030): every gate before anything is queued, and the
/// planning step's own checks before the model is called.
/// </summary>
public sealed class DescribedTestTests : IAsyncLifetime
{
    /// <summary>Our demo shop's address, which the default options trust as one of our own applications.</summary>
    private const string OurShop = "http://127.0.0.1:5090";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private TestDatabase _database = null!;
    private Guid _northwind;
    private Guid _contoso;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_northwind, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        (_contoso, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Contoso");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private async Task<T> AsAsync<T>(Guid organization, Role role, Func<IServiceProvider, Task<T>> act, Action<IServiceCollection>? configure = null)
    {
        await using var services = _database.NewServices(configure);
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);

        using var scope = services.CreateScope();
        return await act(scope.ServiceProvider);
    }

    /// <summary>A project with one environment, the AI permission given or not.</summary>
    private async Task<(Guid Project, Guid Environment)> ProjectAsync(Guid organization, bool aiAllowed, string origin = OurShop, EnvironmentKind kind = EnvironmentKind.Test)
    {
        return await AsAsync(organization, Role.Owner, async provider =>
        {
            var projects = provider.GetRequiredService<IProjectService>();
            await projects.CreateAsync(new NewProject("Shop", "SHOP", null));
            var project = (await projects.FindAsync("SHOP"))!.ProjectId;
            var environment = (await projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", origin, kind))).Value;

            if (aiAllowed)
            {
                Assert.True((await projects.AllowAiProcessingAsync(project, acknowledged: true)).Succeeded);
            }

            return (project, environment);
        });
    }

    private Task<Contracts.Common.OperationResult<Guid>> DescribeAsync(Guid organization, Guid project, Guid environment, Role role = Role.Owner, string text = "Check the basket adds up") =>
        AsAsync(organization, role, provider => provider.GetRequiredService<ITestCaseService>().DescribeAsync(project, environment, text));

    [Fact]
    public async Task Describe_OurOwnShop_WithEverythingAllowed_QueuesAnExplorationRunForTheTest()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);

        var described = await DescribeAsync(_northwind, project, environment, Role.QaEngineer);

        Assert.True(described.Succeeded, string.Join(", ", described.Errors));

        await using var database = _database.NewContext();
        var testCase = await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == described.Value, TestContext.Current.CancellationToken);
        var run = await database.TestRuns.IgnoreQueryFilters().SingleAsync(candidate => candidate.TestCaseId == testCase.Id, TestContext.Current.CancellationToken);

        Assert.Equal((TestCaseStatus.Exploring, 1, "Check the basket adds up"), (testCase.Status, testCase.Number, testCase.Description));
        Assert.Equal((Scenarios.Exploration, OurShop, RunStatus.Queued), (run.ScenarioId, run.TargetOrigin, run.Status));
    }

    [Fact]
    public async Task Describe_WithoutTheProjectsAiPermission_IsRefused_AndQueuesNothing()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: false);

        Assert.Equal(["Tests_Error_AiNotAllowed"], (await DescribeAsync(_northwind, project, environment)).Errors);

        await using var database = _database.NewContext();
        Assert.False(await database.TestRuns.IgnoreQueryFilters().AnyAsync(run => run.OrganizationId == _northwind, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task Describe_BySomebodyWhoCannotManageTestCases_IsRefused(Role role)
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);

        Assert.Equal(["Tests_Error_NotAllowed"], (await DescribeAsync(_northwind, project, environment, role)).Errors);
    }

    [Fact]
    public async Task Describe_AnUnauthorizedProductionSite_IsRefused()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true, "https://shop.example", EnvironmentKind.Production);

        Assert.Equal(["Tests_Error_ProductionNotAuthorized"], (await DescribeAsync(_northwind, project, environment)).Errors);
    }

    [Fact]
    public async Task Describe_ACustomerSiteThatIsNotProvedAndSealed_IsRefused()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true, "https://shop.example");

        Assert.Equal(["Tests_Error_SiteNotAllowed"], (await DescribeAsync(_northwind, project, environment)).Errors);
    }

    [Fact]
    public async Task Describe_WithNothingToSay_IsRefused()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);

        Assert.Equal(["Tests_Error_DescriptionRequired"], (await DescribeAsync(_northwind, project, environment, text: "  ")).Errors);
    }

    [Fact]
    public async Task Describe_WhenTheOrganizationHasSpentItsMonthsAiBudget_IsRefused()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);
        await SpentAsync(_northwind, project, environment, 2.00m);

        Assert.Equal(["Tests_Error_AiBudgetSpent"], (await DescribeAsync(_northwind, project, environment)).Errors);
    }

    [Fact]
    public async Task Describe_WhenTheProductHasSpentItsMonthsAiBudget_IsRefusedForEveryone()
    {
        var (contosoProject, contosoEnvironment) = await ProjectAsync(_contoso, aiAllowed: true);
        await SpentAsync(_contoso, contosoProject, contosoEnvironment, 1.50m);
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);

        // Contoso spent 1.50 of the product's 1.00 this month; Northwind, which spent nothing, is refused too.
        var result = await AsAsync(_northwind, Role.Owner, provider => provider.GetRequiredService<ITestCaseService>().DescribeAsync(project, environment, "Check it"),
            services => services.Configure<PlanningOptions>(options => options.MonthlyBudgetUsd = 1.00m));

        Assert.Equal(["Tests_Error_AiBudgetSpent"], result.Errors);
    }

    // --- The planning step ---

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
        ["I assumed the catalogue is the start page."]);

    /// <summary>A test whose exploration has ended, with what it found or nothing.</summary>
    private async Task<Guid> ExploredAsync(Guid organization, bool aiAllowed, SiteSnapshot? found)
    {
        var (project, environment) = await ProjectAsync(organization, aiAllowed: true);
        var testCase = (await DescribeAsync(organization, project, environment)).Value;

        await using var database = _database.NewContext();
        var stored = await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == testCase, TestContext.Current.CancellationToken);
        var run = await database.TestRuns.IgnoreQueryFilters().SingleAsync(candidate => candidate.TestCaseId == testCase, TestContext.Current.CancellationToken);
        var projectRow = await database.Projects.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == project, TestContext.Current.CancellationToken);

        run.Status = found is null ? RunStatus.Cancelled : RunStatus.Passed;
        stored.Snapshot = found is null ? null : JsonSerializer.Serialize(found, Web);

        if (!aiAllowed)
        {
            projectRow.AiProcessingAllowedAt = null;
        }

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return testCase;
    }

    private async Task<(TestCase TestCase, FakePlanners Planners)> PlanAsync(Guid testCase, bool withPlanner = true, Action<PlanningOptions>? options = null)
    {
        var planners = new FakePlanners(withPlanner ? new FakePlanner(GoodPlan) : null);

        await using var services = _database.NewServices(collection => collection.AddSingleton<IPlannerFactory>(planners));
        var planning = new PlanningOptions();
        options?.Invoke(planning);

        var step = new PlanningDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedOptions<PlanningOptions>(planning),
            TimeProvider.System,
            NullLogger<PlanningDispatcher>.Instance);

        Assert.True(await step.PlanNextAsync(TestContext.Current.CancellationToken));

        await using var database = _database.NewContext();
        return (await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == testCase, TestContext.Current.CancellationToken), planners);
    }

    [Fact]
    public async Task Plan_AnExploredTest_StoresThePlan_TheNotesBySource_WhatWasSent_AndWhatItCost()
    {
        var testCase = await ExploredAsync(_northwind, aiAllowed: true, Shop);

        var (planned, planners) = await PlanAsync(testCase);

        Assert.Equal(TestCaseStatus.Planned, planned.Status);
        Assert.Equal((2.00m, 10.00m), planners.PricesAsked);  // the price book's Claude Sonnet 5.5 prices
        Assert.Equal(0.05m, planned.ModelCost);
        Assert.Equal(["SENT: Check the basket adds up"], JsonSerializer.Deserialize<List<string>>(planned.SentToModel!));

        var notes = JsonSerializer.Deserialize<List<PlanNote>>(planned.Notes!, Web)!;
        Assert.Contains(notes, note => note.Source == PlanNoteSource.Ai && note.Text.StartsWith("I assumed", StringComparison.Ordinal));

        await using var services = _database.NewServices();
        _database.Principal.Become(_northwind, Guid.CreateVersion7(), Role.Viewer);
        using var scope = services.CreateScope();
        var detail = await scope.ServiceProvider.GetRequiredService<ITestCaseService>().GetAsync(testCase, TestContext.Current.CancellationToken);
        Assert.Equal(2, detail!.Steps.Count);
    }

    [Fact]
    public async Task Plan_AnExplorationThatFoundNothing_FailsWithoutCallingTheModel()
    {
        var testCase = await ExploredAsync(_northwind, aiAllowed: true, found: null);

        var (planned, planners) = await PlanAsync(testCase);

        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_ExplorationFailed"), (planned.Status, planned.FailureReason));
        Assert.Equal(0, planners.Calls);
    }

    [Fact]
    public async Task Plan_WhenThePermissionWasWithdrawnWhileExploring_FailsWithoutCallingTheModel()
    {
        var testCase = await ExploredAsync(_northwind, aiAllowed: false, Shop);

        var (planned, planners) = await PlanAsync(testCase);

        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_AiNotAllowed"), (planned.Status, planned.FailureReason));
        Assert.Equal(0, planners.Calls);
    }

    [Fact]
    public async Task Plan_WhenTheBudgetIsSpent_FailsWithoutCallingTheModel()
    {
        var testCase = await ExploredAsync(_northwind, aiAllowed: true, Shop);

        var (planned, planners) = await PlanAsync(testCase, options: options => options.OrganizationMonthlyBudgetUsd = 0m);

        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_AiBudgetSpent"), (planned.Status, planned.FailureReason));
        Assert.Equal(0, planners.Calls);
    }

    [Fact]
    public async Task Plan_WithNoKeyConfigured_FailsSayingThereIsNoPlanner()
    {
        var testCase = await ExploredAsync(_northwind, aiAllowed: true, Shop);

        var (planned, _) = await PlanAsync(testCase, withPlanner: false);

        Assert.Equal((TestCaseStatus.Failed, "Tests_Error_NoPlanner"), (planned.Status, planned.FailureReason));
    }

    [Fact]
    public async Task AnExploration_CarriedOutByTheDispatcher_HandsWhatItFoundToTheTest_WhichIsThenPlanned()
    {
        var (project, environment) = await ProjectAsync(_northwind, aiAllowed: true);
        var testCase = (await DescribeAsync(_northwind, project, environment)).Value;

        await using (var services = _database.NewServices())
        {
            var options = new ChangeableOptions(new ExecutionOptions
            {
                WorkerPath = ExecutionSupport.ExploringWorker(JsonSerializer.Serialize(Shop, Web)),
                MaxConcurrentRuns = 1,
            });
            var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);

            await using var database = _database.NewContext();
            var run = await database.TestRuns.IgnoreQueryFilters().SingleAsync(candidate => candidate.TestCaseId == testCase, TestContext.Current.CancellationToken);

            await dispatcher.StartAsync(TestContext.Current.CancellationToken);

            try
            {
                Assert.Equal(RunStatus.Passed, await ExecutionSupport.StatusOnceItIsAsync(_database, run.Id, RunStatus.Passed, TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await dispatcher.StopAsync(CancellationToken.None);
            }
        }

        var (planned, planners) = await PlanAsync(testCase);

        Assert.Equal(TestCaseStatus.Planned, planned.Status);
        Assert.Equal(1, planners.Calls);
        Assert.Contains("add-pot", planned.Snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExplorationRun_IsGivenAnExplorationJob_WithItsLongerBudget()
    {
        var run = new Domain.Execution.TestRun { ScenarioId = Scenarios.Exploration, TargetOrigin = OurShop, TestCaseId = Guid.CreateVersion7() };

        var job = RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x");

        Assert.Equal((WorkerTask.Explore, true, true, 24), (job.Task, job.ExploreStates, job.SubmitForms, job.Budget.MaxPagesCrawled));
        Assert.Equal(TimeSpan.FromMinutes(10), job.Budget.WallClock);
    }

    private async Task SpentAsync(Guid organization, Guid project, Guid environment, decimal cost)
    {
        await using var database = _database.NewContext();
        database.TestCases.Add(new TestCase
        {
            OrganizationId = organization,
            ProjectId = project,
            EnvironmentId = environment,
            Number = 99,
            Description = "earlier",
            Status = TestCaseStatus.Planned,
            ModelCost = cost,
            PlannedAt = DateTimeOffset.UtcNow,
        });

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakePlanners(IScenarioPlanner? planner) : IPlannerFactory
    {
        public (decimal Input, decimal Output) PricesAsked { get; private set; }

        public int Calls => planner is FakePlanner fake ? fake.Calls : 0;

        public IScenarioPlanner? Create(string model, decimal inputPerMillion, decimal outputPerMillion)
        {
            PricesAsked = (inputPerMillion, outputPerMillion);
            return planner;
        }
    }

    private sealed class FakePlanner(TestPlan plan) : IScenarioPlanner
    {
        public int Calls { get; private set; }

        public string Model => "claude-sonnet-5-5";

        public Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(PlanResult.From(plan, PlanningCost.None with { InputTokens = 10_000, OutputTokens = 3_000, Attempts = 1, Cost = 0.05m }, Model) with { Sent = "SENT: " + request.Scenario });
        }
    }
}
