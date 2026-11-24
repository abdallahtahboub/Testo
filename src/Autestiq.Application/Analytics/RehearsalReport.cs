using Microsoft.EntityFrameworkCore;
using Autestiq.Persistence;

namespace Autestiq.Application.Analytics;

/// <summary>
/// The rehearsal's measures for one organization (Milestone 5.1 R6), read from the content-free analytics views
/// (<c>analytics_tests</c>, <c>analytics_runs</c>, <c>analytics_planning_attempts</c>; data-architecture.md §7).
/// Medians are in seconds; <c>null</c> where nothing has happened yet.
/// </summary>
public sealed record RehearsalMeasures
{
    /// <summary>Tests described.</summary>
    public long TestsDescribed { get; init; }

    /// <summary>Median time from describing a test to its first approval.</summary>
    public double? MedianSecondsToFirstApproval { get; init; }

    /// <summary>Median time from describing a test to its first verdict (an approved run that passed or failed).</summary>
    public double? MedianSecondsToFirstVerdict { get; init; }

    /// <summary>
    /// Corrections a person made per test: expected results changed, approvals withdrawn, and planning attempts beyond
    /// the first.
    /// </summary>
    public double? CorrectionsPerTest { get; init; }

    /// <summary>Plans that cover the whole scenario.</summary>
    public long FullPlans { get; init; }

    /// <summary>Plans that cover part of it.</summary>
    public long PartialPlans { get; init; }

    /// <summary>Runs of approved tests that reached a verdict (passed or failed).</summary>
    public long RunsWithVerdict { get; init; }

    /// <summary>Runs of approved tests that ended inconclusive.</summary>
    public long RunsInconclusive { get; init; }

    /// <summary>Runs of approved tests that ended in an error, a timeout or a budget.</summary>
    public long RunsInError { get; init; }

    /// <summary>Median time a run waited in the queue before a worker took it, every kind of run.</summary>
    public double? MedianQueueWaitSeconds { get; init; }

    /// <summary>Days on which anybody in the organization described a test or started a run.</summary>
    public long ActiveDays { get; init; }

    /// <summary>People who came back: active on two or more days.</summary>
    public long PeopleReturning { get; init; }

    /// <summary>Planning and run costs together, per approved run that ended with a verdict or inconclusive.</summary>
    public decimal? CostPerCompletedRun { get; init; }
}

/// <summary>Reads <see cref="RehearsalMeasures"/> for an organization. Operator use: it crosses no tenant filter itself.</summary>
public static class RehearsalReport
{
    /// <summary>The measures for one organization, from the analytics views.</summary>
    public static async Task<RehearsalMeasures> ForAsync(AutestiqDbContext database, Guid organizationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // Parameterized by EF from the interpolation; the views carry no content, so nothing here can return any.
        return await database.Database.SqlQuery<RehearsalMeasures>($"""
            SELECT
                (SELECT count(*) FROM analytics_tests WHERE organization_id = {organizationId}) AS "TestsDescribed",
                (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY seconds_to_first_approval) FROM analytics_tests
                    WHERE organization_id = {organizationId} AND seconds_to_first_approval IS NOT NULL) AS "MedianSecondsToFirstApproval",
                (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY seconds_to_first_verdict) FROM analytics_tests
                    WHERE organization_id = {organizationId} AND seconds_to_first_verdict IS NOT NULL) AS "MedianSecondsToFirstVerdict",
                (SELECT avg(expected_results_edits + approvals_withdrawn + GREATEST(planning_attempts - 1, 0))::double precision
                    FROM analytics_tests WHERE organization_id = {organizationId}) AS "CorrectionsPerTest",
                (SELECT count(*) FROM analytics_tests WHERE organization_id = {organizationId} AND coverage = 'full') AS "FullPlans",
                (SELECT count(*) FROM analytics_tests WHERE organization_id = {organizationId} AND coverage = 'partial') AS "PartialPlans",
                (SELECT count(*) FROM analytics_runs WHERE organization_id = {organizationId} AND kind = 'approved-plan' AND outcome = 'verdict') AS "RunsWithVerdict",
                (SELECT count(*) FROM analytics_runs WHERE organization_id = {organizationId} AND kind = 'approved-plan' AND outcome = 'inconclusive') AS "RunsInconclusive",
                (SELECT count(*) FROM analytics_runs WHERE organization_id = {organizationId} AND kind = 'approved-plan' AND outcome = 'error') AS "RunsInError",
                (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY queue_wait_seconds) FROM analytics_runs
                    WHERE organization_id = {organizationId} AND queue_wait_seconds IS NOT NULL) AS "MedianQueueWaitSeconds",
                (SELECT count(DISTINCT day) FROM analytics_activity_days WHERE organization_id = {organizationId}) AS "ActiveDays",
                (SELECT count(*) FROM (
                    SELECT person FROM (
                        SELECT requested_by AS person, (described_at AT TIME ZONE 'UTC')::date AS day FROM analytics_tests WHERE organization_id = {organizationId}
                        UNION
                        SELECT requested_by, (queued_at AT TIME ZONE 'UTC')::date FROM analytics_runs WHERE organization_id = {organizationId}
                    ) activity
                    WHERE person IS NOT NULL
                    GROUP BY person
                    HAVING count(DISTINCT day) >= 2
                ) returners) AS "PeopleReturning",
                ((SELECT COALESCE(sum(cost_usd), 0) FROM analytics_runs WHERE organization_id = {organizationId})
                    + (SELECT COALESCE(sum(counted_usd), 0) FROM analytics_planning_attempts WHERE organization_id = {organizationId}))
                    / NULLIF((SELECT count(*) FROM analytics_runs WHERE organization_id = {organizationId}
                        AND kind = 'approved-plan' AND outcome IN ('verdict', 'inconclusive')), 0) AS "CostPerCompletedRun"
            """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
