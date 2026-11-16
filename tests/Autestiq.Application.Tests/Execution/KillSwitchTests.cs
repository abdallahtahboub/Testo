using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Projects;

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
        _firstProject = await ProjectAsync(_first, "SHOP");
        _secondProject = await ProjectAsync(_second, "OTHER");
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
        var firstRun = await QueuedRunAsync(_first, _firstProject);
        var secondRun = await QueuedRunAsync(_second, _secondProject);

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
        var monitor = new ChangeableOptions(new ExecutionOptions { WorkerPath = WaitingWorker(), MaxConcurrentRuns = 1 });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(), monitor, TimeProvider.System, NullLogger<RunDispatcher>.Instance);

        var runId = await QueuedRunAsync(_first, _firstProject);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Running, await StatusOnceItIsAsync(runId, RunStatus.Running, TimeSpan.FromSeconds(20)));

            var clock = Stopwatch.StartNew();
            monitor.Change(options => options.PausedOrganizations.Add(_first));

            Assert.Equal(RunStatus.Cancelled, await StatusOnceItIsAsync(runId, RunStatus.Cancelled, TimeSpan.FromSeconds(20)));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Stopping took {clock.Elapsed}.");

            await using var database = _database.NewContext();
            var run = await database.TestRuns.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == runId, TestContext.Current.CancellationToken);
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

    /// <summary>Polls the run's stored status until it is the one expected or the time is up.</summary>
    private async Task<RunStatus> StatusOnceItIsAsync(Guid runId, RunStatus expected, TimeSpan within)
    {
        var deadline = Stopwatch.StartNew();
        var status = RunStatus.Queued;

        while (deadline.Elapsed < within)
        {
            await using var database = _database.NewContext();
            status = await database.TestRuns.IgnoreQueryFilters()
                .Where(run => run.Id == runId)
                .Select(run => run.Status)
                .SingleAsync(TestContext.Current.CancellationToken);

            if (status == expected)
            {
                return status;
            }

            // Real time: a real process is being waited on.
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        return status;
    }

    /// <summary>An executable that waits a minute and says nothing.</summary>
    private static string WaitingWorker()
    {
        var directory = Directory.CreateTempSubdirectory("autestiq-waiting-worker-").FullName;

        if (OperatingSystem.IsWindows())
        {
            var batch = Path.Combine(directory, "worker.cmd");
            File.WriteAllText(batch, "@ping -n 60 127.0.0.1 > nul\r\n");
            return batch;
        }

        var script = Path.Combine(directory, "worker.sh");
        File.WriteAllText(script, "#!/bin/sh\nsleep 60\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    private async Task<Guid> ProjectAsync(Guid organizationId, string prefix)
    {
        await using var database = _database.NewContext();

        var project = new Project { OrganizationId = organizationId, Name = prefix, KeyPrefix = prefix };
        database.Projects.Add(project);

        using (_database.WriteScope.AllowWritingTo(organizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return project.Id;
    }

    private async Task<Guid> QueuedRunAsync(Guid organizationId, Guid projectId)
    {
        await using var database = _database.NewContext();

        var run = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = Random.Shared.Next(1, 100_000),
            ScenarioId = Scenarios.DemoShopBasket,
            TargetOrigin = "http://127.0.0.1:5090",
            QueuedAt = DateTimeOffset.UtcNow,
        };

        database.TestRuns.Add(run);

        using (_database.WriteScope.AllowWritingTo(organizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return run.Id;
    }

    /// <summary>Options a test can change while the dispatcher reads them, as a configuration reload would.</summary>
    private sealed class ChangeableOptions(ExecutionOptions initial) : IOptionsMonitor<ExecutionOptions>
    {
        public ExecutionOptions CurrentValue { get; private set; } = initial;

        public ExecutionOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<ExecutionOptions, string?> listener) => null;

        public void Change(Action<ExecutionOptions> change) => change(CurrentValue);
    }
}
