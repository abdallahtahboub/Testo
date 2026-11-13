using Testo.Contracts.Common;

namespace Testo.Contracts.Identity;

/// <summary>Who belongs to the current organization, and what they may do there.</summary>
public interface IMembershipService
{
    /// <summary>The people in the current organization.</summary>
    Task<PagedResult<OrganizationMember>> ListMembersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>The organizations the signed-in person belongs to, for the switcher.</summary>
    Task<IReadOnlyList<OrganizationSummary>> ListMyOrganizationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes somebody's role. Refuses when it would leave the organization without an owner,
    /// when a person tries to change their own role, or when it would put somebody above the
    /// person doing it (<c>docs/identity-and-access.md</c> §2).
    /// </summary>
    Task<OperationResult> ChangeRoleAsync(
        Guid membershipId,
        Role newRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws somebody's access to this organization, and ends every session they have in it
    /// immediately rather than at the next expiry. Their work stays, still attributed to them.
    /// </summary>
    Task<OperationResult> RemoveMemberAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default);
}
