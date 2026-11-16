using Microsoft.EntityFrameworkCore;
using Autestiq.Application.Audit;
using Autestiq.Domain.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application.Usage;

/// <summary>What moving an organization to a plan version came to.</summary>
public enum PlanChange
{
    /// <summary>Moved, and written to the audit log.</summary>
    Moved = 1,

    /// <summary>Already on that version; nothing was written.</summary>
    Unchanged = 2,

    /// <summary>No organization has that slug.</summary>
    NoSuchOrganization = 3,

    /// <summary>No plan has that code, or it has no version with that number.</summary>
    NoSuchPlan = 4,

    /// <summary>The version takes effect later; an organization cannot be moved onto it yet.</summary>
    NotYetEffective = 5,
}

/// <summary>
/// Moves an organization to another plan version (Milestone 3.7, ADR-0028). For operators only, by the owner's
/// decision: no customer permission reaches it before billing exists, so nobody can raise their own limits while
/// nothing charges for them. Its callers are the operator command now and the billing webhook later (Phase D).
/// </summary>
public static class PlanAssignment
{
    /// <summary>
    /// Moves the organization onto a version of a plan: the one numbered, or the plan's latest version in effect. Every
    /// move is audited, with the old and the new version and the operator who made it.
    /// </summary>
    public static async Task<PlanChange> MoveAsync(
        AutestiqDbContext database,
        TenantWriteScope writeScope,
        TimeProvider clock,
        string organizationSlug,
        string planCode,
        int? versionNumber,
        string operatorName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(writeScope);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.GetUtcNow();

        var organization = await database.Organizations
            .FirstOrDefaultAsync(candidate => candidate.Slug == organizationSlug, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            return PlanChange.NoSuchOrganization;
        }

        var versions = database.PlanVersions.Where(version =>
            database.Plans.Any(plan => plan.Id == version.PlanId && plan.Code == planCode));

        var target = versionNumber is { } number
            ? await versions.FirstOrDefaultAsync(version => version.Number == number, cancellationToken).ConfigureAwait(false)
            : await versions.Where(version => version.EffectiveFrom <= now).OrderByDescending(version => version.Number)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            return PlanChange.NoSuchPlan;
        }

        if (target.EffectiveFrom > now)
        {
            return PlanChange.NotYetEffective;
        }

        if (organization.PlanVersionId == target.Id)
        {
            return PlanChange.Unchanged;
        }

        var before = organization.PlanVersionId;
        organization.PlanVersionId = target.Id;

        new AuditWriter(database, clock).Record(
            organization.Id,
            actorUserId: null,
            actorEmail: operatorName,
            AuditActions.PlanChanged,
            nameof(Organization),
            organization.Id,
            organization.Name,
            before: new { PlanVersionId = before },
            after: new { PlanVersionId = target.Id, Plan = planCode, Version = target.Number });

        using (writeScope.AllowWritingTo(organization.Id))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return PlanChange.Moved;
    }
}
