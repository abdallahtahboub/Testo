using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Usage;

namespace Autestiq.Application.Tests.Usage;

/// <summary>
/// Concurrency and quota, checked when a run is claimed (Milestone 3.6, gaps G7 and G8, R-12): per organization, in
/// the database, under a lock, so they hold across dispatchers.
/// </summary>
public sealed class ClaimLimitsTests : IAsyncLifetime
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

    private static RunDispatcher Dispatcher(ServiceProvider services, string? worker = null) => new(
        services.GetRequiredService<IServiceScopeFactory>(),
        new ChangeableOptions(new ExecutionOptions { WorkerPath = worker ?? ExecutionSupport.WaitingWorker(), MaxConcurrentRuns = 10 }),
        ExecutionSupport.Usage(),
        TimeProvider.System,
        NullLogger<RunDispatcher>.Instance);

    [Fact]
    public async Task AnOrganizationAtItsLimit_WaitsInTheQueue_WhileAnotherOrganizationsRunStarts()
    {
        // Both are on the Developer plan: one run at a time each.
        var firstA = await ExecutionSupport.RunAsync(_database, _first, _firstProject);
        var firstB = await ExecutionSupport.RunAsync(_database, _first, _firstProject);
        var second = await ExecutionSupport.RunAsync(_database, _second, _secondProject);

        await using var services = _database.NewServices();
        var dispatcher = Dispatcher(services);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, firstA, RunStatus.Running, TimeSpan.FromSeconds(20)));
            Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, second, RunStatus.Running, TimeSpan.FromSeconds(20)));

            // Long enough for several claims to have passed it by.
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(RunStatus.Queued, (await ExecutionSupport.StoredAsync(_database, firstB)).Status);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TwoDispatchersRacing_NeverRunMoreThanTheOrganizationsLimit()
    {
        // R-12: the old limit was counted inside each process, so two processes allowed twice as many.
        var runs = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            runs.Add(await ExecutionSupport.RunAsync(_database, _first, _firstProject));
        }

        await using var services = _database.NewServices();
        var one = Dispatcher(services);
        var two = Dispatcher(services);
        await Task.WhenAll(one.StartAsync(TestContext.Current.CancellationToken), two.StartAsync(TestContext.Current.CancellationToken));

        try
        {
            var most = 0;

            for (var look = 0; look < 30; look++)
            {
                await using var database = _database.NewContext();
                var running = await database.TestRuns.IgnoreQueryFilters()
                    .CountAsync(run => runs.Contains(run.Id) && run.Status == RunStatus.Running, TestContext.Current.CancellationToken);

                most = Math.Max(most, running);
                await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
            }

            Assert.Equal(1, most);
        }
        finally
        {
            await Task.WhenAll(one.StopAsync(CancellationToken.None), two.StopAsync(CancellationToken.None));
        }
    }

    [Fact]
    public async Task ARun_ReservesNoMoreThanWhatIsLeftOfTheMonth()
    {
        await UsedAsync(_first, seconds: (100 * 60) - 30);
        var runId = await ExecutionSupport.RunAsync(_database, _first, _firstProject);

        await using var services = _database.NewServices();
        var dispatcher = Dispatcher(services);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Running, TimeSpan.FromSeconds(20)));
            Assert.Equal(30, (await ExecutionSupport.StoredAsync(_database, runId)).ReservedSeconds);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WithTheMonthUsedUp_AQueuedRunIsRefused_AndANewOneCannotStart()
    {
        await UsedAsync(_first, seconds: 100 * 60);
        var queued = await ExecutionSupport.RunAsync(_database, _first, _firstProject);

        await using var services = _database.NewServices();
        var dispatcher = Dispatcher(services);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Cancelled, await ExecutionSupport.StatusOnceItIsAsync(_database, queued, RunStatus.Cancelled, TimeSpan.FromSeconds(20)));
            Assert.Contains("browser minutes", (await ExecutionSupport.StoredAsync(_database, queued)).FailureReason, StringComparison.Ordinal);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }

        _database.Principal.Become(_first, Guid.CreateVersion7(), Role.Owner);
        using var scope = services.CreateScope();
        var started = await scope.ServiceProvider.GetRequiredService<IExecutionService>()
            .StartRunAsync(_firstProject, Scenarios.DemoShopBasket, TestContext.Current.CancellationToken);

        Assert.Equal(["Execution_Error_MinutesUsedUp"], started.Errors);
    }

    [Fact]
    public async Task ParallelRuns_CannotOverspend_TheSecondWaitsWhileTheFirstHoldsWhatIsLeft()
    {
        // Two at a time allowed, 90 seconds left: the first holds all 90, so the second has nothing to start with.
        await PlanAsync(_first, "starter");
        await UsedAsync(_first, seconds: (500 * 60) - 90);
        var first = await ExecutionSupport.RunAsync(_database, _first, _firstProject);
        var second = await ExecutionSupport.RunAsync(_database, _first, _firstProject);

        await using var services = _database.NewServices();
        var dispatcher = Dispatcher(services);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, first, RunStatus.Running, TimeSpan.FromSeconds(20)));
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal(90, (await ExecutionSupport.StoredAsync(_database, first)).ReservedSeconds);
            Assert.Equal(RunStatus.Queued, (await ExecutionSupport.StoredAsync(_database, second)).Status);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AFinishedRun_GivesItsReservationBack()
    {
        var runId = await ExecutionSupport.RunAsync(_database, _first, _firstProject);

        await using var services = _database.NewServices();
        var dispatcher = Dispatcher(services, ExecutionSupport.ReportingWorker(RunStatus.Passed, bytesDownloaded: 500, seconds: 5));
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Passed, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Passed, TimeSpan.FromSeconds(20)));
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }

        Assert.Equal(0, (await ExecutionSupport.StoredAsync(_database, runId)).ReservedSeconds);

        await using var database = _database.NewContext();
        var room = await RunDispatcher.RoomForAsync(database, _first, new UsageOptions(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal((100 * 60) - 5, room.SecondsLeft);
        Assert.Equal(1, room.ConcurrentRunsLeft);
    }

    [Fact]
    public async Task ARunThatWasLost_HoldsNothing()
    {
        // What the reaper leaves: an infrastructure error that still carries its reservation. Only running runs count.
        await ExecutionSupport.RunAsync(_database, _first, _firstProject, run =>
        {
            run.Status = RunStatus.InfrastructureError;
            run.ReservedSeconds = 120;
        });

        await using var database = _database.NewContext();
        var room = await RunDispatcher.RoomForAsync(database, _first, new UsageOptions(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(100 * 60, room.SecondsLeft);
        Assert.Equal(1, room.ConcurrentRunsLeft);
    }

    private async Task UsedAsync(Guid organization, long seconds)
    {
        await using var database = _database.NewContext();

        database.UsageEvents.Add(new UsageEvent
        {
            OrganizationId = organization,
            Category = UsageCategory.BrowserSeconds,
            Quantity = seconds,
            Billable = true,
            Period = UsageMeter.PeriodOf(DateTimeOffset.UtcNow),
            OccurredAt = DateTimeOffset.UtcNow,
            IdempotencyKey = $"earlier-{Guid.CreateVersion7():N}",
            Source = "test",
        });

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task PlanAsync(Guid organization, string plan)
    {
        await using var database = _database.NewContext();
        var stored = await database.Organizations.SingleAsync(candidate => candidate.Id == organization, TestContext.Current.CancellationToken);
        stored.Plan = plan;

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
