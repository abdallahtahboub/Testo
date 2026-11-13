using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testo.Contracts.Identity;
using Testo.Domain.Audit;
using Testo.Domain.Common;

namespace Testo.Persistence.Interceptors;

/// <summary>
/// The second layer of tenant isolation (<c>docs/domain-model.md</c> §4): the organization is put
/// on a new row from the request context, and refused if anybody later tries to change it.
/// </summary>
/// <remarks>
/// The query filter stops one customer <em>reading</em> another's rows. This stops one being
/// <em>written</em> into the wrong place, and stops a row being moved between customers, which no
/// legitimate operation ever does. It also refuses to change or delete an audit entry, because
/// that log is append-only even for an owner (<c>docs/identity-and-access.md</c> §4).
/// </remarks>
public sealed class TenantGuardInterceptor(
    IOrganizationContext organizationContext,
    TenantWriteScope writeScope)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            RefuseToRewriteHistory(entry);

            if (entry.Entity is IBelongsToOrganization tenantEntity)
            {
                GuardTenant(entry, tenantEntity);
            }
        }
    }

    private static void RefuseToRewriteHistory(EntityEntry entry)
    {
        if (entry.Entity is AuditEntry && entry.State is EntityState.Modified or EntityState.Deleted)
        {
            throw new InvalidOperationException(
                "The audit log is append-only: an entry cannot be changed or deleted. "
                + "If an entry is wrong, write another one that says so.");
        }
    }

    private void GuardTenant(EntityEntry entry, IBelongsToOrganization entity)
    {
        var organizationId = nameof(IBelongsToOrganization.OrganizationId);

        switch (entry.State)
        {
            case EntityState.Added when entity.OrganizationId == Guid.Empty:
                // The ordinary case: the row takes the organization of the request that created it.
                entity.OrganizationId = organizationContext.RequireOrganizationId();
                break;

            case EntityState.Added when entity.OrganizationId == organizationContext.OrganizationId:
                // The row says what the request already says. Nothing to decide.
                break;

            case EntityState.Added when writeScope.Allows(entity.OrganizationId):
                // Declared by a flow that legitimately writes outside the current organization:
                // signing in, accepting an invitation, resetting a password. See TenantWriteScope.
                break;

            case EntityState.Added:
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} was about to be written into organization "
                    + $"{entity.OrganizationId}, which is not the one this request is acting in. "
                    + $"If that is deliberate, say so with {nameof(TenantWriteScope)}."
                    + $"{nameof(TenantWriteScope.AllowWritingTo)}.");

            case EntityState.Modified when entry.Property(organizationId).IsModified:
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} cannot be moved between organizations. "
                    + $"{organizationId} is set once, when the row is created.");

            default:
                break;
        }
    }
}
