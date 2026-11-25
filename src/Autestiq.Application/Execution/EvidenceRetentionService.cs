using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Autestiq.Application.Audit;
using Autestiq.Domain.Projects;
using Autestiq.Persistence;

namespace Autestiq.Application.Execution;

/// <summary>How often expired evidence is looked for.</summary>
public sealed class EvidenceRetentionOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "EvidenceRetention";

    /// <summary>Time between sweeps. An hour: a screenshot outlives its policy by at most that.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How many runs one sweep deletes the evidence of, so one sweep is never one long transaction.</summary>
    public int RunsPerSweep { get; set; } = 200;
}

/// <summary>
/// Deletes the screenshots of runs whose project keeps them for less time than has passed (gap analysis G-5,
/// commercial-readiness G12). The run, its result and its steps are kept for good; only the evidence goes.
/// </summary>
/// <remarks>
/// <para>
/// Files first, then rows. A file that cannot be deleted leaves its row, so the next sweep tries again; a row deleted
/// before its file would leave a file nothing points to and nothing would ever find. A row whose file is already gone
/// serves nothing: the artifact endpoint answers "not found".
/// </para>
/// <para>
/// One audit entry per project and sweep says how many runs' screenshots were deleted under which policy, so deletion
/// can be shown as well as done.
/// </para>
/// </remarks>
public sealed partial class EvidenceRetentionService(
    IServiceScopeFactory scopes,
    IOptionsMonitor<EvidenceRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<EvidenceRetentionService> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A full sweep may be followed straight away by another; otherwise wait for the next.
                if (await SweepAsync(stoppingToken).ConfigureAwait(false) < options.CurrentValue.RunsPerSweep)
                {
                    await Task.Delay(options.CurrentValue.Interval, timeProvider, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                SweepFailed(logger, exception);
                await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Deletes the evidence of up to one sweep's runs whose time is up. Returns how many runs it handled.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();
        var store = scope.ServiceProvider.GetRequiredService<IArtifactStore>();
        var now = timeProvider.GetUtcNow();

        // Across organizations, as every background service is: each run is held to its own organization below. A
        // screenshot is due when it was captured longer ago than its project keeps them; a project that keeps none has
        // every screenshot due.
        var due = await (
                from artifact in database.Artifacts.IgnoreQueryFilters()
                join project in database.Projects.IgnoreQueryFilters()
                    on artifact.OrganizationId equals project.OrganizationId
                join run in database.TestRuns.IgnoreQueryFilters()
                    on new { artifact.OrganizationId, RunId = artifact.TestRunId, ProjectId = project.Id } equals new { run.OrganizationId, RunId = run.Id, run.ProjectId }
                where artifact.CapturedAt.AddDays(project.EvidenceRetentionDays) <= now
                select new { artifact.OrganizationId, artifact.TestRunId, ProjectId = project.Id, project.EvidenceRetentionDays })
            .Distinct()
            .Take(options.CurrentValue.RunsPerSweep)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var group in due.GroupBy(run => (run.OrganizationId, run.ProjectId, run.EvidenceRetentionDays)))
        {
            var deleted = 0;

            foreach (var run in group)
            {
                await store.DeleteRunAsync(run.OrganizationId, run.TestRunId, cancellationToken).ConfigureAwait(false);

                var artifacts = await database.Artifacts.IgnoreQueryFilters()
                    .Where(artifact => artifact.OrganizationId == run.OrganizationId && artifact.TestRunId == run.TestRunId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var steps = await database.TestRunSteps.IgnoreQueryFilters()
                    .Where(step => step.OrganizationId == run.OrganizationId && step.TestRunId == run.TestRunId && step.ScreenshotArtifactId != null)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var step in steps)
                {
                    step.ScreenshotArtifactId = null;
                }

                database.Artifacts.RemoveRange(artifacts);
                deleted += artifacts.Count;
            }

            new AuditWriter(database, timeProvider).Record(
                group.Key.OrganizationId,
                actorUserId: null,
                actorEmail: null,
                AuditActions.EvidenceExpired,
                nameof(Project),
                group.Key.ProjectId,
                subjectName: null,
                after: new { Runs = group.Count(), Screenshots = deleted, Days = group.Key.EvidenceRetentionDays });

            using (writeScope.AllowWritingTo(group.Key.OrganizationId))
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return due.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A sweep for expired evidence failed; it is tried again in a minute.")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}
