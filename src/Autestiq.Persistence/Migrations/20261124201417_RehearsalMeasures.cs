using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RehearsalMeasures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Milestone 5.1 R6: the rehearsal's measures, as views over the operational tables (data-architecture.md §7).
            // They carry ids, states, times, counts and costs only: no description, page text, plan, prompt, origin,
            // failure message, name or email. A test checks their columns one by one.
            migrationBuilder.Sql("""
                CREATE VIEW analytics_runs AS
                SELECT r.organization_id,
                       r.project_id,
                       r.id AS run_id,
                       r.test_case_id,
                       CASE r.scenario_id WHEN 'explore' THEN 'exploration' WHEN 'approved-plan' THEN 'approved-plan' ELSE 'scripted' END AS kind,
                       r.status,
                       CASE
                           WHEN r.status IN ('Passed', 'Failed') THEN 'verdict'
                           WHEN r.status = 'Inconclusive' THEN 'inconclusive'
                           WHEN r.status IN ('Queued', 'Running') THEN 'open'
                           WHEN r.status = 'Cancelled' THEN 'cancelled'
                           ELSE 'error'
                       END AS outcome,
                       r.attempt,
                       r.rerun_of IS NOT NULL AS is_rerun,
                       r.requested_by,
                       r.queued_at,
                       r.started_at,
                       r.finished_at,
                       EXTRACT(EPOCH FROM (r.started_at - r.queued_at))::double precision AS queue_wait_seconds,
                       EXTRACT(EPOCH FROM (r.finished_at - r.started_at))::double precision AS duration_seconds,
                       r.cost AS cost_usd
                FROM test_runs r;

                CREATE VIEW analytics_planning_attempts AS
                SELECT a.organization_id,
                       t.project_id,
                       a.test_case_id,
                       a.id AS attempt_id,
                       a.model,
                       a.ceiling_usd,
                       a.created_at AS started_at,
                       o.created_at AS ended_at,
                       COALESCE(o."end", 'Open') AS outcome,
                       COALESCE(o.cost_usd, a.ceiling_usd) AS counted_usd,
                       o.tokens,
                       EXTRACT(EPOCH FROM (o.created_at - a.created_at))::double precision AS duration_seconds
                FROM planning_attempts a
                JOIN test_cases t ON t.organization_id = a.organization_id AND t.id = a.test_case_id
                LEFT JOIN planning_attempt_outcomes o ON o.organization_id = a.organization_id AND o.attempt_id = a.id;

                CREATE VIEW analytics_tests AS
                SELECT t.organization_id,
                       t.project_id,
                       t.id AS test_case_id,
                       t.requested_by,
                       t.created_at AS described_at,
                       t.status,
                       CASE
                           WHEN t.plan IS NULL THEN NULL
                           WHEN (t.plan::jsonb ->> 'coverage') IN ('2', 'Partial') THEN 'partial'
                           ELSE 'full'
                       END AS coverage,
                       t.planned_at,
                       approved.first_approved_at,
                       EXTRACT(EPOCH FROM (approved.first_approved_at - t.created_at))::double precision AS seconds_to_first_approval,
                       verdict.first_verdict_at,
                       EXTRACT(EPOCH FROM (verdict.first_verdict_at - t.created_at))::double precision AS seconds_to_first_verdict,
                       (SELECT count(*) FROM planning_attempts a WHERE a.organization_id = t.organization_id AND a.test_case_id = t.id) AS planning_attempts,
                       (SELECT count(*) FROM audit_entries e WHERE e.organization_id = t.organization_id AND e.subject_id = t.id AND e.action = 'test.expected_results_set') AS expected_results_edits,
                       (SELECT count(*) FROM audit_entries e WHERE e.organization_id = t.organization_id AND e.subject_id = t.id AND e.action = 'test.approval_withdrawn') AS approvals_withdrawn,
                       (SELECT count(*) FROM test_runs r WHERE r.organization_id = t.organization_id AND r.test_case_id = t.id AND r.scenario_id = 'approved-plan') AS approved_runs
                FROM test_cases t
                LEFT JOIN LATERAL (
                    SELECT min(e.at) AS first_approved_at FROM audit_entries e
                    WHERE e.organization_id = t.organization_id AND e.subject_id = t.id AND e.action = 'test.approved'
                ) approved ON true
                LEFT JOIN LATERAL (
                    SELECT min(r.finished_at) AS first_verdict_at FROM test_runs r
                    WHERE r.organization_id = t.organization_id AND r.test_case_id = t.id
                      AND r.scenario_id = 'approved-plan' AND r.status IN ('Passed', 'Failed')
                ) verdict ON true;

                CREATE VIEW analytics_activity_days AS
                SELECT organization_id, project_id, day,
                       count(DISTINCT person) AS active_people,
                       count(*) FILTER (WHERE what = 'test') AS tests_described,
                       count(*) FILTER (WHERE what = 'run') AS runs_started
                FROM (
                    SELECT organization_id, project_id, (created_at AT TIME ZONE 'UTC')::date AS day, requested_by AS person, 'test' AS what FROM test_cases
                    UNION ALL
                    SELECT organization_id, project_id, (queued_at AT TIME ZONE 'UTC')::date, requested_by, 'run' FROM test_runs
                ) activity
                GROUP BY organization_id, project_id, day;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS analytics_activity_days;
                DROP VIEW IF EXISTS analytics_tests;
                DROP VIEW IF EXISTS analytics_planning_attempts;
                DROP VIEW IF EXISTS analytics_runs;
                """);

        }
    }
}
