using Testo.Contracts.Common;

namespace Testo.Contracts.Identity;

/// <summary>Offering somebody a place in an organization, and their taking it up.</summary>
public interface IInvitationService
{
    /// <summary>Invitations that have been sent from the current organization and not yet used.</summary>
    Task<PagedResult<PendingInvitation>> ListPendingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an invitation. The reply never reveals whether that address already has an account.
    /// </summary>
    Task<OperationResult> InviteAsync(
        string email,
        Role role,
        CancellationToken cancellationToken = default);

    /// <summary>Withdraws an invitation that has not been accepted.</summary>
    Task<OperationResult> RevokeAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What the link leads to, or <c>null</c> when the token is unknown, used or expired. Shown
    /// before the person commits to anything.
    /// </summary>
    Task<InvitationDetails?> DescribeAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an invitation. A person without an account gives a name and a password here; one
    /// with an account gives neither. Either way they end up a member and signed in.
    /// </summary>
    Task<OperationResult> AcceptAsync(
        string token,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default);
}
