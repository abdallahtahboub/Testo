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
                var checkId = runId is null ? await ClaimProofCheckAsync(stoppingToken).ConfigureAwait(false) : null;

                if (runId is null && checkId is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await running.WaitAsync(stoppingToken).ConfigureAwait(false);

                var work = runId is { } run ? CarryOutAsync(run, stoppingToken) : CheckOriginAsync(checkId!.Value, stoppingToken); // one of the two is set: checked above.

                _ = work.ContinueWith(
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

        if (options.CurrentValue.Isolation == WorkerIsolation.Container)
        {
            await RemoveLeftoversAsync(scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the containers and networks of runs and checks that are no longer going: what a dispatcher that died
    /// mid-run left behind. Found by their label, which holds the run's or the check's id.
    /// </summary>
    private async Task RemoveLeftoversAsync(AutestiqDbContext database, CancellationToken cancellationToken)
    {
        var docker = new DockerCommand(options.CurrentValue.Docker);
        var ids = new HashSet<Guid>();

        foreach (var command in new[] { SealedRun.ListContainers(), SealedRun.ListNetworks() })
        {
            var (exitCode, output, _) = await docker.RunAsync(command, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return;
            }

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(line, out var id))
                {
                    ids.Add(id);
                }
            }
        }

        foreach (var id in ids)
        {
            // Across organizations: the dispatcher serves all of them, and only reads here.
            var going = await database.TestRuns.IgnoreQueryFilters().AnyAsync(run => run.Id == id && run.Status == RunStatus.Running, cancellationToken).ConfigureAwait(false)
                || await database.OriginProofs.IgnoreQueryFilters().AnyAsync(proof => proof.Id == id && proof.Status == OriginProofStatus.Checking, cancellationToken).ConfigureAwait(false);

            if (!going)
            {
                await RemoveSealedRunAsync(docker, id).ConfigureAwait(false);
            }
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

        // Checks too: a check whose dispatcher went quiet is a failed check, and can be asked for again.
        var lostChecks = await database.OriginProofs
            .IgnoreQueryFilters()
            .Where(proof => proof.Status == OriginProofStatus.Checking && proof.CheckStartedAt < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var proof in lostChecks)
        {
            proof.Status = OriginProofStatus.Failed;
            proof.CheckStartedAt = null;
            proof.CheckedAt = now;
            proof.FailureReason = "The check was lost: the dispatcher carrying it out stopped answering. Ask for it again.";

            using (writeScope.AllowWritingTo(proof.OrganizationId))
            {
                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    database.ChangeTracker.Clear();
                }
            }
        }

        return abandoned.Count + lostChecks.Count;
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

            if (!await MayTargetAsync(database, run, options.CurrentValue, timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false))
            {
                // Not ours, and not proved by the organization that asked: no worker is started at all.
                await RefuseAsync(scope.ServiceProvider, run, stoppingToken).ConfigureAwait(false);
                return;
            }

            var job = JobFor(run, options.CurrentValue, workspace.FullName);

            var (result, stopped) = await RunWorkerAsync(job, run.OrganizationId, stoppingToken).ConfigureAwait(false);

            // Read again before writing back: while the worker ran, heartbeats and perhaps a cancellation changed the
            // row, and saving the copy loaded before it started is refused as a stale write. That turned every run
            // longer than a heartbeat into an infrastructure error.
            await database.Entry(run).ReloadAsync(stoppingToken).ConfigureAwait(false);

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

            var billable = Usage.UsageMeter.IsBillable(result, stoppedByOperator: stopped == Stop.Paused);

            await IngestAsync(scope.ServiceProvider, run, result, billable, workspace.FullName, stoppingToken)
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

    /// <summary>
    /// Whether a run may be aimed at its target: one of our own configured origins, or an origin the run's own
    /// organization has proved it controls, with a proof that has not expired. Another organization's proof of the
    /// same origin counts for nothing.
    /// </summary>
    public static async Task<bool> MayTargetAsync(
        AutestiqDbContext database,
        TestRun run,
        ExecutionOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(options);

        if (Origins.Normalize(run.TargetOrigin) is not { } target)
        {
            return false;
        }

        if (options.TrustedLocalOrigins().Any(origin => Origins.Normalize(origin) == target))
        {
            return true;
        }

        // An origin that is not ours is reached only from a sealed container (ADR-0026). A proof is not enough on
        // its own: in process mode a browser exploit could reach whatever this machine can.
        if (options.Isolation != WorkerIsolation.Container)
        {
            return false;
        }

        // Across the filter, because the dispatcher has no organization of its own; held to the run's below.
        var proof = await database.OriginProofs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == run.OrganizationId && candidate.Origin == target, cancellationToken)
            .ConfigureAwait(false);

        return proof is not null && proof.CountsAt(now);
    }

    private async Task RefuseAsync(IServiceProvider services, TestRun run, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<AutestiqDbContext>();
        var writeScope = services.GetRequiredService<TenantWriteScope>();

        run.Status = RunStatus.Cancelled;
        run.FailureReason = $"Refused: this organization has not proved that it controls {run.TargetOrigin}, or its proof has expired.";
        run.FinishedAt = timeProvider.GetUtcNow();

        using (writeScope.AllowWritingTo(run.OrganizationId))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        DispatcherLog.Refused(logger, run.Id);
    }

    /// <summary>Takes the next waiting proof check, so two dispatchers cannot take the same one.</summary>
    private async Task<Guid?> ClaimProofCheckAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();
        var current = options.CurrentValue;

        if (current.Paused)
        {
            return null;
        }

        var paused = current.PausedOrganizations.ToList();

        // Across organizations, like the claim of a run, and written back under the one it belongs to.
        var proof = await database.OriginProofs
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Status == OriginProofStatus.Checking && candidate.CheckStartedAt == null)
            .Where(candidate => !paused.Contains(candidate.OrganizationId))
            .OrderBy(candidate => candidate.CheckRequestedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (proof is null)
        {
            return null;
        }

        proof.CheckStartedAt = timeProvider.GetUtcNow();

        try
        {
            using (writeScope.AllowWritingTo(proof.OrganizationId))
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return proof.Id;
    }

    /// <summary>Has a worker check an origin's proof file, and writes back what it found.</summary>
    private async Task CheckOriginAsync(Guid proofId, CancellationToken stoppingToken)
    {
        var workspace = Directory.CreateTempSubdirectory($"autestiq-proof-{proofId:N}-");

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
            var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

            var proof = await database.OriginProofs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(candidate => candidate.Id == proofId, stoppingToken)
                .ConfigureAwait(false);

            if (proof is null)
            {
                return;
            }

            WorkerResult result;

            try
            {
                (result, _) = await RunWorkerAsync(ProofJobFor(proof, workspace.FullName), proof.OrganizationId, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                DispatcherLog.CheckFailed(logger, proofId, exception);
                result = new WorkerResult(proofId, RunStatus.InfrastructureError, "The check could not be carried out.", timeProvider.GetUtcNow(), timeProvider.GetUtcNow(), [], 0);
            }

            ApplyProofResult(proof, result, timeProvider.GetUtcNow(), options.CurrentValue.ProofValidity);

            using (writeScope.AllowWritingTo(proof.OrganizationId))
            {
                await database.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                workspace.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Nothing in it is kept; a leftover temporary directory is not worth failing for.
            }
        }
    }

    /// <summary>
    /// The job for checking an origin's proof. The origin is the one origin allowed and is never trusted-local, so
    /// one that resolves to a private, loopback or metadata address is refused by the worker's proxy (R-11).
    /// </summary>
    public static WorkerJob ProofJobFor(OriginProof proof, string workspace)
    {
        ArgumentNullException.ThrowIfNull(proof);

        return new WorkerJob(
            proof.Id,
            "verify-origin",
            proof.Origin,
            AllowedOrigins: [proof.Origin],
            TrustedLocalOrigins: [],
            new RunBudget(TimeSpan.FromSeconds(30), MaxSteps: 5, MaxBytesDownloaded: 1024 * 1024),
            workspace,
            WorkerTask.VerifyOrigin,
            OriginProof: proof.Token);
    }

    /// <summary>What a worker's check means for the proof: verified for a while, or failed with the reason.</summary>
    public static void ApplyProofResult(OriginProof proof, WorkerResult result, DateTimeOffset now, TimeSpan validity)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(result);

        proof.CheckedAt = now;
        proof.CheckStartedAt = null;

        if (result.RunId == proof.Id && result.Status == RunStatus.Passed)
        {
            proof.Status = OriginProofStatus.Verified;
            proof.VerifiedAt = now;
            proof.ExpiresAt = now + validity;
            proof.FailureReason = null;
        }
        else
        {
            proof.Status = OriginProofStatus.Failed;
            proof.FailureReason = result.RunId == proof.Id ? FirstLine(result.FailureReason ?? "The origin did not hold the token.") : "The worker answered about a different check.";
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

        var trusted = options.TrustedLocalOrigins();
        var target = Origins.Normalize(run.TargetOrigin);
        var ours = trusted.Any(origin => Origins.Normalize(origin) == target);

        return new WorkerJob(
            run.Id,
            run.ScenarioId,
            run.TargetOrigin,

            // A target that is not ours is allowed by name, and every address it resolves to is judged as public.
            AllowedOrigins: ours || target is null ? [] : [target],
            TrustedLocalOrigins: trusted,
            RunBudget.Default,
            workspace);
    }

    /// <summary>Launches the worker, feeds it the job, and waits for its result or a cancellation.</summary>
    private Task<(WorkerResult Result, Stop Stopped)> RunWorkerAsync(WorkerJob job, Guid organizationId, CancellationToken stoppingToken) =>
        options.CurrentValue.Isolation == WorkerIsolation.Container
            ? RunInContainerAsync(job, organizationId, stoppingToken)
            : RunAsProcessAsync(job, organizationId, stoppingToken);

    private async Task<(WorkerResult Result, Stop Stopped)> RunAsProcessAsync(WorkerJob job, Guid organizationId, CancellationToken stoppingToken)
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

        var stopped = await WaitOrStopAsync(process, runId, organizationId, () => KillProcess(process), stoppingToken).ConfigureAwait(false);

        return (ResultOf(job, (await output.ConfigureAwait(false)).Trim(), (await errors.ConfigureAwait(false)).Trim()), stopped);
    }

    private static Task KillProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It finished between the check and the kill.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Carries a run out in a sealed container (ADR-0026): a network of its own, the outer proxy on it as the only
    /// way out, the worker locked down on it, and all three removed afterwards however the run ended.
    /// </summary>
    private async Task<(WorkerResult Result, Stop Stopped)> RunInContainerAsync(WorkerJob job, Guid organizationId, CancellationToken stoppingToken)
    {
        var current = options.CurrentValue;
        var docker = new DockerCommand(current.Docker);
        var runId = job.RunId;

        try
        {
            if (await PrepareSealedRunAsync(docker, job, current.WorkerImage, stoppingToken).ConfigureAwait(false) is not { } proxy)
            {
                return (Infrastructure(job, "The run's sealed container could not be prepared."), Stop.None);
            }

            // The worker runs as an unprivileged user and writes its screenshots into the run's own folder.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(job.ArtifactDirectory, (UnixFileMode)0b111_111_111);
            }

            using var process = Process.Start(docker.Start(SealedRun.RunWorker(runId, current.WorkerImage, job.ArtifactDirectory, proxy), input: true))
                ?? throw new InvalidOperationException("The worker's container would not start.");

            // Inside the container the workspace is at a fixed place; the worker names files relative to it.
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(job with { ArtifactDirectory = SealedRun.Workspace }, Wire)).ConfigureAwait(false);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEndAsync(stoppingToken);
            var errors = process.StandardError.ReadToEndAsync(stoppingToken);

            // A stop kills the container, not only the client attached to it, which would leave the browser running.
            var stopped = await WaitOrStopAsync(
                    process,
                    runId,
                    organizationId,
                    () => docker.RunAsync(SealedRun.KillWorker(runId), CancellationToken.None),
                    stoppingToken)
                .ConfigureAwait(false);

            return (ResultOf(job, (await output.ConfigureAwait(false)).Trim(), (await errors.ConfigureAwait(false)).Trim()), stopped);
        }
        finally
        {
            // Whatever happened. A dispatcher that dies before this line leaves labelled leftovers, which the clean-up
            // pass removes.
            await RemoveSealedRunAsync(docker, runId).ConfigureAwait(false);
        }
    }

    /// <summary>The run's network and outer proxy, ready; the proxy's address on the network, or null.</summary>
    private async Task<string?> PrepareSealedRunAsync(DockerCommand docker, WorkerJob job, string image, CancellationToken cancellationToken)
    {
        var runId = job.RunId;
        var origins = job.AllowedOrigins.Count > 0 ? job.AllowedOrigins : [Origins.Normalize(job.TargetOrigin) ?? job.TargetOrigin];

        foreach (var command in new[] { SealedRun.CreateNetwork(runId), SealedRun.RunProxy(runId, image, origins), SealedRun.ConnectProxy(runId) })
        {
            var (exitCode, _, errors) = await docker.RunAsync(command, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                DispatcherLog.ContainerStep(logger, runId, command[0] + " " + command[1], FirstLine(errors));
                return null;
            }
        }

        var address = (await docker.RunAsync(SealedRun.ProxyAddress(runId), cancellationToken).ConfigureAwait(false)).Output;

        if (address.Length == 0)
        {
            return null;
        }

        // Ready when it says so, and not before: a worker that starts first finds nothing to talk to.
        var deadline = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(20);

        while (timeProvider.GetUtcNow() < deadline)
        {
            if ((await docker.RunAsync(SealedRun.ProxyLogs(runId), cancellationToken).ConfigureAwait(false)).Output.Contains(SealedRun.ProxyReady, StringComparison.Ordinal))
            {
                return $"{address}:{SealedRun.ProxyPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeProvider, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task RemoveSealedRunAsync(DockerCommand docker, Guid runId)
    {
        await docker.RunAsync(SealedRun.RemoveContainers(runId), CancellationToken.None).ConfigureAwait(false);
        await docker.RunAsync(SealedRun.RemoveNetwork(runId), CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>What a worker's standard output says, or an infrastructure error saying why there is nothing.</summary>
    private WorkerResult ResultOf(WorkerJob job, string output, string errors)
    {
        if (output.Length == 0)
        {
            return Infrastructure(job, errors.Length > 0 ? FirstLine(errors) : "The worker produced no result.");
        }

        return JsonSerializer.Deserialize<WorkerResult>(output, Wire)
            ?? throw new JsonException("The worker's result could not be read.");
    }

    private WorkerResult Infrastructure(WorkerJob job, string reason) =>
        new(job.RunId, RunStatus.InfrastructureError, reason, timeProvider.GetUtcNow(), timeProvider.GetUtcNow(), [], 0);

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
    private async Task<Stop> WaitOrStopAsync(Process process, Guid runId, Guid organizationId, Func<Task> kill, CancellationToken stoppingToken)
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

                await kill().ConfigureAwait(false);
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
        bool billable,
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
            long artifactBytes = 0;

            foreach (var step in result.Steps)
            {
                var screenshot = await StoreScreenshotAsync(
                        artifacts, database, run, step, workspace, cancellationToken)
                    .ConfigureAwait(false);

                artifactBytes += database.ChangeTracker.Entries<Artifact>()
                    .Where(entry => entry.State == EntityState.Added && entry.Entity.Id == screenshot)
                    .Sum(entry => entry.Entity.SizeBytes);

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

            // What the run used, in the same save as its result, so neither is ever written without the other. Each
            // event's key names the run, so a result written back twice records nothing twice.
            foreach (var usage in Usage.UsageMeter.ForRun(run, result, billable, artifactBytes, timeProvider.GetUtcNow()))
            {
                var known = await database.UsageEvents.IgnoreQueryFilters()
                    .AnyAsync(existing => existing.OrganizationId == usage.OrganizationId && existing.IdempotencyKey == usage.IdempotencyKey, cancellationToken)
                    .ConfigureAwait(false);

                if (!known)
                {
                    database.UsageEvents.Add(usage);
                }
            }

            run.ReservedSeconds = 0;

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId}: preparing its sealed container failed at '{Step}': {Reason}")]
    public static partial void ContainerStep(ILogger logger, Guid runId, string step, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} was refused: its target is not proved.")]
    public static partial void Refused(ILogger logger, Guid runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The check of proof {ProofId} could not be carried out.")]
    public static partial void CheckFailed(ILogger logger, Guid proofId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reaped {Count} run(s) or check(s) whose dispatcher stopped answering.")]
    public static partial void Reaped(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} ended as {Status}.")]
    public static partial void Finished(ILogger logger, Guid runId, RunStatus status);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A worker sent for run {Expected} answered about run {Received}. Refused.")]
    public static partial void WrongRun(ILogger logger, Guid expected, Guid received);
}
