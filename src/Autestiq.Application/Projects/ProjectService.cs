using Microsoft.EntityFrameworkCore;
using Autestiq.Application.Audit;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Domain.Projects;
using Autestiq.Persistence;

namespace Autestiq.Application.Projects;

/// <summary>Projects and their environments (Milestone 5). Every query goes through the tenant filter.</summary>
internal sealed class ProjectService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    IOriginProofService proofs,
    TimeProvider timeProvider)
    : IProjectService
{
    /// <summary>The longest project name, as the database holds it.</summary>
    private const int MaxNameLength = 200;

    /// <summary>The longest environment name, as the database holds it.</summary>
    private const int MaxEnvironmentNameLength = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return [];
        }

        return await Summaries(database.Projects.Where(project => project.Status == ProjectStatus.Active).OrderBy(project => project.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectSummary?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read) || ProjectKeys.Normalize(key) is not { } normalized)
        {
            return null;
        }

        return await Summaries(database.Projects.Where(project => project.KeyPrefix == normalized && project.Status == ProjectStatus.Active))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> CreateAsync(NewProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure<string>("Projects_Error_NotAllowed");
        }

        var name = project.Name?.Trim() ?? "";

        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return OperationResult.Failure<string>("Projects_Error_NameRequired");
        }

        if (ProjectKeys.Normalize(project.Key) is not { } key)
        {
            return OperationResult.Failure<string>("Projects_Error_InvalidKey");
        }

        var organizationId = organizationContext.RequireOrganizationId();

        if (!await Usage.Entitlements.MayAddProjectAsync(database, organizationId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<string>("Projects_Error_PlanLimit");
        }

        // Archived projects keep their keys: a key once used names that project's history.
        if (await database.Projects.AnyAsync(existing => existing.KeyPrefix == key, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure<string>("Projects_Error_KeyTaken");
        }

        var created = new Project
        {
            OrganizationId = organizationId,
            Name = name,
            KeyPrefix = key,
            Description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description.Trim(),
        };

        database.Projects.Add(created);
        Record(AuditActions.ProjectCreated, nameof(Project), created.Id, $"{created.Name} ({created.KeyPrefix})");

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Two people creating the same key at once: the unique index answers for the second.
            return OperationResult.Failure<string>("Projects_Error_KeyTaken");
        }

        return OperationResult.With(created.KeyPrefix);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AllowAiProcessingAsync(Guid projectId, bool acknowledged, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure("Projects_Error_NotAllowed");
        }

        if (!acknowledged)
        {
            return OperationResult.Failure("Projects_Error_AiAcknowledgementRequired");
        }

        var project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken).ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure("Projects_Error_NoSuchProject");
        }

        project.AiProcessingAllowedAt = timeProvider.GetUtcNow();
        project.AiProcessingAllowedBy = currentUser.UserId;
        Record(AuditActions.AiProcessingAllowed, nameof(Project), project.Id, $"{project.Name} ({project.KeyPrefix})");
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> WithdrawAiProcessingAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure("Projects_Error_NotAllowed");
        }

        var project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken).ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure("Projects_Error_NoSuchProject");
        }

        project.AiProcessingAllowedAt = null;
        project.AiProcessingAllowedBy = null;
        Record(AuditActions.AiProcessingWithdrawn, nameof(Project), project.Id, $"{project.Name} ({project.KeyPrefix})");
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnvironmentSummary>> ListEnvironmentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return [];
        }

        var environments = await database.Environments
            .Where(environment => environment.ProjectId == projectId)
            .OrderBy(environment => environment.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summaries = new List<EnvironmentSummary>(environments.Count);

        foreach (var environment in environments)
        {
            var proof = await proofs.GetAsync(environment.Origin, cancellationToken).ConfigureAwait(false);
            summaries.Add(new EnvironmentSummary(environment.Id, environment.Name, environment.Origin, proof, environment.Kind, environment.ProductionAuthorizedAt));
        }

        return summaries;
    }

    /// <inheritdoc />
    public async Task<OperationResult<Guid>> AddEnvironmentAsync(Guid projectId, NewEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure<Guid>("Environments_Error_NotAllowed");
        }

        var name = environment.Name?.Trim() ?? "";

        if (name.Length == 0 || name.Length > MaxEnvironmentNameLength)
        {
            return OperationResult.Failure<Guid>("Environments_Error_NameRequired");
        }

        if (Origins.Normalize(environment.Origin) is not { } origin)
        {
            return OperationResult.Failure<Guid>("Environments_Error_InvalidOrigin");
        }

        if (!Enum.IsDefined(environment.Kind))
        {
            return OperationResult.Failure<Guid>("Environments_Error_InvalidKind");
        }

        var project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId && candidate.Status == ProjectStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure<Guid>("Environments_Error_NoSuchProject");
        }

        var known = await database.Environments
            .Where(existing => existing.ProjectId == projectId)
            .Select(existing => new { existing.Name, existing.Origin })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (known.Exists(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult.Failure<Guid>("Environments_Error_NameTaken");
        }

        if (known.Exists(existing => existing.Origin == origin))
        {
            return OperationResult.Failure<Guid>("Environments_Error_OriginTaken");
        }

        var added = new TestEnvironment
        {
            OrganizationId = project.OrganizationId,
            ProjectId = project.Id,
            Name = name,
            Origin = origin,
            Kind = environment.Kind,
        };

        database.Environments.Add(added);
        Record(AuditActions.EnvironmentAdded, nameof(TestEnvironment), added.Id, $"{project.KeyPrefix}: {added.Name} ({added.Origin})");

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure<Guid>("Environments_Error_NameTaken");
        }

        return OperationResult.With(added.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AuthorizeProductionAsync(Guid environmentId, bool acknowledged, CancellationToken cancellationToken = default)
    {
        // An owner's decision alone: it lets tests change a live site's real data.
        if (currentUser.Role != Role.Owner)
        {
            return OperationResult.Failure("Environments_Error_OwnerOnly");
        }

        if (!acknowledged)
        {
            return OperationResult.Failure("Environments_Error_AcknowledgementRequired");
        }

        var environment = await database.Environments.FirstOrDefaultAsync(candidate => candidate.Id == environmentId, cancellationToken).ConfigureAwait(false);

        if (environment is null)
        {
            return OperationResult.Failure("Environments_Error_NoSuchEnvironment");
        }

        if (environment.Kind != EnvironmentKind.Production)
        {
            return OperationResult.Failure("Environments_Error_NotProduction");
        }

        environment.ProductionAuthorizedAt = timeProvider.GetUtcNow();
        environment.ProductionAuthorizedBy = currentUser.UserId;
        Record(AuditActions.ProductionAuthorized, nameof(TestEnvironment), environment.Id, $"{environment.Name} ({environment.Origin})");
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> WithdrawProductionAsync(Guid environmentId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != Role.Owner)
        {
            return OperationResult.Failure("Environments_Error_OwnerOnly");
        }

        var environment = await database.Environments.FirstOrDefaultAsync(candidate => candidate.Id == environmentId, cancellationToken).ConfigureAwait(false);

        if (environment is null)
        {
            return OperationResult.Failure("Environments_Error_NoSuchEnvironment");
        }

        environment.ProductionAuthorizedAt = null;
        environment.ProductionAuthorizedBy = null;
        Record(AuditActions.ProductionWithdrawn, nameof(TestEnvironment), environment.Id, $"{environment.Name} ({environment.Origin})");
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemoveEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure("Environments_Error_NotAllowed");
        }

        var environment = await database.Environments
            .FirstOrDefaultAsync(candidate => candidate.Id == environmentId, cancellationToken)
            .ConfigureAwait(false);

        if (environment is null)
        {
            return OperationResult.Failure("Environments_Error_NoSuchEnvironment");
        }

        // Tests keep the environment they were explored against: one that has any is not removed from under them.
        if (await database.TestCases.AnyAsync(testCase => testCase.EnvironmentId == environmentId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure("Environments_Error_InUse");
        }

        database.Environments.Remove(environment);
        Record(AuditActions.EnvironmentRemoved, nameof(TestEnvironment), environment.Id, $"{environment.Name} ({environment.Origin})");
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> SetEvidenceRetentionAsync(Guid projectId, int days, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure("Projects_Error_NotAllowed");
        }

        if (!EvidenceRetention.AllowedDays.Contains(days))
        {
            return OperationResult.Failure("Projects_Error_InvalidRetention");
        }

        var project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken).ConfigureAwait(false);

        if (project is null)
        {
            return OperationResult.Failure("Projects_Error_NoSuchProject");
        }

        var before = project.EvidenceRetentionDays;
        project.EvidenceRetentionDays = days;

        new AuditWriter(database, timeProvider).Record(
            organizationContext.RequireOrganizationId(),
            currentUser.UserId,
            currentUser.Email,
            AuditActions.EvidenceRetentionChanged,
            nameof(Project),
            project.Id,
            $"{project.Name} ({project.KeyPrefix})",
            before: new { Days = before },
            after: new { Days = days });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OperationResult.Success;
    }

    private IQueryable<ProjectSummary> Summaries(IQueryable<Project> projects) =>
        projects.Select(project => new ProjectSummary(
            project.Id,
            project.Name,
            project.KeyPrefix,
            project.Description,
            database.Environments.Count(environment => environment.ProjectId == project.Id),
            project.AiProcessingAllowedAt,
            project.EvidenceRetentionDays));

    private void Record(string action, string subjectType, Guid subjectId, string subjectName) =>
        new AuditWriter(database, timeProvider).Record(
            organizationContext.RequireOrganizationId(),
            currentUser.UserId,
            currentUser.Email,
            action,
            subjectType,
            subjectId,
            subjectName);
}
