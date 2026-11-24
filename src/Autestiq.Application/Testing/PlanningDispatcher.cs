using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Autestiq.Application.Audit;
using Autestiq.Application.Planning;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Testing;
using Autestiq.Persistence;

namespace Autestiq.Application.Testing;

/// <summary>
/// The planning step (Milestone 5, part 2; ADR-0030). Takes a test whose exploration has ended and, if the project
/// still allows AI processing and the month's budgets still have room, asks the model for its plan, validates it, and
/// stores the plan, its notes by source, exactly what was sent, and what it cost. Nothing here runs a browser: the
/// plan runs only once a person approves it (part 3).
/// </summary>
/// <remarks>
/// Milestone 5.1 R1 (R-17): one planning step per deployment, by a PostgreSQL advisory lock, so the budgets, which are
/// read before a call and spent after it, cannot be spent twice by two instances. Each call is recorded as an attempt
/// <em>before</em> the model is asked, and counts at its ceiling until its outcome is written. A test left in Planning
/// past <see cref="PlanningOptions.Lease"/> is failed, saying so, and never planned again on its own: the provider may
/// have billed it, and a person decides whether to spend again.
/// </remarks>
public sealed partial class PlanningDispatcher(
    IServiceScopeFactory scopes,
    IOptionsMonitor<PlanningOptions> options,
    TimeProvider timeProvider,
    ILogger<PlanningDispatcher> logger)
    : BackgroundService
{
    /// <summary>The advisory lock's key for the planning step, unique within the product.</summary>
    public const int LockKey = 5_101;

    private AdvisoryLock? _lock;
    private DateTimeOffset _nextRecovery = DateTimeOffset.MinValue;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await PlanNextAsync(stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(options.CurrentValue.PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // A planning step that stops is worse than one that logs and carries on.
                LoopFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Moves one test along, if there is one to move and this is the deployment's planning step. True when something
    /// was done.
    /// </summary>
    public async Task<bool> PlanNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        if (!await HoldsLockAsync(database, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (timeProvider.GetUtcNow() >= _nextRecovery)
        {
            await RecoverAsync(database, writeScope, cancellationToken).ConfigureAwait(false);
            _nextRecovery = timeProvider.GetUtcNow() + options.CurrentValue.RecoveryInterval;
        }

        // Across organizations, as the dispatcher claims runs; each test is written back under its own organization.
        var testCase = await database.TestCases.IgnoreQueryFilters()
            .Where(candidate => candidate.Status == TestCaseStatus.Exploring
                && database.TestRuns.IgnoreQueryFilters().Any(run => run.TestCaseId == candidate.Id
                    && run.OrganizationId == candidate.OrganizationId
                    && run.Status != RunStatus.Queued
                    && run.Status != RunStatus.Running))
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (testCase is null)
        {
            return false;
        }

        using (writeScope.AllowWritingTo(testCase.OrganizationId))
        {
            if (testCase.Snapshot is null)
            {
                // Refused, timed out or broken before it described anything: nothing to plan from.
                await FailAsync(database, testCase, "Tests_Error_ExplorationFailed", cancellationToken).ConfigureAwait(false);
                return true;
            }

            testCase.Status = TestCaseStatus.Planning;
            testCase.PlanningStartedAt = timeProvider.GetUtcNow();

            try
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another planning step took it first.
                return true;
            }

            await PlanAsync(scope.ServiceProvider, database, testCase, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task PlanAsync(IServiceProvider services, AutestiqDbContext database, TestCase testCase, CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var now = timeProvider.GetUtcNow();

        // Checked again here, not only when the test was described: either may have changed while it explored.
        var allowed = await database.Projects.IgnoreQueryFilters()
            .AnyAsync(project => project.Id == testCase.ProjectId && project.OrganizationId == testCase.OrganizationId && project.AiProcessingAllowedAt != null, cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            await FailAsync(database, testCase, "Tests_Error_AiNotAllowed", cancellationToken).ConfigureAwait(false);
            return;
        }

        var left = await AiBudget.LeftAsync(database, testCase.OrganizationId, current, now, cancellationToken).ConfigureAwait(false);

        if (left <= 0)
        {
            await FailAsync(database, testCase, "Tests_Error_AiBudgetSpent", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await AiBudget.PricesAsync(database, current.Model, now, cancellationToken).ConfigureAwait(false) is not { } prices
            || services.GetRequiredService<IPlannerFactory>().Create(current.Model, prices.Input, prices.Output) is not { } planner)
        {
            await FailAsync(database, testCase, "Tests_Error_NoPlanner", cancellationToken).ConfigureAwait(false);
            return;
        }

        var site = JsonSerializer.Deserialize<SiteSnapshot>(testCase.Snapshot!, TestCaseService.Json) ?? SiteSnapshot.Empty(""); // not null: checked by the caller.

        // A single plan may spend no more than its own limit, nor more than the month has left.
        var budget = RunBudget.Default with { MaxCostUsd = Math.Min(RunBudget.Default.MaxCostUsd, left) };

        // Recorded before the model is asked: from here on it may be billed, whatever happens to this process.
        var attempt = new PlanningAttempt
        {
            OrganizationId = testCase.OrganizationId,
            TestCaseId = testCase.Id,
            Model = current.Model,
            CeilingUsd = budget.MaxCostUsd,
        };

        database.PlanningAttempts.Add(attempt);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var planned = await new PlanningService(planner, services.GetRequiredService<ILogger<PlanningService>>())
            // The names of the environment's sign-in values, never the values: a plan may type them by name (ADR-0033).
            .PlanAsync(
                testCase.Description,
                site,
                budget,
                await Projects.EnvironmentSecrets.NamesAsync(database, testCase.OrganizationId, testCase.EnvironmentId, cancellationToken).ConfigureAwait(false),

                // What the tester says should be shown, labelled as theirs (ADR-0034).
                TestCaseService.ExpectedOf(testCase),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var checks = planned.CheckNotes ?? [];
        var notes = planned.Notes
            .Select(note => new PlanNote(note, checks.Contains(note) ? PlanNoteSource.CheckedByAutestiq : PlanNoteSource.Ai))
            .ToList();

        database.PlanningAttemptOutcomes.Add(new PlanningAttemptOutcome
        {
            OrganizationId = testCase.OrganizationId,
            AttemptId = attempt.Id,
            End = PlanningAttemptEnd.Answered,
            CostUsd = planned.Cost.Cost,
            Tokens = planned.Cost.TotalTokens,
        });

        testCase.Model = planned.Model;
        testCase.ModelTokens = planned.Cost.TotalTokens;
        testCase.ModelCost = planned.Cost.Cost;
        testCase.SentToModel = JsonSerializer.Serialize(planned.Sent ?? [], TestCaseService.Json);
        testCase.Notes = JsonSerializer.Serialize(notes, TestCaseService.Json);
        testCase.PlannedAt = timeProvider.GetUtcNow();

        if (planned.Plan is { } plan)
        {
            testCase.Plan = JsonSerializer.Serialize(new StoredPlan(plan.Actions, plan.Coverage), TestCaseService.Json);
            testCase.Status = TestCaseStatus.Planned;
            testCase.FailureReason = null;
        }
        else
        {
            testCase.Status = TestCaseStatus.Failed;
            testCase.FailureReason = planned.Errors.Count > 0 ? planned.Errors[0] : "Tests_Error_NoPlan";
        }

        new AuditWriter(database, timeProvider).Record(
            testCase.OrganizationId,
            actorUserId: null,
            actorEmail: null,
            AuditActions.TestPlanned,
            nameof(TestCase),
            testCase.Id,
            $"test #{testCase.Number}",
            after: new { Status = testCase.Status.ToString(), testCase.Model, testCase.ModelCost });

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || IsDuplicate(exception))
        {
            // The lease ran out while the model answered, and recovery already failed the test and counted the attempt
            // at its ceiling. Nothing of this answer is kept: the person sees the interruption and decides.
            AnswerTooLate(logger, testCase.Id);
            return;
        }

        Planned(logger, testCase.Id, testCase.Status, testCase.ModelCost ?? 0m);
    }

    /// <summary>
    /// Fails every test left in Planning past its lease, and ends its unfinished attempts as interrupted, at their
    /// ceiling. Never plans one again: whether the provider billed it is unknown.
    /// </summary>
    public async Task<int> RecoverAsync(AutestiqDbContext database, TenantWriteScope writeScope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(writeScope);

        var now = timeProvider.GetUtcNow();
        var expired = now - options.CurrentValue.Lease;

        // Across organizations, as the planning step works; each is written back under its own organization.
        var stuck = await database.TestCases.IgnoreQueryFilters()
            .Where(candidate => candidate.Status == TestCaseStatus.Planning && candidate.PlanningStartedAt < expired)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var testCase in stuck)
        {
            var open = await database.PlanningAttempts.IgnoreQueryFilters()
                .Where(attempt => attempt.OrganizationId == testCase.OrganizationId && attempt.TestCaseId == testCase.Id
                    && !database.PlanningAttemptOutcomes.IgnoreQueryFilters().Any(outcome => outcome.AttemptId == attempt.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            using (writeScope.AllowWritingTo(testCase.OrganizationId))
            {
                foreach (var attempt in open)
                {
                    database.PlanningAttemptOutcomes.Add(new PlanningAttemptOutcome
                    {
                        OrganizationId = attempt.OrganizationId,
                        AttemptId = attempt.Id,
                        End = PlanningAttemptEnd.Interrupted,
                        CostUsd = attempt.CeilingUsd,
                    });
                }

                testCase.Status = TestCaseStatus.Failed;
                testCase.FailureReason = "Tests_Error_PlanningInterrupted";
                testCase.PlannedAt ??= now;

                new AuditWriter(database, timeProvider).Record(
                    testCase.OrganizationId,
                    actorUserId: null,
                    actorEmail: null,
                    AuditActions.TestPlanningInterrupted,
                    nameof(TestCase),
                    testCase.Id,
                    $"test #{testCase.Number}",
                    after: new { Attempts = open.Count, CountedUsd = open.Sum(attempt => attempt.CeilingUsd) });

                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    Interrupted(logger, testCase.Id, open.Count);
                }
                catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || IsDuplicate(exception))
                {
                    // The answer arrived just now and was saved first: the test is not stuck after all.
                    database.ChangeTracker.Clear();
                }
            }
        }

        return stuck.Count;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _lock?.Dispose();
        _lock = null;
        base.Dispose();
    }

    /// <summary>Takes the deployment's planning lock, or keeps the one held, or finds another instance has it.</summary>
    private async Task<bool> HoldsLockAsync(AutestiqDbContext database, CancellationToken cancellationToken)
    {
        if (_lock is not null)
        {
            if (await _lock.StillHeldAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await _lock.DisposeAsync().ConfigureAwait(false);
            _lock = null;
            LockLost(logger);
        }

        var connectionString = database.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The database has no connection string.");

        _lock = await AdvisoryLock.TryTakeAsync(connectionString, LockKey, cancellationToken).ConfigureAwait(false);
        return _lock is not null;
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };

    private async Task FailAsync(AutestiqDbContext database, TestCase testCase, string reason, CancellationToken cancellationToken)
    {
        testCase.Status = TestCaseStatus.Failed;
        testCase.FailureReason = reason;
        testCase.PlannedAt ??= timeProvider.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Planned(logger, testCase.Id, testCase.Status, 0m);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The planning step's loop failed.")]
    private static partial void LoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Test {TestCaseId} was still planning after its lease; failed, {Attempts} attempt(s) counted at their ceiling.")]
    private static partial void Interrupted(ILogger logger, Guid testCaseId, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The model answered for test {TestCaseId} after its lease ran out; the answer was not kept.")]
    private static partial void AnswerTooLate(ILogger logger, Guid testCaseId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The planning lock's connection was lost; taking it again.")]
    private static partial void LockLost(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Test {TestCaseId} planned: {Status}, ${Cost}.")]
    private static partial void Planned(ILogger logger, Guid testCaseId, TestCaseStatus status, decimal cost);
}
