using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Plans;
using Autestiq.Domain.Projects;

namespace Autestiq.Application.Tests.Identity;

/// <summary>
/// Reading the organization in context (E1.4, ADR-0043): its name and its plan version for owners and admins, its own
/// projects and members counted and no one else's, and nothing at all for anyone else.
/// </summary>
public sealed class OrganizationServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private Guid _northwind;
    private Guid _contoso;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _services = _database.NewServices();
        (_contoso, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Contoso");
        await Seed.MemberAsync(_services, _contoso, "eve@contoso.example", Role.QaEngineer);
        await AddProjectAsync(_contoso, "CON", ProjectStatus.Active);
        await AddProjectAsync(_contoso, "OLD", ProjectStatus.Active);

        (_northwind, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");
        await Seed.MemberAsync(_services, _northwind, "sven@northwind.example", Role.Admin);
        await AddProjectAsync(_northwind, "SHOP", ProjectStatus.Active);
        await AddProjectAsync(_northwind, "GONE", ProjectStatus.Archived);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task GetCurrent_AsTheOwner_IsThisOrganization_WithItsPlanVersion()
    {
        var profile = await ReadAsync();

        Assert.NotNull(profile);
        Assert.Equal("Northwind", profile.Name);
        Assert.False(string.IsNullOrWhiteSpace(profile.Slug));

        await using var database = _database.NewContext();
        var version = database.PlanVersions.Single(candidate => candidate.Id == PlanCatalog.StartingVersion);
        var plan = database.Plans.Single(candidate => candidate.Id == version.PlanId);
        Assert.Equal(plan.Name, profile.PlanName);
        Assert.Equal(version.Number, profile.PlanVersion);
        Assert.Equal(version.MonthlyBrowserMinutes, profile.MonthlyBrowserMinutes);
        Assert.Equal(version.ConcurrentRuns, profile.ConcurrentRuns);
        Assert.Equal(version.MaxProjects, profile.MaxProjects);
    }

    [Fact]
    public async Task GetCurrent_CountsOnlyThisOrganizations_ActiveProjectsAndMembers()
    {
        var profile = await ReadAsync();

        Assert.NotNull(profile);
        Assert.Equal(1, profile.ActiveProjects);
        Assert.Equal(2, profile.ActiveMembers);
    }

    [Fact]
    public async Task GetCurrent_AsAnAdmin_IsAllowed()
    {
        var sven = await FindUserAsync("sven@northwind.example");
        _database.Principal.Become(_northwind, sven, Role.Admin, "sven@northwind.example");

        Assert.NotNull(await ReadAsync());
    }

    [Theory]
    [InlineData(Role.QaLead)]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task GetCurrent_WithoutThePermission_IsNothing(Role role)
    {
        var email = $"{role}@northwind.example".ToLowerInvariant();
        var person = await Seed.MemberAsync(_services, _northwind, email, role);
        _database.Principal.Become(_northwind, person, role, email);

        Assert.Null(await ReadAsync());
    }

    private async Task<OrganizationProfile?> ReadAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationService>().GetCurrentAsync(Cancellation);
    }

    private async Task<Guid> FindUserAsync(string email)
    {
        await using var database = _database.NewContext();
        return database.Users.Single(user => user.Email == email).Id;
    }

    private async Task AddProjectAsync(Guid organization, string key, ProjectStatus status)
    {
        await using var database = _database.NewContext();
        database.Projects.Add(new Project { OrganizationId = organization, Name = key, KeyPrefix = key, Status = status });

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(Cancellation);
        }
    }
}
