using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Tests.Support;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;

namespace Autestiq.Application.Tests.Projects;

/// <summary>Projects and their environments (Milestone 5, part 1).</summary>
public sealed class ProjectServiceTests : IAsyncLifetime
{
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

    private Task<Contracts.Common.OperationResult<string>> CreateAsync(Guid organization, string key, Role role = Role.Owner) =>
        AsAsync(organization, role, projects => projects.CreateAsync(new NewProject($"Project {key}", key, null)));

    private async Task MoveToAsync(string plan)
    {
        await using var database = _database.NewContext();
        Assert.Equal(PlanChange.Moved, await PlanAssignment.MoveAsync(
            database, _database.WriteScope, TimeProvider.System, "northwind", plan, null, "ops@autestiq.example", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_AsOwner_StoresTheProject_WithItsKeyInCapitals_AndAudits()
    {
        var created = await AsAsync(_northwind, Role.Owner, projects => projects.CreateAsync(new NewProject("  Web shop ", "shop", " The shop. ")));
        var listed = await AsAsync(_northwind, Role.Viewer, projects => projects.ListAsync());

        Assert.Equal("SHOP", created.Value);
        var project = Assert.Single(listed);
        Assert.Equal(("Web shop", "SHOP", "The shop.", 0), (project.Name, project.Key, project.Description, project.Environments));

        await using var database = _database.NewContext();
        Assert.True(await database.AuditEntries.IgnoreQueryFilters().AnyAsync(entry => entry.Action == "project.created" && entry.OrganizationId == _northwind, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_BeyondWhatThePlanAllows_IsRefused()
    {
        // A new organization is on Developer, which allows one project.
        Assert.True((await CreateAsync(_northwind, "SHOP")).Succeeded);

        Assert.Equal(["Projects_Error_PlanLimit"], (await CreateAsync(_northwind, "CLINIC")).Errors);
    }

    [Fact]
    public async Task Create_AKeyTheOrganizationAlreadyUses_IsRefused_ButAnotherOrganizationMayUseIt()
    {
        await MoveToAsync("starter");
        await CreateAsync(_northwind, "SHOP");

        Assert.Equal(["Projects_Error_KeyTaken"], (await CreateAsync(_northwind, "shop")).Errors);
        Assert.True((await CreateAsync(_contoso, "SHOP")).Succeeded);
    }

    [Theory]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task Create_BySomebodyWhoCannotManageProjects_IsRefused(Role role)
    {
        Assert.Equal(["Projects_Error_NotAllowed"], (await CreateAsync(_northwind, "SHOP", role)).Errors);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("1SHOP")]
    [InlineData("SHOP-1")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("")]
    public async Task Create_AKeyThatIsNotOne_IsRefused(string key)
    {
        Assert.Equal(["Projects_Error_InvalidKey"], (await CreateAsync(_northwind, key)).Errors);
    }

    [Fact]
    public async Task Find_ByKeyInAnyCase_FindsTheOrganizationsOwnProjectOnly()
    {
        await CreateAsync(_northwind, "SHOP");

        Assert.NotNull(await AsAsync(_northwind, Role.Viewer, projects => projects.FindAsync("shop")));
        Assert.Null(await AsAsync(_contoso, Role.Owner, projects => projects.FindAsync("SHOP")));
    }

    [Fact]
    public async Task AddEnvironment_StoresTheNormalizedOrigin_AndListsItWithItsProof()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP"));

        var added = await AsAsync(_northwind, Role.Admin, projects => projects.AddEnvironmentAsync(project!.ProjectId, new NewEnvironment("Staging", "HTTPS://Staging.Shop.Example/")));
        var before = await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project!.ProjectId));

        await AsAsync(_northwind, Role.Owner, async projects =>
        {
            await using var services = _database.NewServices();
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOriginProofService>().RequestAsync("https://staging.shop.example");
        });

        var after = await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project!.ProjectId));

        Assert.True(added.Succeeded);
        var environment = Assert.Single(before);
        Assert.Equal(("Staging", "https://staging.shop.example:443", (OriginProofState?)null), (environment.Name, environment.Origin, environment.Proof));
        Assert.Equal(OriginProofStatus.Issued, Assert.Single(after).Proof?.Status);
        Assert.Equal(1, (await AsAsync(_northwind, Role.Viewer, projects => projects.FindAsync("SHOP")))!.Environments);
    }

    [Fact]
    public async Task AddEnvironment_TheSameSiteOrNameTwice_OrNotAnOrigin_IsRefused()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "https://staging.shop.example")));

        Assert.Equal(["Environments_Error_OriginTaken"], (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Again", "https://staging.shop.example:443/")))).Errors);
        Assert.Equal(["Environments_Error_NameTaken"], (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("staging", "https://other.shop.example")))).Errors);
        Assert.Equal(["Environments_Error_InvalidOrigin"], (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Admin", "https://shop.example/admin")))).Errors);
        Assert.Equal(["Environments_Error_NameRequired"], (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment(" ", "https://x.example")))).Errors);
    }

    [Fact]
    public async Task AddEnvironment_ToAnotherOrganizationsProject_FindsNoSuchProject()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        var result = await AsAsync(_contoso, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Mine", "https://shop.example")));

        Assert.Equal(["Environments_Error_NoSuchProject"], result.Errors);
        Assert.Empty(await AsAsync(_contoso, Role.Owner, projects => projects.ListEnvironmentsAsync(project)));
    }

    [Fact]
    public async Task AddEnvironment_BySomebodyWhoCannotManageProjects_IsRefused()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        var result = await AsAsync(_northwind, Role.QaEngineer, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "https://shop.example")));

        Assert.Equal(["Environments_Error_NotAllowed"], result.Errors);
    }

    private async Task<(Guid Project, Guid Environment)> ProductionAsync()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;
        var environment = (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Live", "https://shop.example", EnvironmentKind.Production)))).Value;
        return (project, environment);
    }

    [Fact]
    public async Task AddEnvironment_WithoutSayingWhichKind_IsATestSite()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "https://staging.shop.example")));

        var environment = Assert.Single(await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project)));
        Assert.Equal(EnvironmentKind.Test, environment.Kind);
        Assert.True(environment.KindAllowsRuns);
    }

    [Fact]
    public async Task AProductionEnvironment_IsNotAuthorizedWhenAdded()
    {
        var (project, _) = await ProductionAsync();

        var environment = Assert.Single(await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project)));
        Assert.Equal(EnvironmentKind.Production, environment.Kind);
        Assert.Null(environment.ProductionAuthorizedAt);
        Assert.False(environment.KindAllowsRuns);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.QaLead)]
    [InlineData(Role.QaEngineer)]
    public async Task AuthorizeProduction_BySomebodyWhoIsNotAnOwner_IsRefused(Role role)
    {
        var (_, environment) = await ProductionAsync();

        Assert.Equal(["Environments_Error_OwnerOnly"], (await AsAsync(_northwind, role, projects => projects.AuthorizeProductionAsync(environment, acknowledged: true))).Errors);
    }

    [Fact]
    public async Task AuthorizeProduction_WithoutTheAcknowledgement_IsRefused()
    {
        var (_, environment) = await ProductionAsync();

        Assert.Equal(["Environments_Error_AcknowledgementRequired"], (await AsAsync(_northwind, Role.Owner, projects => projects.AuthorizeProductionAsync(environment, acknowledged: false))).Errors);
    }

    [Fact]
    public async Task AuthorizeProduction_ByAnOwner_IsRecordedAndAudited_AndCanBeWithdrawn()
    {
        var (project, environment) = await ProductionAsync();

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.AuthorizeProductionAsync(environment, acknowledged: true))).Succeeded);
        Assert.True(Assert.Single(await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project))).KindAllowsRuns);

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.WithdrawProductionAsync(environment))).Succeeded);
        Assert.False(Assert.Single(await AsAsync(_northwind, Role.Viewer, projects => projects.ListEnvironmentsAsync(project))).KindAllowsRuns);

        await using var database = _database.NewContext();
        var actions = await database.AuditEntries.IgnoreQueryFilters()
            .Where(entry => entry.OrganizationId == _northwind && entry.Action.StartsWith("environment.production"))
            .Select(entry => entry.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["environment.production_authorized", "environment.production_withdrawn"], actions.Order().ToList());
    }

    [Fact]
    public async Task AuthorizeProduction_OfATestSite_IsRefused()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;
        var environment = (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "https://staging.shop.example")))).Value;

        Assert.Equal(["Environments_Error_NotProduction"], (await AsAsync(_northwind, Role.Owner, projects => projects.AuthorizeProductionAsync(environment, acknowledged: true))).Errors);
    }

    [Fact]
    public async Task AiProcessing_IsOffForANewProject()
    {
        await CreateAsync(_northwind, "SHOP");

        Assert.Null((await AsAsync(_northwind, Role.Viewer, projects => projects.FindAsync("SHOP")))!.AiProcessingAllowedAt);
    }

    [Fact]
    public async Task AllowAiProcessing_NeedsTheAcknowledgement_AndSomebodyWhoManagesProjects_AndIsAuditedBothWays()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        Assert.Equal(["Projects_Error_AiAcknowledgementRequired"], (await AsAsync(_northwind, Role.Owner, projects => projects.AllowAiProcessingAsync(project, acknowledged: false))).Errors);
        Assert.Equal(["Projects_Error_NotAllowed"], (await AsAsync(_northwind, Role.QaEngineer, projects => projects.AllowAiProcessingAsync(project, acknowledged: true))).Errors);

        Assert.True((await AsAsync(_northwind, Role.Admin, projects => projects.AllowAiProcessingAsync(project, acknowledged: true))).Succeeded);
        Assert.NotNull((await AsAsync(_northwind, Role.Viewer, projects => projects.FindAsync("SHOP")))!.AiProcessingAllowedAt);

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.WithdrawAiProcessingAsync(project))).Succeeded);
        Assert.Null((await AsAsync(_northwind, Role.Viewer, projects => projects.FindAsync("SHOP")))!.AiProcessingAllowedAt);

        await using var database = _database.NewContext();
        Assert.Equal(2, await database.AuditEntries.IgnoreQueryFilters().CountAsync(entry => entry.OrganizationId == _northwind && entry.Action.StartsWith("project.ai_processing"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllowAiProcessing_ForAnotherOrganizationsProject_FindsNoSuchProject()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;

        Assert.Equal(["Projects_Error_NoSuchProject"], (await AsAsync(_contoso, Role.Owner, projects => projects.AllowAiProcessingAsync(project, acknowledged: true))).Errors);
    }

    [Fact]
    public async Task RemoveEnvironment_RemovesIt_ButNotTheOrganizationsProofOfItsSite()
    {
        await CreateAsync(_northwind, "SHOP");
        var project = (await AsAsync(_northwind, Role.Owner, projects => projects.FindAsync("SHOP")))!.ProjectId;
        var environment = (await AsAsync(_northwind, Role.Owner, projects => projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "https://shop.example")))).Value;

        await AsAsync(_northwind, Role.Owner, async _ =>
        {
            await using var services = _database.NewServices();
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOriginProofService>().RequestAsync("https://shop.example");
        });

        Assert.True((await AsAsync(_northwind, Role.Owner, projects => projects.RemoveEnvironmentAsync(environment))).Succeeded);
        Assert.Empty(await AsAsync(_northwind, Role.Owner, projects => projects.ListEnvironmentsAsync(project)));

        await using var database = _database.NewContext();
        Assert.True(await database.OriginProofs.IgnoreQueryFilters().AnyAsync(proof => proof.OrganizationId == _northwind, TestContext.Current.CancellationToken));
    }
}
