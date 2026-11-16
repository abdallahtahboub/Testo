using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// The kill switch (Milestone 3.5, gap G4): for every organization or for some, no run starts, none is claimed,
/// and a running one is stopped within seconds.
/// </summary>
public sealed class KillSwitchTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _first;
    private Guid _second;
    private Guid _firstProject;
    private Guid _secondProject;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_first, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        (_second, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Contoso");
        _firstProject = await ExecutionSupport.ProjectAsync(_database, _first, "SHOP");
        _secondProject = await ExecutionSupport.ProjectAsync(_database, _second, "OTHER");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task StartRun_WhileEveryOrganizationIsPaused_IsRefused()
    {
        var result = await StartAsync(_first, _firstProject, options => options.Paused = true);

        Assert.False(result.Succeeded);
        Assert.Equal(["Execution_Error_Paused"], result.Errors);
    }

    [Fact]
    public async Task StartRun_WhileOnlyAnotherOrganizationIsPaused_GoesAhead()
    {
        var paused = await StartAsync(_first, _firstProject, options => options.PausedOrganizations.Add(_first));
        var other = await StartAsync(_second, _secondProject, options => options.PausedOrganizations.Add(_first));

        Assert.Equal(["Execution_Error_Paused"], paused.Errors);
        Assert.True(other.Succeeded);
    }

    [Fact]
    public async Task Claimable_LeavesAPausedOrganizationsRunsQueued()
    {
        var firstRun = await ExecutionSupport.RunAsync(_database, _first, _firstProject);
        var secondRun = await ExecutionSupport.RunAsync(_database, _second, _secondProject);

        await using var database = _database.NewContext();
        var options = new ExecutionOptions { PausedOrganizations = [_first] };

        var claimable = await RunDispatcher.Claimable(database.TestRuns.IgnoreQueryFilters(), options)
            .Select(run => run.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(firstRun, claimable);
        Assert.Contains(secondRun, claimable);

        var everything = await RunDispatcher.Claimable(database.TestRuns.IgnoreQueryFilters(), new ExecutionOptions { Paused = true })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(everything);
    }

    [Fact]
    public async Task ARunningRun_IsStoppedWithinSeconds_WhenItsOrganizationIsPaused()
    {
        // A stand-in for the worker that only waits, so the run stays running until something stops it.
        var monitor = new ChangeableOptions(new ExecutionOptions { WorkerPath = ExecutionSupport.WaitingWorker(), MaxConcurrentRuns = 1 });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(), monitor, ExecutionSupport.Usage(), TimeProvider.System, NullLogger<RunDispatcher>.Instance);

        var runId = await ExecutionSupport.RunAsync(_database, _first, _firstProject);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Running, TimeSpan.FromSeconds(20)));

            var clock = Stopwatch.StartNew();
            monitor.Change(options => options.PausedOrganizations.Add(_first));

            Assert.Equal(RunStatus.Cancelled, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Cancelled, TimeSpan.FromSeconds(20)));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Stopping took {clock.Elapsed}.");

            var run = await ExecutionSupport.StoredAsync(_database, runId);
            Assert.Contains("paused", run.FailureReason, StringComparison.Ordinal);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    private async Task<Contracts.Common.OperationResult<Guid>> StartAsync(Guid organization, Guid project, Action<ExecutionOptions> configure)
    {
        await using var services = _database.NewServices(collection => collection.Configure(configure));
        _database.Principal.Become(organization, Guid.CreateVersion7(), Role.Owner);

        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IExecutionService>()
            .StartRunAsync(project, Scenarios.DemoShopBasket, TestContext.Current.CancellationToken);
    }
}
