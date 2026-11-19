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
    IEnumerable<Contracts.Export.ITestExporter> exporters,
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
            .Where(run => run.TestCaseId == testCase.Id && run.ScenarioId == Scenarios.Exploration)
            .OrderByDescending(run => run.QueuedAt)
            .Select(run => (Guid?)run.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var plan = testCase.Plan is null ? null : JsonSerializer.Deserialize<StoredPlan>(testCase.Plan, Json);
        var snapshot = testCase.Snapshot is null ? null : JsonSerializer.Deserialize<SiteSnapshot>(testCase.Snapshot, Json);
        var actions = plan?.Actions ?? [];

        PlanApproval? approval = null;

        if (testCase.ApprovedAt is { } approvedAt)
        {
            // A person's name, from the shared users table: a user is not inside the tenant boundary.
            var approver = await database.Users.IgnoreQueryFilters()
                .Where(user => user.Id == testCase.ApprovedBy)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            approval = new PlanApproval(approvedAt, approver, testCase.ApprovedToChangeData);
        }

        var runs = await database.TestRuns
            .Where(run => run.TestCaseId == testCase.Id && run.ScenarioId == Scenarios.ApprovedPlan)
            .OrderByDescending(run => run.QueuedAt)
            .Take(20)
            .Select(run => new TestCaseRun(run.Id, run.Number, run.Status, run.QueuedAt, run.FinishedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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
            exploration,
            PlanEffects.StepsThatMayChangeData(actions, snapshot),
            PlanEffects.SecretsMissing(actions, await EnvironmentSecretNamesAsync(testCase, cancellationToken).ConfigureAwait(false)).Count > 0,
            approval,
            runs,
            [.. snapshot?.Pages.SelectMany(page => page.ReachedBy ?? []).Distinct(StringComparer.Ordinal) ?? []]);
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

    /// <inheritdoc />
    public async Task<OperationResult> ApproveAsync(Guid testCaseId, bool allowChangingData, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ApproveTestArtifacts))
        {
            return OperationResult.Failure("Tests_Error_NotApprover");
        }

        var testCase = await database.TestCases.FirstOrDefaultAsync(candidate => candidate.Id == testCaseId, cancellationToken).ConfigureAwait(false);

        if (testCase is null)
        {
            return OperationResult.Failure("Tests_Error_NoSuchTest");
        }

        if (testCase.Status != TestCaseStatus.Planned || testCase.Plan is null
            || JsonSerializer.Deserialize<StoredPlan>(testCase.Plan, Json) is not { } plan)
        {
            return OperationResult.Failure("Tests_Error_NotPlanned");
        }

        // A plan that tests part of what was asked would pass for the whole of it.
        if (plan.Coverage != PlanCoverage.Full)
        {
            return OperationResult.Failure("Tests_Error_PartialPlan");
        }

        // ADR-0033: a plan types only secrets the environment keeps; one it lacks would fail every run.
        if (PlanEffects.SecretsMissing(plan.Actions, await EnvironmentSecretNamesAsync(testCase, cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            return OperationResult.Failure("Tests_Error_NeedsSecret");
        }

        var snapshot = testCase.Snapshot is null ? null : JsonSerializer.Deserialize<SiteSnapshot>(testCase.Snapshot, Json);

        // G-4: a plan that may change data is approved only by someone who says, in so many words, that it may.
        if (PlanEffects.StepsThatMayChangeData(plan.Actions, snapshot).Count > 0 && !allowChangingData)
        {
            return OperationResult.Failure("Tests_Error_ChangeAcknowledgementRequired");
        }

        testCase.ApprovedAt = timeProvider.GetUtcNow();
        testCase.ApprovedBy = currentUser.UserId;
        testCase.ApprovedPlanHash = PlanFingerprint.Of(testCase.Plan);
        testCase.ApprovedToChangeData = allowChangingData;

        Record(testCase, AuditActions.TestApproved, new { ToChangeData = allowChangingData, Plan = testCase.ApprovedPlanHash });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> WithdrawApprovalAsync(Guid testCaseId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ApproveTestArtifacts))
        {
            return OperationResult.Failure("Tests_Error_NotApprover");
        }

        var testCase = await database.TestCases.FirstOrDefaultAsync(candidate => candidate.Id == testCaseId, cancellationToken).ConfigureAwait(false);

        if (testCase is null)
        {
            return OperationResult.Failure("Tests_Error_NoSuchTest");
        }

        testCase.ApprovedAt = null;
        testCase.ApprovedBy = null;
        testCase.ApprovedPlanHash = null;
        testCase.ApprovedToChangeData = false;

        Record(testCase, AuditActions.TestApprovalWithdrawn, after: null);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult<Guid>> RunAsync(Guid testCaseId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.RunTests))
        {
            return OperationResult.Failure<Guid>("Tests_Error_CannotRun");
        }

        var testCase = await database.TestCases.FirstOrDefaultAsync(candidate => candidate.Id == testCaseId, cancellationToken).ConfigureAwait(false);

        if (testCase is null)
        {
            return OperationResult.Failure<Guid>("Tests_Error_NoSuchTest");
        }

        if (!IsApproved(testCase))
        {
            return OperationResult.Failure<Guid>("Tests_Error_NotApproved");
        }

        var environment = await database.Environments
            .FirstOrDefaultAsync(candidate => candidate.Id == testCase.EnvironmentId, cancellationToken)
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

        if (options.IsPaused(testCase.OrganizationId))
        {
            return OperationResult.Failure<Guid>("Tests_Error_Paused");
        }

        var probe = new TestRun { OrganizationId = testCase.OrganizationId, ScenarioId = Scenarios.ApprovedPlan, TargetOrigin = environment.Origin };

        if (!await RunDispatcher.MayTargetAsync(database, probe, options, now, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Tests_Error_SiteNotAllowed");
        }

        if ((await RunDispatcher.RoomForAsync(database, testCase.OrganizationId, now, cancellationToken).ConfigureAwait(false)).UsedUp)
        {
            return OperationResult.Failure<Guid>("Tests_Error_MinutesUsedUp");
        }

        var organizationId = organizationContext.RequireOrganizationId();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await Usage.StartLimits.LockAsync(database, organizationId, cancellationToken).ConfigureAwait(false);

        if (await Usage.StartLimits.RunStartsExceededAsync(database, organizationId, currentUser.UserId, usage.CurrentValue, now, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<Guid>("Tests_Error_TooManyStarts");
        }

        var runNumber = (await database.TestRuns.Where(run => run.ProjectId == testCase.ProjectId)
            .MaxAsync(run => (int?)run.Number, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

        var run = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = testCase.ProjectId,
            Number = runNumber,
            ScenarioId = Scenarios.ApprovedPlan,
            TargetOrigin = environment.Origin,
            Status = RunStatus.Queued,
            QueuedAt = now,
            RequestedBy = currentUser.UserId,
            TestCaseId = testCase.Id,
        };

        database.TestRuns.Add(run);

        new AuditWriter(database, timeProvider).Record(
            organizationId,
            currentUser.UserId,
            currentUser.Email,
            AuditActions.RunStarted,
            nameof(TestRun),
            run.Id,
            $"test #{testCase.Number}, run #{run.Number}");

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.With(run.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResult<Contracts.Export.ExportedTest>> DownloadAsync(Guid testCaseId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return OperationResult.Failure<Contracts.Export.ExportedTest>("Tests_Error_NoSuchTest");
        }

        var testCase = await database.TestCases.FirstOrDefaultAsync(candidate => candidate.Id == testCaseId, cancellationToken).ConfigureAwait(false);

        if (testCase is null)
        {
            return OperationResult.Failure<Contracts.Export.ExportedTest>("Tests_Error_NoSuchTest");
        }

        // Only an approved plan leaves Autestiq, and only the plan that was approved.
        if (!IsApproved(testCase) || testCase.Plan is not { } stored || JsonSerializer.Deserialize<StoredPlan>(stored, Json) is not { } plan)
        {
            return OperationResult.Failure<Contracts.Export.ExportedTest>("Tests_Error_NotApproved");
        }

        // The project's language is a setting once there is more than one; Python is the one that is built (ADR-0023).
        if (exporters.FirstOrDefault(candidate => candidate.Language == Contracts.Export.ExportLanguage.Python) is not { } exporter)
        {
            return OperationResult.Failure<Contracts.Export.ExportedTest>("Tests_Error_NoExporter");
        }

        var exported = exporter.Export(new TestPlan(testCase.Description, plan.Actions, [], plan.Coverage));

        Record(testCase, AuditActions.TestDownloaded, new { Plan = testCase.ApprovedPlanHash, exported.FileName });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.With(exported with { Code = ApprovedTestHeader.For(testCase) + exported.Code });
    }

    private Task<IReadOnlyList<string>> EnvironmentSecretNamesAsync(TestCase testCase, CancellationToken cancellationToken) =>
        Projects.EnvironmentSecrets.NamesAsync(database, testCase.OrganizationId, testCase.EnvironmentId, cancellationToken);

    /// <summary>Whether a test's plan is approved, and is still the plan that was approved.</summary>
    internal static bool IsApproved(TestCase testCase) =>
        testCase.ApprovedAt is not null
        && testCase.Plan is not null
        && testCase.ApprovedPlanHash == PlanFingerprint.Of(testCase.Plan);

    private void Record(TestCase testCase, string action, object? after) =>
        new AuditWriter(database, timeProvider).Record(
            testCase.OrganizationId,
            currentUser.UserId,
            currentUser.Email,
            action,
            nameof(TestCase),
            testCase.Id,
            $"test #{testCase.Number}",
            after: after);
}

/// <summary>
/// The fixed lines a downloaded test begins with: what was approved, and what the file does not do. Fixed text, a date
/// and a fingerprint only, never a name or anything a person or a page wrote, so nothing can break out of a comment
/// (ADR-0023).
/// </summary>
internal static class ApprovedTestHeader
{
    private const string ChangesData = "# The approval allows this test to change data on the site it runs against.\n";

    private const string ReadOnly =
        "# The approval was for a test that does not change data. Autestiq held that in its own browser;\n"
        + "# this file does not. Run it only where a test that changes data would be acceptable.\n";

    public static string For(TestCase testCase)
    {
        var approvedOn = testCase.ApprovedAt?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "";

        return $"# Approved in Autestiq on {approvedOn}. Plan fingerprint (SHA-256): {testCase.ApprovedPlanHash}.\n"
            + (testCase.ApprovedToChangeData ? ChangesData : ReadOnly)
            + "\n";
    }
}

/// <summary>The fingerprint of a stored plan: its SHA-256, in lower-case hex.</summary>
internal static class PlanFingerprint
{
    public static string Of(string plan) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plan)));
}

/// <summary>A plan as it is stored with its test.</summary>
internal sealed record StoredPlan(IReadOnlyList<PlannedAction> Actions, PlanCoverage Coverage);
