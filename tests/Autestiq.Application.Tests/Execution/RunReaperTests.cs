using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// Runs whose dispatcher stopped answering (Milestone 3.5, gap G5) are ended as infrastructure errors, not left
/// running for ever, and a live dispatcher's runs are not.
/// </summary>
public sealed class RunReaperTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

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

    private async Task<int> ReapAsync()
    {
        await using var context = _database.NewContext();
        return await RunDispatcher.ReapAbandonedAsync(context, _database.WriteScope, new SetClock(Now), Lease, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reap_ARunningRunWhoseDispatcherWentQuiet_EndsAsAnInfrastructureError()
    {
        var quiet = await ExecutionSupport.RunAsync(_database, _organization, _project, run =>
        {
            run.Status = RunStatus.Running;
            run.StartedAt = Now - TimeSpan.FromMinutes(10);
            run.HeartbeatAt = Now - TimeSpan.FromMinutes(5);
        });

        Assert.Equal(1, await ReapAsync());

        var run = await ExecutionSupport.StoredAsync(_database, quiet);
        Assert.Equal(RunStatus.InfrastructureError, run.Status);
        Assert.StartsWith("Abandoned", run.FailureReason, StringComparison.Ordinal);
        Assert.Equal(Now, run.FinishedAt);
    }

    [Fact]
    public async Task Reap_ARunningRunWithARecentHeartbeat_IsLeftRunning()
    {
        var alive = await ExecutionSupport.RunAsync(_database, _organization, _project, run =>
        {
            run.Status = RunStatus.Running;
            run.StartedAt = Now - TimeSpan.FromMinutes(10);
            run.HeartbeatAt = Now - TimeSpan.FromSeconds(30);
        });

        Assert.Equal(0, await ReapAsync());
        Assert.Equal(RunStatus.Running, (await ExecutionSupport.StoredAsync(_database, alive)).Status);
    }

    [Fact]
    public async Task Reap_QueuedAndFinishedRuns_AreNotItsBusiness()
    {
        var queued = await ExecutionSupport.RunAsync(_database, _organization, _project, run => run.QueuedAt = Now - TimeSpan.FromHours(3));
        var passed = await ExecutionSupport.RunAsync(_database, _organization, _project, run =>
        {
            run.Status = RunStatus.Passed;
            run.HeartbeatAt = Now - TimeSpan.FromHours(3);
        });

        Assert.Equal(0, await ReapAsync());
        Assert.Equal(RunStatus.Queued, (await ExecutionSupport.StoredAsync(_database, queued)).Status);
        Assert.Equal(RunStatus.Passed, (await ExecutionSupport.StoredAsync(_database, passed)).Status);
    }

    [Fact]
    public async Task ARunLongerThanSeveralHeartbeats_KeepsTheResultItsWorkerGave()
    {
        // The heartbeats change the run's row while the worker runs. Writing the result back over the copy loaded
        // before the worker started was refused as a stale write, and the run ended as an infrastructure error.
        var options = new ChangeableOptions(new ExecutionOptions
        {
            WorkerPath = PassingWorkerAfter(seconds: 3),
            MaxConcurrentRuns = 1,
            HeartbeatInterval = TimeSpan.FromMilliseconds(300),
        });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);
        var runId = await ExecutionSupport.RunAsync(_database, _organization, _project);

        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Passed, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Passed, TimeSpan.FromSeconds(30)));
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>A worker that reads its job, waits, and reports that the run passed.</summary>
    private static string PassingWorkerAfter(int seconds)
    {
        var directory = Directory.CreateTempSubdirectory("autestiq-passing-worker-").FullName;
        var script = Path.Combine(directory, "worker.sh");

        File.WriteAllText(script, string.Join('\n',
        [
            "#!/bin/sh",
            "job=$(cat)",
            """id=$(printf '%s' "$job" | sed -E 's/.*"runId":"([^"]+)".*/\1/')""",
            $"sleep {seconds}",
            """printf '{"runId":"%s","status":3,"failureReason":null,"startedAt":"2026-11-16T12:00:00+00:00","finishedAt":"2026-11-16T12:00:03+00:00","steps":[],"bytesDownloaded":0}\n' "$id" """,
            "",
        ]));

        // A shell script, so this one test needs Linux or macOS, as CI is. The worker it stands in for is Linux only.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return script;
    }

    [Fact]
    public async Task ADispatcherThatDies_LeavesItsRunToBeReapedByTheNext_WhileALiveOneKeepsItsRunGoing()
    {
        // Short intervals, so that the whole story takes seconds: heartbeats every half second, a lease of three.
        ChangeableOptions Options() => new(new ExecutionOptions
        {
            WorkerPath = ExecutionSupport.WaitingWorker(),
            MaxConcurrentRuns = 1,
            HeartbeatInterval = TimeSpan.FromMilliseconds(500),
            LeaseTimeout = TimeSpan.FromSeconds(3),
            ReapInterval = TimeSpan.FromMilliseconds(500),
        });

        await using var services = _database.NewServices();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var first = new RunDispatcher(scopes, Options(), TimeProvider.System, NullLogger<RunDispatcher>.Instance);

        var runId = await ExecutionSupport.RunAsync(_database, _organization, _project);
        await first.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RunStatus.Running, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Running, TimeSpan.FromSeconds(20)));

        // Alive for longer than the lease: its heartbeats keep the run from being reaped, by itself or anybody.
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(RunStatus.Running, (await ExecutionSupport.StoredAsync(_database, runId)).Status);

        // The dispatcher goes away mid-run, as a process that is killed does, and its run is left saying "running".
        await first.StopAsync(CancellationToken.None);
        Assert.Equal(RunStatus.Running, (await ExecutionSupport.StoredAsync(_database, runId)).Status);

        var second = new RunDispatcher(scopes, Options(), TimeProvider.System, NullLogger<RunDispatcher>.Instance);
        await second.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(
                RunStatus.InfrastructureError,
                await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.InfrastructureError, TimeSpan.FromSeconds(20)));
        }
        finally
        {
            await second.StopAsync(CancellationToken.None);
        }
    }
}
