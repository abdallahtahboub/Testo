using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Autestiq.Application.Analytics;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Audit;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Testing;

namespace Autestiq.Application.Tests.Analytics;

/// <summary>
/// The rehearsal's measures (Milestone 5.1 R6): views with no content in them, checked column by column, and the
/// report's figures on a test whose every time and outcome is known.
/// </summary>
public sealed class RehearsalMeasuresTests : IAsyncLifetime
{
    /// <summary>Every column each view may have: ids, states, times, counts and costs. A new column fails here first.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        ["analytics_runs"] =
        [
            "organization_id", "project_id", "run_id", "test_case_id", "kind", "status", "outcome", "attempt", "is_rerun",
            "requested_by", "queued_at", "started_at", "finished_at", "queue_wait_seconds", "duration_seconds", "cost_usd",
        ],
        ["analytics_planning_attempts"] =
        [
            "organization_id", "project_id", "test_case_id", "attempt_id", "model", "ceiling_usd", "started_at", "ended_at",
            "outcome", "counted_usd", "tokens", "duration_seconds",
        ],
        ["analytics_tests"] =
        [
            "organization_id", "project_id", "test_case_id", "requested_by", "described_at", "status", "coverage",
            "planned_at", "first_approved_at", "seconds_to_first_approval", "first_verdict_at", "seconds_to_first_verdict",
            "planning_attempts", "expected_results_edits", "approvals_withdrawn", "approved_runs",
        ],
        ["analytics_activity_days"] = ["organization_id", "project_id", "day", "active_people", "tests_described", "runs_started"],
    };

    /// <summary>What may never reach analytics, whatever a future view is called: content, credentials, identities.</summary>
    private static readonly string[] Forbidden =
    [
        "description", "expected_results", "snapshot", "sent_to_model", "plan", "notes", "failure_reason", "message",
        "target_origin", "origin", "ciphertext", "email", "display_name", "name", "before", "after", "subject_name",
        "ip_address", "actor_email", "storage_key",
    ];

    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
    private TestDatabase _database = null!;
    private Guid _northwind;
    private Guid _person;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_northwind, _person) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryAnalyticsView_HasOnlyItsAllowedColumns()
    {
        var columns = await ColumnsAsync();

        Assert.Equal(Allowed.Keys.Order(StringComparer.Ordinal), columns.Keys.Order(StringComparer.Ordinal));

        foreach (var (view, allowed) in Allowed)
        {
            Assert.Equal(allowed, columns[view]);
        }
    }

    [Fact]
    public async Task NoAnalyticsView_CarriesContent_CredentialsOrIdentities()
    {
        foreach (var (view, names) in await ColumnsAsync())
        {
            Assert.DoesNotContain(names, name => Forbidden.Contains(name, StringComparer.Ordinal));
            Assert.All(names, name => Assert.False(name.EndsWith("_name", StringComparison.Ordinal), $"{view}.{name}"));
        }
    }

    [Fact]
    public async Task Report_OfOneTestDescribedApprovedAndRun_GivesItsTimes_Corrections_Outcomes_Reuse_AndCost()
    {
        await SeedAsync();

        await using var database = _database.NewContext();
        var measures = await RehearsalReport.ForAsync(database, _northwind, Cancellation);

        Assert.Equal(1, measures.TestsDescribed);

        // Described at the start; approved ten minutes later; its first verdict thirty minutes after the start.
        Assert.InRange(measures.MedianSecondsToFirstApproval!.Value, 590, 610);
        Assert.InRange(measures.MedianSecondsToFirstVerdict!.Value, 1790, 1810);

        // Two changes of expected results, and one planning attempt beyond the first.
        Assert.Equal(3.0, measures.CorrectionsPerTest);
        Assert.Equal((0L, 1L), (measures.FullPlans, measures.PartialPlans));
        Assert.Equal((1L, 1L, 1L), (measures.RunsWithVerdict, measures.RunsInconclusive, measures.RunsInError));
        Assert.Equal(60.0, measures.MedianQueueWaitSeconds);

        // Active today and tomorrow, so the one person came back.
        Assert.Equal((2L, 1L), (measures.ActiveDays, measures.PeopleReturning));

        // Five runs at $0.01, an answered attempt at $0.05 and an open one counted at its $0.25 ceiling, over the two
        // approved runs that finished with a verdict or inconclusive.
        Assert.Equal(0.175m, measures.CostPerCompletedRun);
    }

    [Fact]
    public async Task Report_OfAnotherOrganization_SeesNoneOfThis()
    {
        await SeedAsync();

        await using var services = _database.NewServices();
        var (contoso, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Contoso");
        await using var database = _database.NewContext();

        var measures = await RehearsalReport.ForAsync(database, contoso, Cancellation);

        Assert.Equal((0L, 0L, 0L, (decimal?)null), (measures.TestsDescribed, measures.RunsWithVerdict, measures.ActiveDays, measures.CostPerCompletedRun));
    }

    private async Task<Dictionary<string, string[]>> ColumnsAsync()
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name, column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name LIKE 'analytics\_%'
            ORDER BY table_name, ordinal_position
            """,
            connection);

        var columns = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(Cancellation);

        while (await reader.ReadAsync(Cancellation))
        {
            var view = reader.GetString(0);
            columns.TryAdd(view, []);
            columns[view].Add(reader.GetString(1));
        }

        return columns.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>
    /// One test, described now with a partial plan; its expected results changed twice; planned twice (one attempt
    /// answered, one never ended); approved ten minutes in; an exploration, a passing run at thirty minutes, an
    /// inconclusive one, a timed-out one, and a scripted run tomorrow, each having waited a minute.
    /// </summary>
    private async Task SeedAsync()
    {
        _database.Principal.Become(_northwind, _person, Role.Owner);
        Guid project, environment;

        await using (var services = _database.NewServices())
        {
            using var scope = services.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();
            await projects.CreateAsync(new NewProject("Shop", "SHOP", null), Cancellation);
            project = (await projects.FindAsync("SHOP", Cancellation))!.ProjectId;
            environment = (await projects.AddEnvironmentAsync(project, new NewEnvironment("Staging", "http://127.0.0.1:5090", EnvironmentKind.Test), Cancellation)).Value;
        }

        await using var database = _database.NewContext();
        var testCase = new TestCase
        {
            OrganizationId = _northwind,
            ProjectId = project,
            EnvironmentId = environment,
            Number = 1,
            Description = "Check the basket adds up",
            Status = TestCaseStatus.Planned,
            Plan = """{"actions":[],"coverage":2}""",
            RequestedBy = _person,
            PlannedAt = _start.AddMinutes(5),
        };
        database.TestCases.Add(testCase);

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(Cancellation);
        }

        var answered = new PlanningAttempt { OrganizationId = _northwind, TestCaseId = testCase.Id, Model = "claude-sonnet-5-5", CeilingUsd = 0.25m };
        var open = new PlanningAttempt { OrganizationId = _northwind, TestCaseId = testCase.Id, Model = "claude-sonnet-5-5", CeilingUsd = 0.25m };
        database.PlanningAttempts.AddRange(answered, open);
        database.PlanningAttemptOutcomes.Add(new PlanningAttemptOutcome { OrganizationId = _northwind, AttemptId = answered.Id, End = PlanningAttemptEnd.Answered, CostUsd = 0.05m, Tokens = 13_000 });

        database.AuditEntries.AddRange(
            Audit("test.expected_results_set", testCase.Id, _start.AddMinutes(2)),
            Audit("test.expected_results_set", testCase.Id, _start.AddMinutes(3)),
            Audit("test.approved", testCase.Id, _start.AddMinutes(10)));

        database.TestRuns.AddRange(
            Run(1, Scenarios.Exploration, RunStatus.Passed, testCase.Id, _start),
            Run(2, Scenarios.ApprovedPlan, RunStatus.Passed, testCase.Id, _start.AddMinutes(28)),
            Run(3, Scenarios.ApprovedPlan, RunStatus.Inconclusive, testCase.Id, _start.AddMinutes(40)),
            Run(4, Scenarios.ApprovedPlan, RunStatus.TimedOut, testCase.Id, _start.AddMinutes(50)),
            Run(5, Scenarios.DemoShopBasket, RunStatus.Passed, null, _start.AddDays(1)));

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(Cancellation);
        }

        TestRun Run(int number, string scenario, RunStatus status, Guid? test, DateTimeOffset queued) => new()
        {
            OrganizationId = _northwind,
            ProjectId = project,
            Number = number,
            ScenarioId = scenario,
            TargetOrigin = "http://127.0.0.1:5090",
            Status = status,
            TestCaseId = test,
            RequestedBy = _person,
            QueuedAt = queued,
            StartedAt = queued.AddMinutes(1),
            FinishedAt = queued.AddMinutes(2),
            Cost = 0.01m,
        };
    }

    private AuditEntry Audit(string action, Guid subject, DateTimeOffset at) => new()
    {
        OrganizationId = _northwind,
        ActorUserId = _person,
        Action = action,
        SubjectType = nameof(TestCase),
        SubjectId = subject,
        At = at,
    };
}
