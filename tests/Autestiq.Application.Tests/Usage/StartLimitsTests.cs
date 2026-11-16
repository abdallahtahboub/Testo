using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Tests.Support;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;

namespace Autestiq.Application.Tests.Usage;

/// <summary>Rate limits on starting runs and asking for proofs (Milestone 3.6, gap G14), counted in the database.</summary>
public sealed class StartLimitsTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _organization;
    private Guid _project;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_organization, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        _project = await ExecutionSupport.ProjectAsync(_database, _organization, "SHOP");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private ServiceProvider Services(int perOrganization, int perUser) =>
        _database.NewServices(collection => collection.Configure<UsageOptions>(options =>
        {
            options.StartsPerOrganizationPerMinute = perOrganization;
            options.StartsPerUserPerMinute = perUser;
        }));

    private async Task<OperationResult<Guid>> StartAsync(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IExecutionService>()
            .StartRunAsync(_project, Scenarios.DemoShopBasket, TestContext.Current.CancellationToken);
    }

    private async Task<int> QueuedAsync()
    {
        await using var database = _database.NewContext();
        return await database.TestRuns.IgnoreQueryFilters().CountAsync(run => run.OrganizationId == _organization, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartingPastTheOrganizationsLimit_IsRefused_AndQueuesNothing()
    {
        await using var services = Services(perOrganization: 3, perUser: 100);
        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);

        for (var start = 0; start < 3; start++)
        {
            Assert.True((await StartAsync(services)).Succeeded);
        }

        var refused = await StartAsync(services);

        Assert.Equal(["Execution_Error_TooManyStarts"], refused.Errors);
        Assert.Equal(3, await QueuedAsync());
    }

    [Fact]
    public async Task APersonPastTheirOwnLimit_IsRefused_WhileAColleagueCanStillStart()
    {
        await using var services = Services(perOrganization: 100, perUser: 2);

        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);
        Assert.True((await StartAsync(services)).Succeeded);
        Assert.True((await StartAsync(services)).Succeeded);
        Assert.Equal(["Execution_Error_TooManyStarts"], (await StartAsync(services)).Errors);

        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.QaEngineer);
        Assert.True((await StartAsync(services)).Succeeded);
    }

    [Fact]
    public async Task TenStartsAtOnce_AgainstALimitOfFive_QueueExactlyFive()
    {
        // The organization's row lock is what makes this exact: without it, several requests read the same count.
        await using var services = Services(perOrganization: 5, perUser: 100);
        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => StartAsync(services))));

        Assert.Equal(5, results.Count(result => result.Succeeded));
        Assert.Equal(5, await QueuedAsync());
    }

    [Fact]
    public async Task AskingForProofsPastTheLimit_IsRefused()
    {
        await using var services = Services(perOrganization: 2, perUser: 100);
        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);

        async Task<OperationResult<OriginProofRequest>> RequestAsync(string origin)
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOriginProofService>().RequestAsync(origin, TestContext.Current.CancellationToken);
        }

        Assert.True((await RequestAsync("https://one.example")).Succeeded);
        Assert.True((await RequestAsync("https://two.example")).Succeeded);
        Assert.Equal(["Execution_Error_TooManyStarts"], (await RequestAsync("https://three.example")).Errors);

        // Asking again for one already issued is not a new request, and is answered.
        Assert.True((await RequestAsync("https://one.example")).Succeeded);
    }
}
