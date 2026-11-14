using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testo.Contracts.Execution;
using Testo.Domain.Execution;
using Testo.Persistence;

namespace Testo.Application.Execution;

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
    IOptions<ExecutionOptions> options,
    TimeProvider timeProvider,
    ILogger<RunDispatcher> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var running = new SemaphoreSlim(options.Value.MaxConcurrentRuns);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
        var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        // No request, so no organization: the dispatcher works across all of them by necessity and
        // writes back only to the one on the run it claimed.
        var run = await database.TestRuns
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Status == RunStatus.Queued && !candidate.CancellationRequested)
            .OrderBy(candidate => candidate.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        run.Status = RunStatus.Running;
        run.StartedAt = timeProvider.GetUtcNow();

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

    private async Task CarryOutAsync(Guid runId, CancellationToken stoppingToken)
    {
        var workspace = Directory.CreateTempSubdirectory($"testo-run-{runId:N}-");

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();

            var run = await database.TestRuns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(candidate => candidate.Id == runId, stoppingToken)
                .ConfigureAwait(false);

            if (run is null)
            {
                return;
            }

            var job = new WorkerJob(
                run.Id,
                run.ScenarioId,
                run.TargetOrigin,
                AllowedOrigins: [],
                TrustedLocalOrigins: [run.TargetOrigin],
                RunBudget.Default,
                workspace.FullName);

            var result = await RunWorkerAsync(job, runId, stoppingToken).ConfigureAwait(false);

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

    /// <summary>Launches the worker, feeds it the job, and waits for its result or a cancellation.</summary>
    private async Task<WorkerResult> RunWorkerAsync(WorkerJob job, Guid runId, CancellationToken stoppingToken)
    {
        var workerPath = options.Value.WorkerPath
            ?? throw new InvalidOperationException(
                $"No worker is configured. Set {ExecutionOptions.SectionName}:{nameof(ExecutionOptions.WorkerPath)} "
                + "to the Testo.Worker executable.");

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

        await WaitOrCancelAsync(process, runId, stoppingToken).ConfigureAwait(false);

        var text = (await output.ConfigureAwait(false)).Trim();

        if (text.Length == 0)
        {
            var why = (await errors.ConfigureAwait(false)).Trim();

            return new WorkerResult(
                job.RunId,
                RunStatus.InfrastructureError,
                why.Length > 0 ? FirstLine(why) : "The worker produced no result.",
                timeProvider.GetUtcNow(),
                timeProvider.GetUtcNow(),
                [],
                0);
        }

        return JsonSerializer.Deserialize<WorkerResult>(text, Wire)
            ?? throw new JsonException("The worker's result could not be read.");
    }

    /// <summary>
    /// Waits for the worker, watching for somebody asking the run to stop. A stop kills the
    /// process; the worker's own handler reports what it managed, and if it cannot, the result is
    /// empty and becomes a cancellation here.
    /// </summary>
    private async Task WaitOrCancelAsync(Process process, Guid runId, CancellationToken stoppingToken)
    {
        var finished = process.WaitForExitAsync(stoppingToken);

        while (!finished.IsCompleted)
        {
            var completed = await Task.WhenAny(finished, Task.Delay(TimeSpan.FromMilliseconds(500), timeProvider, stoppingToken))
                .ConfigureAwait(false);

            if (completed == finished)
            {
                return;
            }

            if (await IsCancellationRequestedAsync(runId, stoppingToken).ConfigureAwait(false))
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
                return;
            }
        }
    }

    private async Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();

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

        var database = services.GetRequiredService<TestoDbContext>();
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
        TestoDbContext database,
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
        var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} ended as {Status}.")]
    public static partial void Finished(ILogger logger, Guid runId, RunStatus status);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A worker sent for run {Expected} answered about run {Received}. Refused.")]
    public static partial void WrongRun(ILogger logger, Guid expected, Guid received);
}
