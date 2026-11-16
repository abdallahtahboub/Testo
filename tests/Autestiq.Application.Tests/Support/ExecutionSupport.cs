using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Autestiq.Application.Execution;
using Autestiq.Contracts.Execution;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Projects;

namespace Autestiq.Application.Tests.Support;

/// <summary>What the execution tests share: a project, a run in a given state, a worker that only waits.</summary>
internal static class ExecutionSupport
{
    public static async Task<Guid> ProjectAsync(TestDatabase database, Guid organizationId, string prefix)
    {
        await using var context = database.NewContext();

        var project = new Project { OrganizationId = organizationId, Name = prefix, KeyPrefix = prefix };
        context.Projects.Add(project);

        using (database.WriteScope.AllowWritingTo(organizationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return project.Id;
    }

    /// <summary>A run of the demo basket scenario, queued unless told otherwise.</summary>
    public static async Task<Guid> RunAsync(
        TestDatabase database,
        Guid organizationId,
        Guid projectId,
        Action<TestRun>? shape = null)
    {
        await using var context = database.NewContext();

        var run = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = Random.Shared.Next(1, 100_000),
            ScenarioId = Scenarios.DemoShopBasket,
            TargetOrigin = "http://127.0.0.1:5090",
            QueuedAt = DateTimeOffset.UtcNow,
        };

        shape?.Invoke(run);
        context.TestRuns.Add(run);

        using (database.WriteScope.AllowWritingTo(organizationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return run.Id;
    }

    public static async Task<TestRun> StoredAsync(TestDatabase database, Guid runId)
    {
        await using var context = database.NewContext();
        return await context.TestRuns.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(run => run.Id == runId, TestContext.Current.CancellationToken);
    }

    /// <summary>Polls a run's stored status until it is the one expected or the time is up.</summary>
    public static async Task<RunStatus> StatusOnceItIsAsync(TestDatabase database, Guid runId, RunStatus expected, TimeSpan within)
    {
        var deadline = Stopwatch.StartNew();
        var status = RunStatus.Queued;

        while (deadline.Elapsed < within)
        {
            status = (await StoredAsync(database, runId)).Status;

            if (status == expected)
            {
                return status;
            }

            // Real time: a real process is being waited on.
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        return status;
    }

    /// <summary>An executable that waits a minute and says nothing: a run that stays running until stopped.</summary>
    public static string WaitingWorker()
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
}

/// <summary>Options a test can change while the dispatcher reads them, as a configuration reload would.</summary>
internal sealed class ChangeableOptions(ExecutionOptions initial) : IOptionsMonitor<ExecutionOptions>
{
    public ExecutionOptions CurrentValue { get; } = initial;

    public ExecutionOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<ExecutionOptions, string?> listener) => null;

    public void Change(Action<ExecutionOptions> change) => change(CurrentValue);
}

/// <summary>A clock that says what it is told.</summary>
internal sealed class SetClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
