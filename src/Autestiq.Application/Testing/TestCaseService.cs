using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Autestiq.Application.Audit;
using Autestiq.Application.Execution;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Projects;
using Autestiq.Domain.Testing;
using Autestiq.Persistence;

namespace Autestiq.Application.Testing;

/// <summary>
/// Describing a test and reading the plan for it (Milestone 5, part 2). Every gate is checked here, before anything
/// is queued, and again where it matters later: the dispatcher checks the site before exploring it, and the planning
/// step checks the permission and the budgets before calling the model.
/// </summary>
internal sealed class TestCaseService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    IOptionsMonitor<ExecutionOptions> execution,
    IOptionsMonitor<PlanningOptions> planning,
    IOptionsMonitor<Usage.UsageOptions> usage,
    TimeProvider timeProvider)
    : ITestCaseService
{
    /// <summary>The longest description, as the database holds it.</summary>
    public const int MaxDescriptionLength = 1000;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestCaseSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return [];
        }

        return await (
                from testCase in database.TestCases
                join environment in database.Environments on testCase.EnvironmentId equals environment.Id
                where testCase.ProjectId == projectId
                orderby testCase.Number descending
                select new TestCaseSummary(testCase.Id, testCase.Number, testCase.Description, environment.Name, testCase.Status, testCase.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TestCaseDetail?> GetAsync(Guid testCaseId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return null;
        }

        var found = await (
                from candidate in database.TestCases
                join environment in database.Environments on candidate.EnvironmentId equals environment.Id
                where candidate.Id == testCaseId
                select new { TestCase = candidate, environment.Name, environment.Origin })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (found is null)
        {
            return null;
        }

        var testCase = found.TestCase;

        var exploration = await database.TestRuns
            .Where(run => run.TestCaseId == testCase.Id)
            .OrderByDescending(run => run.QueuedAt)
            .Select(run => (Guid?)run.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var plan = testCase.Plan is null ? null : JsonSerializer.Deserialize<StoredPlan>(testCase.Plan, Json);

        return new TestCaseDetail(
            testCase.Id,
            testCase.Number,
            testCase.Description,
            found.Name,
            found.Origin,
            testCase.Status,
            testCase.FailureReason,
            plan?.Actions ?? [],
            plan?.Coverage ?? PlanCoverage.Full,
            testCase.Notes is null ? [] : JsonSerializer.Deserialize<List<PlanNote>>(testCase.Notes, Json) ?? [],
            testCase.Model,
            testCase.ModelCost,
            testCase.SentToModel is null ? [] : JsonSerializer.Deserialize<List<string>>(testCase.SentToModel, Json) ?? [],
            exploration);
    }

    /// <inheritdoc />
    public async Task<OperationResult<Guid>> DescribeAsync(Guid projectId, Guid environmentId, string description, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageTestCases))
        {
            return OperationResult.Failure<Guid>("Tests_Error_NotAllowed");
        }

        var text = description?.Trim() ?? "";

        if (text.Length == 0 || text.Length > MaxDescriptionLength)
        {
            return OperationResult.Failure<Guid>("Tests_Error_DescriptionRequired");
        }

        var project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId && candidate.Status == ProjectStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure<Guid>("Tests_Error_NoSuchProject");
        }

        // G-1: no page content of this project goes to a model without its permission, given and audited.
        if (project.AiProcessingAllowedAt is null)
        {
            return OperationResult.Failure<Guid>("Tests_Error_AiNotAllowed");
        }

        var environment = await database.Environments
            .FirstOrDefaultAsync(candidate => candidate.Id == environmentId && candidate.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (environment is null)
        {
            return OperationResult.Failure<Guid>("Tests_Error_NoSuchEnvironment");
        }

        if (environment.Kind == EnvironmentKind.Production && environment.ProductionAuthorizedAt is null)
        {
            return OperationResult.Failure<Guid>("Tests_Error_ProductionNotAuthorized");
        }

        var now = timeProvider.GetUtcNow();
        var options = execution.CurrentValue;

        if (options.IsPaused(project.OrganizationId))
        {
            return OperationResult.Failure<Guid>("Tests_Error_Paused");
        }

        // The site, as the dispatcher will judge it: proved, sealed, not an unauthorized production site. Asked now so
        // that a person hears it at once; the dispatcher asks again before any browser reaches the site.
        var probe = new TestRun { OrganizationId = project.OrganizationId, ScenarioId = Scenarios.Exploration, TargetOrigin = environment.Origin };

        if (!await RunDispatcher.MayTargetAsync(database, probe, options, now, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Tests_Error_SiteNotAllowed");
        }

        if ((await RunDispatcher.RoomForAsync(database, project.OrganizationId, now, cancellationToken).ConfigureAwait(false)).UsedUp)
        {
            return OperationResult.Failure<Guid>("Tests_Error_MinutesUsedUp");
        }

        if (await AiBudget.LeftAsync(database, project.OrganizationId, planning.CurrentValue, now, cancellationToken).ConfigureAwait(false) <= 0)
        {
            return OperationResult.Failure<Guid>("Tests_Error_AiBudgetSpent");
        }

        var organizationId = organizationContext.RequireOrganizationId();

        // Counted and added under the organization's lock, like any run, so parallel requests cannot pass the limit.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await Usage.StartLimits.LockAsync(database, organizationId, cancellationToken).ConfigureAwait(false);

        if (await Usage.StartLimits.RunStartsExceededAsync(database, organizationId, currentUser.UserId, usage.CurrentValue, now, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Tests_Error_TooManyStarts");
        }

        var testNumber = (await database.TestCases.Where(candidate => candidate.ProjectId == projectId)
            .MaxAsync(candidate => (int?)candidate.Number, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

        var runNumber = (await database.TestRuns.Where(run => run.ProjectId == projectId)
            .MaxAsync(run => (int?)run.Number, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

        var testCase = new TestCase
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            EnvironmentId = environment.Id,
            Number = testNumber,
            Description = text,
            RequestedBy = currentUser.UserId,
        };

        var exploration = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = runNumber,
            ScenarioId = Scenarios.Exploration,
            TargetOrigin = environment.Origin,
            Status = RunStatus.Queued,
            QueuedAt = now,
            RequestedBy = currentUser.UserId,
            TestCaseId = testCase.Id,
        };

        database.TestCases.Add(testCase);
        database.TestRuns.Add(exploration);

        new AuditWriter(database, timeProvider).Record(
            organizationId,
            currentUser.UserId,
            currentUser.Email,
            AuditActions.TestDescribed,
            nameof(TestCase),
            testCase.Id,
            $"{project.KeyPrefix} test #{testCase.Number}");

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.With(testCase.Id);
    }
}

/// <summary>A plan as it is stored with its test.</summary>
internal sealed record StoredPlan(IReadOnlyList<PlannedAction> Actions, PlanCoverage Coverage);
