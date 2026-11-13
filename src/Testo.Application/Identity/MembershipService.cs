using Microsoft.EntityFrameworkCore;
using Testo.Application.Audit;
using Testo.Contracts.Common;
using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.Persistence;

namespace Testo.Application.Identity;

/// <summary>Who belongs to the current organization, and what they may do there.</summary>
internal sealed class MembershipService(
    TestoDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
    : IMembershipService
{
    /// <inheritdoc />
    public async Task<PagedResult<OrganizationMember>> ListMembersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return PagedResult.Empty<OrganizationMember>();
        }

        var query = database.Memberships
            .Where(membership => membership.Status == MembershipStatus.Active)
            .OrderBy(membership => membership.User!.DisplayName);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var members = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(membership => new OrganizationMember(
                membership.Id,
                membership.UserId,
                membership.User!.DisplayName,
                membership.User.Email,
                membership.Role,
                membership.JoinedAt,
                membership.Status == MembershipStatus.Active,
                membership.User.TwoFactorEnabled))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<OrganizationMember>(members, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationSummary>> ListMyOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return [];
        }

        return await database.Memberships
            .IgnoreQueryFilters() // The switcher is the one list that spans organizations.
            .Where(membership =>
                membership.UserId == userId
                && membership.Status == MembershipStatus.Active
                && membership.Organization!.Status == OrganizationStatus.Active)
            .OrderBy(membership => membership.Organization!.Name)
            .Select(membership => new OrganizationSummary(
                membership.OrganizationId,
                membership.Organization!.Name,
                membership.Organization.Slug,
                membership.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> ChangeRoleAsync(
        Guid membershipId,
        Role newRole,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManagePeople) || currentUser.Role is not { } actorRole)
        {
            return OperationResult.Failure("Members_Error_NotAllowed");
        }

        var membership = await database.Memberships
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == membershipId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return OperationResult.Failure("Members_Error_NotFound");
        }

        if (membership.UserId == currentUser.UserId)
        {
            // Otherwise the only owner could quietly make themselves an admin and lock the
            // organization out of its own billing.
            return OperationResult.Failure("Members_Error_CannotChangeOwnRole");
        }

        // Reach is needed twice: over the role being given, and over the role already held. An
        // admin may not demote an owner by first being allowed to assign "admin".
        if (!Permissions.MayAssignRole(actorRole, newRole) || !Permissions.MayAssignRole(actorRole, membership.Role))
        {
            return OperationResult.Failure("Members_Error_AboveYourRole");
        }

        if (membership.Role == Role.Owner && newRole != Role.Owner
            && !await AnotherOwnerExistsAsync(membership.Id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure("Members_Error_LastOwner");
        }

        var previousRole = membership.Role;
        membership.Role = newRole;

        Writer().Record(
            organizationContext.RequireOrganizationId(),
            currentUser.UserId,
            currentUser.Email,
            AuditActions.MemberRoleChanged,
            nameof(Membership),
            membership.Id,
            membership.User?.DisplayName,
            before: new { Role = previousRole.ToString() },
            after: new { Role = newRole.ToString() });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemoveMemberAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManagePeople) || currentUser.Role is not { } actorRole)
        {
            return OperationResult.Failure("Members_Error_NotAllowed");
        }

        var membership = await database.Memberships
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == membershipId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null || membership.Status != MembershipStatus.Active)
        {
            return OperationResult.Failure("Members_Error_NotFound");
        }

        if (membership.UserId == currentUser.UserId)
        {
            return OperationResult.Failure("Members_Error_CannotRemoveYourself");
        }

        if (!Permissions.MayAssignRole(actorRole, membership.Role))
        {
            return OperationResult.Failure("Members_Error_AboveYourRole");
        }

        if (membership.Role == Role.Owner
            && !await AnotherOwnerExistsAsync(membership.Id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure("Members_Error_LastOwner");
        }

        // The row stays. Their work keeps their name on it; only the access goes
        // (docs/identity-and-access.md §3). Every session of theirs in this organization stops at
        // the next request, because SessionValidator looks this status up each time.
        membership.Status = MembershipStatus.Removed;
        membership.RemovedAt = timeProvider.GetUtcNow();

        Writer().Record(
            organizationContext.RequireOrganizationId(),
            currentUser.UserId,
            currentUser.Email,
            AuditActions.MemberRemoved,
            nameof(Membership),
            membership.Id,
            membership.User?.DisplayName,
            before: new { Status = nameof(MembershipStatus.Active), Role = membership.Role.ToString() },
            after: new { Status = nameof(MembershipStatus.Removed) });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <summary>
    /// Whether an owner other than this membership is still there. "At least one always exists"
    /// is the rule the product refuses to break (<c>docs/identity-and-access.md</c> §2).
    /// </summary>
    private Task<bool> AnotherOwnerExistsAsync(Guid exceptMembershipId, CancellationToken cancellationToken) =>
        database.Memberships.AnyAsync(
            membership => membership.Id != exceptMembershipId
                && membership.Role == Role.Owner
                && membership.Status == MembershipStatus.Active,
            cancellationToken);

    private AuditWriter Writer() => new(database, timeProvider);
}
