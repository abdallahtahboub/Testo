using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Testing;

namespace Autestiq.Application.Tests.Projects;

/// <summary>
/// Exploration that may act on a customer's site (ADR-0032): only an Owner allows it, only with the acknowledgement,
/// never on production, audited; and the dispatcher asks the worker to act only while the permission stands.
/// </summary>
public sealed class ExplorationActingTests : IAsyncLifetime
{
    private const string Staging = "https://staging.shop.example";

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

    private async Task<T> AsAsync<T>(Guid organization, Role role, Func<IProjectService, Task<T>> act)
    {
        await using var services = _database.NewServices();
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);

        using var scope = services.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<IProjectService>());
    }

    private async Task<(Guid Project, Guid Environment)> EnvironmentAsync(EnvironmentKind kind = EnvironmentKind.Test, string origin = Staging)
    {
        return await AsAsync(_northwind, Role.Owner, async projects =>
        {
            if (await projects.FindAsync("SHOP") is not { } found)
            {
                await projects.CreateAsync(new NewProject("Shop", "SHOP", null));
                found = (await projects.FindAsync("SHOP"))!;
            }

            var environment = await projects.AddEnvironmentAsync(found.ProjectId, new NewEnvironment($"E{Random.Shared.Next(1000, 9999)}", origin, kind));
            return (found.ProjectId, environment.Value);
        });
    }

    private async Task<EnvironmentSummary> SummaryAsync(Guid project, Guid environment) =>
        (await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project))).Single(candidate => candidate.EnvironmentId == environment);

    [Fact]
    public async Task Allow_ByAnOwner_WithTheAcknowledgement_IsKept_Audited_AndCanBeWithdrawn()
    {
        var (project, environment) = await EnvironmentAsync();

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, acknowledged: true))).Succeeded);
        Assert.NotNull((await SummaryAsync(project, environment)).ExplorationMayActAt);

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.WithdrawExplorationActingAsync(environment))).Succeeded);
        Assert.Null((await SummaryAsync(project, environment)).ExplorationMayActAt);

        await using var database = _database.NewContext();
        var actions = await database.AuditEntries.IgnoreQueryFilters()
            .Where(entry => entry.SubjectId == environment)
            .Select(entry => entry.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("environment.exploration_acting_allowed", actions);
        Assert.Contains("environment.exploration_acting_withdrawn", actions);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.QaLead)]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Viewer)]
    public async Task Allow_BySomebodyWhoIsNotAnOwner_IsRefused(Role role)
    {
        var (project, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_OwnerOnly"], (await AsAsync(_northwind, role, projects => projects.AllowExplorationActingAsync(environment, true))).Errors);
        Assert.Null((await SummaryAsync(project, environment)).ExplorationMayActAt);
    }

    [Fact]
    public async Task Allow_WithoutTheAcknowledgement_IsRefused()
    {
        var (_, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_ActingAcknowledgementRequired"], (await AsAsync(_northwind, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, false))).Errors);
    }

    [Fact]
    public async Task Allow_OnAProductionSite_IsRefused_EvenOneAuthorizedForRuns()
    {
        var (_, environment) = await EnvironmentAsync(EnvironmentKind.Production, "https://shop.example");
        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.AuthorizeProductionAsync(environment, true))).Succeeded);

        Assert.Equal(["Environments_Error_ActingOnProduction"], (await AsAsync(_northwind, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, true))).Errors);
    }

    [Fact]
    public async Task Allow_OnAnotherOrganizationsEnvironment_IsAsIfItDidNotExist()
    {
        var (_, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_NoSuchEnvironment"], (await AsAsync(_contoso, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, true))).Errors);
    }

    // --- What the dispatcher asks of the worker ---

    /// <summary>An exploration run of a test against the environment, as describing a test would queue it.</summary>
    private async Task<TestRun> ExplorationAsync(Guid project, Guid environment, string origin = Staging)
    {
        await using var database = _database.NewContext();
        var testCase = new TestCase
        {
            OrganizationId = _northwind,
            ProjectId = project,
            EnvironmentId = environment,
            Number = Random.Shared.Next(1, 1_000_000),
            Description = "Check the basket",
            Status = TestCaseStatus.Exploring,
        };
        database.TestCases.Add(testCase);

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new TestRun { OrganizationId = _northwind, ProjectId = project, ScenarioId = Scenarios.Exploration, TargetOrigin = origin, TestCaseId = testCase.Id };
    }

    private async Task<bool> MayActAsync(TestRun run)
    {
        await using var database = _database.NewContext();
        return await RunDispatcher.ExplorationMayActAsync(database, run, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnExploration_AsksTheWorkerToAct_OnlyWhileTheOwnersPermissionStands()
    {
        var (project, environment) = await EnvironmentAsync();
        var run = await ExplorationAsync(project, environment);

        Assert.False(await MayActAsync(run));
        Assert.False(RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x").CustomerAllowsActing);

        await AsAsync(_northwind, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, true));
        Assert.True(await MayActAsync(run));

        var job = RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x", customerAllowsActing: true);
        Assert.Equal((WorkerTask.Explore, true, true, true), (job.Task, job.ExploreStates, job.SubmitForms, job.CustomerAllowsActing));

        await AsAsync(_northwind, Role.Owner, projects => projects.WithdrawExplorationActingAsync(environment));
        Assert.False(await MayActAsync(run));
    }

    [Fact]
    public void ARunThatIsNotAnExploration_NeverCarriesThePermission()
    {
        var run = new TestRun { OrganizationId = _northwind, ScenarioId = Scenarios.ApprovedPlan, TargetOrigin = Staging, TestCaseId = Guid.CreateVersion7() };

        Assert.False(RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x", customerAllowsActing: true).CustomerAllowsActing);
    }

    [Fact]
    public async Task AnExploration_AimedAtAnotherOrigin_ThanItsEnvironments_DoesNotAct()
    {
        var (project, environment) = await EnvironmentAsync();
        await AsAsync(_northwind, Role.Owner, projects => projects.AllowExplorationActingAsync(environment, true));

        var run = await ExplorationAsync(project, environment, origin: "https://elsewhere.example");

        Assert.False(await MayActAsync(run));
    }
}
