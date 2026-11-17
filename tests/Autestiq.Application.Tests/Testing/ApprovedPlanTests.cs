using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Testing;

namespace Autestiq.Application.Tests.Testing;

/// <summary>
/// Approving a test's plan and running it (Milestone 5, part 3; ADR-0030): who may approve, which plans may not be
/// approved, the explicit acknowledgement a plan that may change data needs, and the dispatcher's own check that only
/// the approved plan runs, read-only unless the approval says otherwise.
/// </summary>
public sealed class ApprovedPlanTests : IAsyncLifetime
{
    private const string OurShop = "http://127.0.0.1:5090";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly SiteSnapshot Shop = new(
        OurShop,
        [
            new PageSnapshot("/", "Catalogue", "Catalogue",
            [
                new SnapshotElement("link", "Basket", "basket-link"),
                new SnapshotElement("button", "Add to basket", "add-pot"),
                new SnapshotElement("heading", "Catalogue"),
            ]),
        ],
        Truncated: false);

    /// <summary>Opens the catalogue, follows a link and checks a heading: it only reads.</summary>
    private static readonly PlannedAction[] Reads =
    [
        new(ActionKind.Goto, "Open the catalogue", Path: "/"),
        new(ActionKind.Click, "Open the basket", ActionTarget.ByTestId("basket-link")),
        new(ActionKind.AssertVisible, "The heading is there", ActionTarget.ByRole("heading", "Catalogue")),
    ];

    /// <summary>Presses "Add to basket", a button: it may change data.</summary>
    private static readonly PlannedAction[] Changes =
    [
        new(ActionKind.Goto, "Open the catalogue", Path: "/"),
        new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
        new(ActionKind.AssertVisible, "The heading is there", ActionTarget.ByRole("heading", "Catalogue")),
    ];

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

    private async Task<T> AsAsync<T>(Guid organization, Role role, Func<ITestCaseService, Task<T>> act)
    {
        await using var services = _database.NewServices();
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);

