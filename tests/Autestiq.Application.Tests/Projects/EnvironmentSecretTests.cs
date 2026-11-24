using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Execution;
using Autestiq.Application.Planning;
using Autestiq.Application.Projects;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Testing;

namespace Autestiq.Application.Tests.Projects;

/// <summary>
/// A test account's sign-in values, per environment (ADR-0033): who may set them, that nothing reads a value back,
/// that a value reads only under its own environment's purpose, never on production; and that plans, approvals and runs
/// use them by name only, and only where they are kept.
/// </summary>
public sealed class EnvironmentSecretTests : IAsyncLifetime
{
    private const string Staging = "https://staging.shop.example";
    private const string Password = "Pa ss w0rd!";
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

    private ServiceProvider Services() => _database.NewServices(services => services.AddSingleton<ISecretProtector>(new PurposeBoundProtector()));

    private async Task<T> AsAsync<T>(Guid organization, Role role, Func<IServiceProvider, Task<T>> act)
    {
        await using var services = Services();
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);
        using var scope = services.CreateScope();
        return await act(scope.ServiceProvider);
    }

    private Task<Contracts.Common.OperationResult> SetAsync(Guid organization, Role role, Guid environment, string name = "password", string value = Password) =>
        AsAsync(organization, role, provider => provider.GetRequiredService<IProjectService>().SetSecretAsync(environment, name, value));

    private async Task<(Guid Project, Guid Environment)> EnvironmentAsync(EnvironmentKind kind = EnvironmentKind.Test, string origin = Staging) =>
        await AsAsync(_northwind, Role.Owner, async provider =>
        {
            var projects = provider.GetRequiredService<IProjectService>();

            if (await projects.FindAsync("SHOP") is not { } found)
            {
                await projects.CreateAsync(new NewProject("Shop", "SHOP", null));
                found = (await projects.FindAsync("SHOP"))!;
            }

            return (found.ProjectId, (await projects.AddEnvironmentAsync(found.ProjectId, new NewEnvironment($"E{Random.Shared.Next(1000, 9999)}", origin, kind))).Value);
        });

    // --- Setting and reading ---

    [Fact]
    public async Task Set_ByAQaLead_IsKeptEncrypted_ListedByNameOnly_AndAuditedWithoutTheValue()
    {
        var (project, environment) = await EnvironmentAsync();

        Assert.True((await SetAsync(_northwind, Role.QaLead, environment)).Succeeded);

        var listed = (await AsAsync(_northwind, Role.Viewer, provider => provider.GetRequiredService<IProjectService>().ListEnvironmentsAsync(project)))
            .Single(candidate => candidate.EnvironmentId == environment);
        Assert.Equal(["password"], listed.Secrets!.Select(secret => secret.Name));

        await using var database = _database.NewContext();
        var stored = await database.EnvironmentSecrets.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Password, stored.Ciphertext, StringComparison.Ordinal);

        var audit = await database.AuditEntries.IgnoreQueryFilters()
            .Where(entry => entry.Action == "environment.secret_set")
            .ToListAsync(TestContext.Current.CancellationToken);
        var entry = Assert.Single(audit);
        Assert.Contains("password", entry.SubjectName, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, (entry.SubjectName ?? "") + entry.Before + entry.After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Values_ReadOnlyUnderTheirOwnEnvironmentsPurpose()
    {
        var (_, environment) = await EnvironmentAsync();
        var (_, other) = await EnvironmentAsync(origin: "https://other.shop.example");
        await SetAsync(_northwind, Role.Owner, environment);

        await using var database = _database.NewContext();
        var protector = new PurposeBoundProtector();

        var mine = await EnvironmentSecrets.ValuesAsync(database, protector, _northwind, environment, TestContext.Current.CancellationToken);
        Assert.Equal(Password, mine!["password"]);

        // The same ciphertext, read as another environment's or another organization's, is nothing.
        var ciphertext = (await database.EnvironmentSecrets.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Ciphertext;
        Assert.Null(protector.Unprotect(EnvironmentSecrets.Purpose(_northwind, other), ciphertext));
        Assert.Null(protector.Unprotect(EnvironmentSecrets.Purpose(_contoso, environment), ciphertext));
    }

    [Theory]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task Set_BySomebodyWhoCannotManageProjects_IsRefused(Role role)
    {
        var (_, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_NotAllowed"], (await SetAsync(_northwind, role, environment)).Errors);
    }

    [Fact]
    public async Task Set_OnAProductionSite_IsRefused()
    {
        var (_, environment) = await EnvironmentAsync(EnvironmentKind.Production, "https://shop.example");

        Assert.Equal(["Environments_Error_SecretsOnProduction"], (await SetAsync(_northwind, Role.Owner, environment)).Errors);
    }

    [Theory]
    [InlineData("Password", Password)]
    [InlineData("pass word", Password)]
    [InlineData("1password", Password)]
    [InlineData("password", "")]
    public async Task Set_WithANameOrValueNotAllowed_IsRefused(string name, string value)
    {
        var (_, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_InvalidSecret"], (await SetAsync(_northwind, Role.Owner, environment, name, value)).Errors);
    }

    [Fact]
    public async Task Set_OnAnotherOrganizationsEnvironment_IsAsIfItDidNotExist()
    {
        var (_, environment) = await EnvironmentAsync();

        Assert.Equal(["Environments_Error_NoSuchEnvironment"], (await SetAsync(_contoso, Role.Owner, environment)).Errors);
    }

    [Fact]
    public async Task Delete_RemovesTheValue_AndIsAudited()
    {
        var (project, environment) = await EnvironmentAsync();
        await SetAsync(_northwind, Role.Owner, environment);

        Assert.True((await AsAsync(_northwind, Role.Owner, provider => provider.GetRequiredService<IProjectService>().DeleteSecretAsync(environment, "password"))).Succeeded);

        await using var database = _database.NewContext();
        Assert.False(await database.EnvironmentSecrets.IgnoreQueryFilters().AnyAsync(TestContext.Current.CancellationToken));
        Assert.True(await database.AuditEntries.IgnoreQueryFilters().AnyAsync(entry => entry.Action == "environment.secret_deleted", TestContext.Current.CancellationToken));
    }

    // --- Planning, approving, running ---

    private static readonly SiteSnapshot Shop = new(
        Staging,
        [new PageSnapshot("/login", "Sign in", "Sign in", [new SnapshotElement("textbox", "Password", "password"), new SnapshotElement("heading", "Sign in")])],
        Truncated: false);

    private static readonly PlannedAction[] SignsIn =
    [
        new(ActionKind.Goto, "Open the sign-in page", Path: "/login"),
        new(ActionKind.Type, "Type the password", ActionTarget.ByTestId("password"), SecretName: "password"),
        new(ActionKind.AssertVisible, "Still the sign-in heading", ActionTarget.ByRole("heading", "Sign in")),
    ];

    [Fact]
    public void Validator_RefusesAPlanThatTypesASecretItWasNotGiven()
    {
        var plan = new TestPlan("Sign in", SignsIn, []);

        Assert.Contains(PlanningErrors.UnknownSecret, PlanValidator.Review(plan, Shop, 20, secretNames: []).Errors);
        Assert.DoesNotContain(PlanningErrors.UnknownSecret, PlanValidator.Review(plan, Shop, 20, secretNames: ["password"]).Errors);
    }

    private async Task<Guid> PlannedAsync(Guid project, Guid environment)
    {
        await using var database = _database.NewContext();
        var testCase = new TestCase
        {
            OrganizationId = _northwind,
            ProjectId = project,
            EnvironmentId = environment,
            Number = Random.Shared.Next(1, 1_000_000),
            Description = "Sign in",
            Status = TestCaseStatus.Planned,
            Snapshot = JsonSerializer.Serialize(Shop, Web),
            Plan = JsonSerializer.Serialize(new { Actions = SignsIn, Coverage = PlanCoverage.Full }, Web),
            PlannedAt = DateTimeOffset.UtcNow,
        };
        database.TestCases.Add(testCase);

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return testCase.Id;
    }

    [Fact]
    public async Task APlanThatSignsIn_IsApprovableOnlyOnceItsSecretIsKept_AndItsRunCarriesTheValue()
    {
        var (project, environment) = await EnvironmentAsync();
        var testCase = await PlannedAsync(project, environment);

        Assert.Equal(["Tests_Error_NeedsSecret"],
            (await AsAsync(_northwind, Role.Owner, provider => provider.GetRequiredService<ITestCaseService>().ApproveAsync(testCase, true))).Errors);

        await SetAsync(_northwind, Role.Owner, environment);
        Assert.True((await AsAsync(_northwind, Role.Owner, provider => provider.GetRequiredService<ITestCaseService>().ApproveAsync(testCase, true))).Succeeded);

        await using var database = _database.NewContext();
        var run = new TestRun { OrganizationId = _northwind, ProjectId = project, ScenarioId = Scenarios.ApprovedPlan, TargetOrigin = Staging, TestCaseId = testCase };
        var approved = await RunDispatcher.ApprovedPlanAsync(database, run, TestContext.Current.CancellationToken);
        Assert.NotNull(approved);

        var values = await EnvironmentSecrets.ValuesAsync(database, new PurposeBoundProtector(), _northwind, approved.EnvironmentId, TestContext.Current.CancellationToken);
        var job = RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x", approved, secrets: values);
        Assert.Equal(Password, job.Secrets!["password"]);

        // Deleted after approval: the dispatcher carries out nothing.
        await AsAsync(_northwind, Role.Owner, provider => provider.GetRequiredService<IProjectService>().DeleteSecretAsync(environment, "password"));
        Assert.Null(await RunDispatcher.ApprovedPlanAsync(_database.NewContext(), run, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AScriptedRun_NeverCarriesSecrets_EvenIfGiven()
    {
        var run = new TestRun { OrganizationId = _northwind, ScenarioId = Scenarios.DemoShopBasket, TargetOrigin = Staging };

        Assert.Null(RunDispatcher.JobFor(run, new ExecutionOptions(), "/tmp/x", secrets: new Dictionary<string, string> { ["password"] = Password }).Secrets);
    }
}
