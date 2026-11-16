using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Autestiq.Contracts.Execution;
using Autestiq.Domain.Execution;
using Autestiq.Persistence;

namespace Autestiq.Application.Execution;

/// <summary>
/// Takes queued runs, gives each one to a worker, and writes back what the worker says.
/// </summary>
/// <remarks>
/// <para>
/// This is where the worker's result crosses back into our trust, so it is where the tenant has to
/// be re-established. The result names a run; this looks the run up, takes the organization from
/// the stored row, and writes under <see cref="TenantWriteScope"/>. The worker is never told an
/// organization and so has nothing to lie about (ADR-0018).
/// </para>
/// <para>
/// A worker is a process today and a container when there is more than one machine. The shape is
/// the same: a job in, a result out, and nothing shared.
/// </para>
/// </remarks>
public sealed class RunDispatcher(
    IServiceScopeFactory scopes,
    IOptionsMonitor<ExecutionOptions> options,
    TimeProvider timeProvider,
    ILogger<RunDispatcher> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var running = new SemaphoreSlim(options.CurrentValue.MaxConcurrentRuns);

        // Reaped first: a run left running by a dispatcher that stopped, this one or another, is not running.
        var nextReap = timeProvider.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (timeProvider.GetUtcNow() >= nextReap)
                {
                    await ReapAsync(stoppingToken).ConfigureAwait(false);
                    nextReap = timeProvider.GetUtcNow() + options.CurrentValue.ReapInterval;
                }

                var runId = await ClaimAsync(stoppingToken).ConfigureAwait(false);

                if (runId is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await running.WaitAsync(stoppingToken).ConfigureAwait(false);

                _ = CarryOutAsync(runId.Value, stoppingToken).ContinueWith(
                    _ => running.Release(),
                    TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // A dispatcher that stops dispatching is worse than one that logs and carries on.
                DispatcherLog.LoopFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Takes the next queued run and marks it running, so two dispatchers cannot take the same one.
    /// </summary>
    private async Task<Guid?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        // No request, so no organization: the dispatcher works across all of them by necessity and
        // writes back only to the one on the run it claimed.
        var run = await Claimable(database.TestRuns.IgnoreQueryFilters(), options.CurrentValue)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        run.Status = RunStatus.Running;
        run.StartedAt = timeProvider.GetUtcNow();
        run.HeartbeatAt = run.StartedAt;

        try
        {
            using (writeScope.AllowWritingTo(run.OrganizationId))
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another dispatcher took it first. Perfectly normal.
            return null;
        }

        return run.Id;
    }

    private async Task ReapAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var reaped = await ReapAbandonedAsync(
                scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(),
                scope.ServiceProvider.GetRequiredService<TenantWriteScope>(),
                timeProvider,
                options.CurrentValue.LeaseTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (reaped > 0)
        {
            DispatcherLog.Reaped(logger, reaped);
        }
    }

    /// <summary>
    /// Ends every running run whose dispatcher has gone quiet for longer than the lease, as an infrastructure
    /// error: the run did not fail, we lost it. Without this a run whose dispatcher died stayed "running" for
    /// ever, holding its place and saying something untrue. Returns how many were reaped.
    /// </summary>
    public static async Task<int> ReapAbandonedAsync(
        AutestiqDbContext database,
        TenantWriteScope writeScope,
        TimeProvider clock,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(writeScope);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.GetUtcNow();
        var cutoff = now - leaseTimeout;

        // Across organizations by necessity, like the claim: the dispatcher serves all of them, and each reaped run
        // is written back under its own organization below.
        var abandoned = await database.TestRuns
            .IgnoreQueryFilters()
            .Where(run => run.Status == RunStatus.Running && (run.HeartbeatAt ?? run.StartedAt ?? run.QueuedAt) < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var run in abandoned)
        {
            run.Status = RunStatus.InfrastructureError;
            run.FailureReason = "Abandoned: the dispatcher carrying it out stopped answering. The run did not fail; it was lost.";
            run.FinishedAt = now;

            new Audit.AuditWriter(database, clock).Record(
                run.OrganizationId,
                actorUserId: null,
                actorEmail: null,
                Audit.AuditActions.RunFinished,
                nameof(TestRun),
                run.Id,
                $"{run.ScenarioId} #{run.Number}",
                after: new { Status = run.Status.ToString() });

            using (writeScope.AllowWritingTo(run.OrganizationId))
            {
                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Its dispatcher answered after all, or another reaper got there first. Either way not ours.
                    database.ChangeTracker.Clear();
                }
            }
        }

        return abandoned.Count;
    }

    /// <summary>Says that a run is still being carried out.</summary>
    private async Task BeatAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        var run = await database.TestRuns.IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null || run.Status != RunStatus.Running)
        {
            return;
        }

        run.HeartbeatAt = timeProvider.GetUtcNow();

        using (writeScope.AllowWritingTo(run.OrganizationId))
        {
            try
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Somebody cancelled it in the same instant; the next heartbeat, or the result, settles it.
            }
        }
    }

    /// <summary>
    /// The queued runs a dispatcher may take, oldest first. None while every organization is paused, and none of a
    /// paused organization's: they stay queued until the switch is lifted.
    /// </summary>
    public static IQueryable<TestRun> Claimable(IQueryable<TestRun> runs, ExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Paused)
        {
            return runs.Where(_ => false);
        }

        var paused = options.PausedOrganizations.ToList();

        return runs
            .Where(candidate => candidate.Status == RunStatus.Queued && !candidate.CancellationRequested)
            .Where(candidate => !paused.Contains(candidate.OrganizationId))
            .OrderBy(candidate => candidate.QueuedAt);
    }

    private async Task CarryOutAsync(Guid runId, CancellationToken stoppingToken)
    {
        var workspace = Directory.CreateTempSubdirectory($"autestiq-run-{runId:N}-");

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();

            var run = await database.TestRuns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(candidate => candidate.Id == runId, stoppingToken)
                .ConfigureAwait(false);

            if (run is null)
            {
                return;
            }

            var job = JobFor(run, options.CurrentValue, workspace.FullName);

            var (result, stopped) = await RunWorkerAsync(job, run.OrganizationId, stoppingToken).ConfigureAwait(false);

            if (stopped != Stop.None)
            {
                // Said on the run as it is written back: the copy loaded before the worker started knows nothing
                // of a cancellation asked for since, and a paused run is stopped the same way.
                run.CancellationRequested = true;

                if (stopped == Stop.Paused)
                {
                    result = result with { FailureReason = "Stopped: runs were paused by an operator." };
                }
            }

            await IngestAsync(scope.ServiceProvider, run, result, workspace.FullName, stoppingToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DispatcherLog.RunFailed(logger, runId, exception);
            await MarkInfrastructureErrorAsync(runId, exception.Message).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                workspace.Delete(recursive: true);
            }
            catch (IOException)
            {
                // The artifacts are already copied out; a leftover temp directory is not worth failing for.
            }
        }
    }

    /// <summary>The job a worker is given for a run.</summary>
    /// <remarks>
    /// The origins the worker may reach on its own machine come from configuration only, never from the run
    /// (R-11). A run's target is stored data, and once a customer can name one, taking it as trusted would
    /// make the loopback exception a route to whatever listens on the worker's host. A stored target that
    /// configuration does not name is reached, if at all, as any other address is: never on loopback.
    /// </remarks>
    public static WorkerJob JobFor(TestRun run, ExecutionOptions options, string workspace)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(options);

        return new WorkerJob(
            run.Id,
            run.ScenarioId,
            run.TargetOrigin,
            AllowedOrigins: [],
            TrustedLocalOrigins: options.TrustedLocalOrigins(),
            RunBudget.Default,
            workspace);
    }

    /// <summary>Launches the worker, feeds it the job, and waits for its result or a cancellation.</summary>
    private async Task<(WorkerResult Result, Stop Stopped)> RunWorkerAsync(WorkerJob job, Guid organizationId, CancellationToken stoppingToken)
    {
        var runId = job.RunId;
        var workerPath = options.CurrentValue.WorkerPath
            ?? throw new InvalidOperationException(
                $"No worker is configured. Set {ExecutionOptions.SectionName}:{nameof(ExecutionOptions.WorkerPath)} "
                + "to the Autestiq.Worker executable.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(workerPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();

        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(job, Wire)).ConfigureAwait(false);
        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEndAsync(stoppingToken);
        var errors = process.StandardError.ReadToEndAsync(stoppingToken);

        var stopped = await WaitOrStopAsync(process, runId, organizationId, stoppingToken).ConfigureAwait(false);

        var text = (await output.ConfigureAwait(false)).Trim();

        if (text.Length == 0)
        {
            var why = (await errors.ConfigureAwait(false)).Trim();

            return (new WorkerResult(
                job.RunId,
                RunStatus.InfrastructureError,
                why.Length > 0 ? FirstLine(why) : "The worker produced no result.",
                timeProvider.GetUtcNow(),
                timeProvider.GetUtcNow(),
                [],
                0), stopped);
        }

        return (JsonSerializer.Deserialize<WorkerResult>(text, Wire)
            ?? throw new JsonException("The worker's result could not be read."), stopped);
    }

    /// <summary>Why a worker was stopped before it finished.</summary>
    private enum Stop
    {
        None,
        Cancelled,
        Paused,
    }

    /// <summary>
    /// Waits for the worker, watching for somebody asking the run to stop, and for the kill switch. A stop kills
    /// the process; the worker's own handler reports what it managed, and if it cannot, the result is empty and
    /// becomes a cancellation here.
    /// </summary>
    private async Task<Stop> WaitOrStopAsync(Process process, Guid runId, Guid organizationId, CancellationToken stoppingToken)
    {
        var finished = process.WaitForExitAsync(stoppingToken);
        var nextBeat = timeProvider.GetUtcNow() + options.CurrentValue.HeartbeatInterval;

        while (!finished.IsCompleted)
        {
            if (timeProvider.GetUtcNow() >= nextBeat)
            {
                await BeatAsync(runId, stoppingToken).ConfigureAwait(false);
                nextBeat = timeProvider.GetUtcNow() + options.CurrentValue.HeartbeatInterval;
            }

            var completed = await Task.WhenAny(finished, Task.Delay(TimeSpan.FromMilliseconds(500), timeProvider, stoppingToken))
                .ConfigureAwait(false);

            if (completed == finished)
            {
                return Stop.None;
            }

            // The switch first: it is read from memory, and a paused run should not wait on a database round trip.
            var stop = options.CurrentValue.IsPaused(organizationId) ? Stop.Paused
                : await IsCancellationRequestedAsync(runId, stoppingToken).ConfigureAwait(false) ? Stop.Cancelled
                : Stop.None;

            if (stop != Stop.None)
            {
                DispatcherLog.Cancelling(logger, runId);

                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It finished between the check and the kill.
                }

                await finished.ConfigureAwait(false);
                return stop;
            }
        }

        return Stop.None;
    }

    private async Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();

        return await database.TestRuns
            .IgnoreQueryFilters()
            .Where(run => run.Id == runId)
            .Select(run => run.CancellationRequested)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes back what the worker said, under the organization taken from the stored run.
    /// </summary>
    private async Task IngestAsync(
        IServiceProvider services,
        TestRun run,
        WorkerResult result,
        string workspace,
        CancellationToken cancellationToken)
    {
        // The one check that makes the protocol safe: a result is about the run it was sent for,
        // or it is about nothing. The worker never learns another run's id to offer.
        if (result.RunId != run.Id)
        {
            DispatcherLog.WrongRun(logger, run.Id, result.RunId);
            result = result with { RunId = run.Id, Status = RunStatus.InfrastructureError, FailureReason = "The worker answered about a different run." };
        }

        var database = services.GetRequiredService<AutestiqDbContext>();
        var writeScope = services.GetRequiredService<TenantWriteScope>();
        var artifacts = services.GetRequiredService<IArtifactStore>();

        run.Status = run.CancellationRequested && result.Status != RunStatus.Passed
            ? RunStatus.Cancelled
            : result.Status;

        run.FailureReason = result.FailureReason;
        run.StartedAt ??= result.StartedAt;
        run.FinishedAt = result.FinishedAt;
        run.BytesDownloaded = result.BytesDownloaded;

        using (writeScope.AllowWritingTo(run.OrganizationId))
        {
            foreach (var step in result.Steps)
            {
                var screenshot = await StoreScreenshotAsync(
                        artifacts, database, run, step, workspace, cancellationToken)
                    .ConfigureAwait(false);

                database.TestRunSteps.Add(new TestRunStep
                {
                    OrganizationId = run.OrganizationId,
                    TestRunId = run.Id,
                    Order = step.Order,
                    Description = step.Description,
                    Status = step.Status,
                    StartedAt = step.StartedAt,
                    FinishedAt = step.FinishedAt,
                    Message = step.Message,
                    ScreenshotArtifactId = screenshot,
                });
            }

            new Audit.AuditWriter(database, timeProvider).Record(
                run.OrganizationId,
                actorUserId: null,
                actorEmail: null,
                Audit.AuditActions.RunFinished,
                nameof(TestRun),
                run.Id,
                $"{run.ScenarioId} #{run.Number}",
                after: new { Status = run.Status.ToString() });

            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        DispatcherLog.Finished(logger, run.Id, run.Status);
    }

    private static async Task<Guid?> StoreScreenshotAsync(
        IArtifactStore artifacts,
        AutestiqDbContext database,
        TestRun run,
        WorkerStepResult step,
        string workspace,
        CancellationToken cancellationToken)
    {
        if (step.ScreenshotFile is not { Length: > 0 } fileName)
        {
            return null;
        }

        // The worker names a file; this decides where it lives. A worker cannot write outside its
        // own workspace, and cannot choose a key.
        var path = Path.Combine(workspace, Path.GetFileName(fileName));

        if (!File.Exists(path))
        {
            return null;
        }

        await using var content = File.OpenRead(path);

        var key = await artifacts
            .SaveAsync(run.OrganizationId, run.Id, Path.GetFileName(fileName), content, cancellationToken)
            .ConfigureAwait(false);

        var artifact = new Artifact
        {
            OrganizationId = run.OrganizationId,
            TestRunId = run.Id,
            Kind = ArtifactKind.Screenshot,
            StorageKey = key,
            ContentType = "image/png",
            SizeBytes = new FileInfo(path).Length,
            CapturedAt = step.FinishedAt ?? step.StartedAt ?? DateTimeOffset.UtcNow,
        };

        database.Artifacts.Add(artifact);
        return artifact.Id;
    }

    private async Task MarkInfrastructureErrorAsync(Guid runId, string reason)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        var run = await database.TestRuns.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == runId)
            .ConfigureAwait(false);

        if (run is null)
        {
            return;
        }

        run.Status = RunStatus.InfrastructureError;
        run.FailureReason = FirstLine(reason);
        run.FinishedAt = timeProvider.GetUtcNow();

        using (writeScope.AllowWritingTo(run.OrganizationId))
        {
            await database.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private static string FirstLine(string message)
    {
        var line = message.Split('\n')[0].Trim();
        return line.Length > 500 ? line[..500] : line;
    }
}

/// <summary>What the dispatcher says about itself.</summary>
internal static partial class DispatcherLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "The run dispatcher's loop failed.")]
    public static partial void LoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} could not be carried out.")]
    public static partial void RunFailed(ILogger logger, Guid runId, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} was asked to stop.")]
    public static partial void Cancelling(ILogger logger, Guid runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reaped {Count} run(s) whose dispatcher stopped answering.")]
    public static partial void Reaped(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} ended as {Status}.")]
    public static partial void Finished(ILogger logger, Guid runId, RunStatus status);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A worker sent for run {Expected} answered about run {Received}. Refused.")]
    public static partial void WrongRun(ILogger logger, Guid expected, Guid received);
}