        using var scope = services.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<ITestCaseService>());
    }

    /// <summary>A planned test of Northwind's shop, with the plan given, as the planning step would have stored it.</summary>
    private async Task<Guid> PlannedAsync(
        IReadOnlyList<PlannedAction> actions,
        PlanCoverage coverage = PlanCoverage.Full,
        TestCaseStatus status = TestCaseStatus.Planned,
        EnvironmentKind kind = EnvironmentKind.Test)
    {
        var (project, environment) = await AsAsync(_northwind, Role.Owner, async _ =>
        {
            await using var services = _database.NewServices();
            using var scope = services.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();
            await projects.CreateAsync(new NewProject("Shop", "SHOP", null));
            var id = (await projects.FindAsync("SHOP"))!.ProjectId;
            var added = await projects.AddEnvironmentAsync(id, new NewEnvironment($"Staging {Guid.CreateVersion7():N}"[..20], kind == EnvironmentKind.Test ? OurShop : "https://live.northwind.example", kind));
            return (id, added.Value);
        });

        await using var database = _database.NewContext();
        var testCase = new TestCase
        {
            OrganizationId = _northwind,
            ProjectId = project,
            EnvironmentId = environment,
            Number = Random.Shared.Next(1, 1_000_000),
            Description = "Check the catalogue",
            Status = status,
            Snapshot = JsonSerializer.Serialize(Shop, Web),
            Plan = JsonSerializer.Serialize(new { Actions = actions, Coverage = coverage }, Web),
            PlannedAt = DateTimeOffset.UtcNow,
        };
        database.TestCases.Add(testCase);

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return testCase.Id;
    }

    private async Task<TestCase> StoredAsync(Guid testCase)
    {
        await using var database = _database.NewContext();
        return await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == testCase, TestContext.Current.CancellationToken);
    }

    /// <summary>What the dispatcher would carry out for a run of the test: the approved plan, or nothing.</summary>
    private async Task<RunDispatcher.ApprovedPlan?> DispatchedAsync(Guid testCase)
    {
        await using var database = _database.NewContext();
        var run = new Domain.Execution.TestRun { OrganizationId = _northwind, ScenarioId = Scenarios.ApprovedPlan, TargetOrigin = OurShop, TestCaseId = testCase };
        return await RunDispatcher.ApprovedPlanAsync(database, run, TestContext.Current.CancellationToken);
    }

    // --- Approving ---

    [Fact]
    public async Task Approve_APlanThatOnlyReads_ByAQaLead_IsApproved_AndRunsReadOnly()
    {
        var testCase = await PlannedAsync(Reads);

        var approved = await AsAsync(_northwind, Role.QaLead, tests => tests.ApproveAsync(testCase, allowChangingData: false));

        Assert.True(approved.Succeeded, string.Join(", ", approved.Errors));
        var stored = await StoredAsync(testCase);
        Assert.NotNull(stored.ApprovedAt);
        Assert.Equal(64, stored.ApprovedPlanHash!.Length);

        var plan = await DispatchedAsync(testCase);
        Assert.NotNull(plan);
        Assert.True(plan.ReadOnly);

        var job = RunDispatcher.JobFor(
            new Domain.Execution.TestRun { ScenarioId = Scenarios.ApprovedPlan, TargetOrigin = OurShop, TestCaseId = testCase },
            new ExecutionOptions(), "/tmp/x", plan);
        Assert.Equal((WorkerTask.Planned, true, 3, false, false), (job.Task, job.ReadOnly, job.Plan!.Count, job.ExploreStates, job.SubmitForms));
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task Approve_BySomebodyWhoCannotApprove_IsRefused(Role role)
    {
        var testCase = await PlannedAsync(Reads);

        var approved = await AsAsync(_northwind, role, tests => tests.ApproveAsync(testCase, allowChangingData: true));

        Assert.Equal(["Tests_Error_NotApprover"], approved.Errors);
        Assert.Null((await StoredAsync(testCase)).ApprovedAt);
    }

    [Fact]
    public async Task Approve_APlanThatMayChangeData_WithoutSayingSo_IsRefused_AndWithIt_RunsAllowedToChangeData()
    {
        var testCase = await PlannedAsync(Changes);

        var refused = await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, allowChangingData: false));
        Assert.Equal(["Tests_Error_ChangeAcknowledgementRequired"], refused.Errors);
        Assert.Null(await DispatchedAsync(testCase));

        var approved = await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, allowChangingData: true));
        Assert.True(approved.Succeeded);

        var plan = await DispatchedAsync(testCase);
        Assert.NotNull(plan);
        Assert.False(plan.ReadOnly);
    }

    [Fact]
    public async Task Approve_APartialPlan_IsRefused()
    {
        var testCase = await PlannedAsync(Reads, PlanCoverage.Partial);

        Assert.Equal(["Tests_Error_PartialPlan"], (await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, true))).Errors);
    }

    [Fact]
    public async Task Approve_APlanThatTypesASecret_IsRefused_BecauseThereIsNowhereToKeepOne()
    {
        PlannedAction[] signIn = [.. Reads, new(ActionKind.Type, "Type the password", ActionTarget.ByTestId("password"), SecretName: "password")];
        var testCase = await PlannedAsync(signIn);

        Assert.Equal(["Tests_Error_NeedsSecret"], (await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, true))).Errors);
    }

    [Fact]
    public async Task Approve_ATestWithNoPlan_IsRefused()
    {
        var testCase = await PlannedAsync(Reads, status: TestCaseStatus.Planning);

        Assert.Equal(["Tests_Error_NotPlanned"], (await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, true))).Errors);
    }

    [Fact]
    public async Task Approve_AnotherOrganizationsTest_IsAsIfItDidNotExist()
    {
        var testCase = await PlannedAsync(Reads);

        Assert.Equal(["Tests_Error_NoSuchTest"], (await AsAsync(_contoso, Role.Owner, tests => tests.ApproveAsync(testCase, true))).Errors);
        Assert.Equal(["Tests_Error_NoSuchTest"], (await AsAsync(_contoso, Role.Owner, tests => tests.RunAsync(testCase))).Errors);
    }

    // --- Running ---

    [Fact]
    public async Task Run_AnApprovedPlan_QueuesARunOfTheTest_ThatTheTestLists()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));

        var run = await AsAsync(_northwind, Role.QaEngineer, tests => tests.RunAsync(testCase));

        Assert.True(run.Succeeded, string.Join(", ", run.Errors));
        var detail = await AsAsync(_northwind, Role.Viewer, tests => tests.GetAsync(testCase));
        var listed = Assert.Single(detail!.Runs);
        Assert.Equal((run.Value, RunStatus.Queued), (listed.RunId, listed.Status));
        Assert.NotNull(detail.Approval);
        Assert.False(detail.Approval.ToChangeData);
        Assert.Empty(detail.StepsThatMayChangeData);
    }

    [Fact]
    public async Task Run_APlanThatWasNeverApproved_IsRefused_AndQueuesNothing()
    {
        var testCase = await PlannedAsync(Reads);

        Assert.Equal(["Tests_Error_NotApproved"], (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Errors);

        await using var database = _database.NewContext();
        Assert.False(await database.TestRuns.IgnoreQueryFilters().AnyAsync(run => run.TestCaseId == testCase, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Run_BySomebodyWhoCannotRunTests_IsRefused()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));

        Assert.Equal(["Tests_Error_CannotRun"], (await AsAsync(_northwind, Role.Viewer, tests => tests.RunAsync(testCase))).Errors);
    }

    [Fact]
    public async Task Run_WhenThePlanIsNotTheOneThatWasApproved_IsRefused_AndTheDispatcherCarriesOutNothing()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));

        // The stored plan changes after approval, however that came about.
        await using (var database = _database.NewContext())
        {
            var stored = await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == testCase, TestContext.Current.CancellationToken);
            stored.Plan = JsonSerializer.Serialize(new { Actions = Changes, Coverage = PlanCoverage.Full }, Web);

            using (_database.WriteScope.AllowWritingTo(_northwind))
            {
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        Assert.Equal(["Tests_Error_NotApproved"], (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Errors);
        Assert.Null(await DispatchedAsync(testCase));
    }

    [Fact]
    public async Task Run_AfterTheApprovalIsWithdrawn_IsRefused_AndTheDispatcherCarriesOutNothing()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));

        Assert.True((await AsAsync(_northwind, Role.QaLead, tests => tests.WithdrawApprovalAsync(testCase))).Succeeded);

        Assert.Equal(["Tests_Error_NotApproved"], (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Errors);
        Assert.Null(await DispatchedAsync(testCase));
    }

    [Fact]
    public async Task Dispatch_OfAPlanThatMayChangeData_UnderAReadOnlyApproval_CarriesOutNothing()
    {
        // An approval stored without the acknowledgement for a plan that needs it, as an older rule might have left it.
        var testCase = await PlannedAsync(Changes);

        await using (var database = _database.NewContext())
        {
            var stored = await database.TestCases.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == testCase, TestContext.Current.CancellationToken);
            stored.ApprovedAt = DateTimeOffset.UtcNow;
            stored.ApprovedPlanHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored.Plan!)));

            using (_database.WriteScope.AllowWritingTo(_northwind))
            {
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        Assert.Null(await DispatchedAsync(testCase));
    }

    [Fact]
    public async Task Run_AgainstAProductionSiteNobodyAuthorized_IsRefused()
    {
        var testCase = await PlannedAsync(Reads, kind: EnvironmentKind.Production);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));

        Assert.Equal(["Tests_Error_ProductionNotAuthorized"], (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Errors);
    }

    /// <summary>Runs the dispatcher, with a worker that reports a pass, until the run has the status expected.</summary>
    private async Task<Domain.Execution.TestRun> DispatchedRunAsync(Guid runId, RunStatus expected)
    {
        await using var services = _database.NewServices();
        var options = new ChangeableOptions(new ExecutionOptions
        {
            WorkerPath = ExecutionSupport.ReportingWorker(RunStatus.Passed, bytesDownloaded: 10),
            MaxConcurrentRuns = 1,
        });
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);

        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await ExecutionSupport.StatusOnceItIsAsync(_database, runId, expected, TimeSpan.FromSeconds(30));
            return await ExecutionSupport.StoredAsync(_database, runId);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AnApprovedRun_CarriedOutByTheDispatcher_ReachesTheWorker_AndEndsWithItsVerdict()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));
        var runId = (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Value;

        var run = await DispatchedRunAsync(runId, RunStatus.Passed);

        Assert.Equal(RunStatus.Passed, run.Status);
    }

    [Fact]
    public async Task ARunWhoseApprovalWasWithdrawnAfterItWasQueued_IsRefusedByTheDispatcher_BeforeAnyWorkerStarts()
    {
        var testCase = await PlannedAsync(Reads);
        await AsAsync(_northwind, Role.Owner, tests => tests.ApproveAsync(testCase, false));
        var runId = (await AsAsync(_northwind, Role.Owner, tests => tests.RunAsync(testCase))).Value;
        await AsAsync(_northwind, Role.Owner, tests => tests.WithdrawApprovalAsync(testCase));

        var run = await DispatchedRunAsync(runId, RunStatus.Cancelled);

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Contains("not approved", run.FailureReason, StringComparison.Ordinal);
    }

    // --- Which steps may change data ---

    [Fact]
    public void PlanEffects_CountsButtonsKeysAndChoices_AndWhatExploringNeverSaw_ButNotLinksTypingOrChecks()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "1", Path: "/"),
            new(ActionKind.Click, "2", ActionTarget.ByTestId("basket-link")),
            new(ActionKind.Click, "3", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.Type, "4", ActionTarget.ByTestId("search"), Text: "pot"),
            new(ActionKind.Press, "5", ActionTarget.ByTestId("search"), Text: "Enter"),
            new(ActionKind.Select, "6", ActionTarget.ByTestId("sort"), Text: "Price"),
            new(ActionKind.Click, "7", ActionTarget.ByTestId("never-seen")),
            new(ActionKind.AssertText, "8", ActionTarget.ByRole("heading", "Catalogue"), Text: "Catalogue"),
        ];

        Assert.Equal([3, 5, 6, 7], PlanEffects.StepsThatMayChangeData(plan, Shop));

        // Without what exploring saw, no click can be shown to be a link.
        Assert.Equal([2, 3, 5, 6, 7], PlanEffects.StepsThatMayChangeData(plan, snapshot: null));
    }

    [Fact]
    public void PlanEffects_ATextThatIsBothALinkAndAButton_CountsAsChangingData()
    {
        var both = new SiteSnapshot(OurShop, [new PageSnapshot("/", "T", null, [new SnapshotElement("link", "Save"), new SnapshotElement("button", "Save")])], false);

        Assert.Equal([1], PlanEffects.StepsThatMayChangeData([new PlannedAction(ActionKind.Click, "Save", ActionTarget.ByText("Save"))], both));
    }
}
