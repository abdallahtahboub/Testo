using Microsoft.EntityFrameworkCore;
using Testo.Application.Audit;
using Testo.Contracts.Common;
using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.Persistence;
using Testo.Persistence.Identity;

namespace Testo.Application.Identity;

/// <summary>Offering somebody a place, and their taking it up.</summary>
internal sealed class InvitationService(
    TestoDbContext database,
    CredentialService credentials,
    IInvitationNotifier notifier,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
    : IInvitationService
{
    private static readonly TimeSpan ValidFor = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public async Task<PagedResult<PendingInvitation>> ListPendingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManagePeople))
        {
            return PagedResult.Empty<PendingInvitation>();
        }

        var now = timeProvider.GetUtcNow();

        var query = database.Invitations
            .Where(invitation =>
                invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var invitations = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(invitation => new PendingInvitation(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                database.Users
                    .Where(user => user.Id == invitation.InvitedBy)
                    .Select(user => user.DisplayName)
                    .FirstOrDefault() ?? string.Empty,
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<PendingInvitation>(invitations, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<OperationResult> InviteAsync(
        string email,
        Role role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!currentUser.Can(Permission.ManagePeople) || currentUser.Role is not { } actorRole)
        {
            return OperationResult.Failure("Members_Error_NotAllowed");
        }

        if (!Permissions.MayAssignRole(actorRole, role))
        {
            return OperationResult.Failure("Members_Error_AboveYourRole");
        }

        var organizationId = organizationContext.RequireOrganizationId();
        var normalized = SignInWorkflow.Normalize(email);
        var now = timeProvider.GetUtcNow();

        var alreadyAMember = await database.Memberships.AnyAsync(
                membership => membership.User!.NormalizedEmail == normalized
                    && membership.Status == MembershipStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyAMember)
        {
            return OperationResult.Failure("Members_Error_AlreadyAMember");
        }

        // A second invitation replaces the first, so that only one link is ever live for an
        // address and re-sending is not a way to collect working links.
        var open = await database.Invitations
            .Where(invitation =>
                invitation.NormalizedEmail == normalized
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var superseded in open)
        {
            superseded.RevokedAt = now;
        }

        var token = Tokens.New();

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Email = email.Trim(),
            NormalizedEmail = normalized,
            Role = role,
            TokenHash = Tokens.HashOf(token)!,
            ExpiresAt = now.Add(ValidFor),
            InvitedBy = currentUser.UserId ?? Guid.Empty,
        };

        database.Invitations.Add(invitation);

        Writer().Record(
            organizationId,
            currentUser.UserId,
            currentUser.Email,
            AuditActions.MemberInvited,
            nameof(Invitation),
            invitation.Id,
            invitation.Email,
            after: new { Role = role.ToString() });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var organizationName = await database.Organizations
            .Where(organization => organization.Id == organizationId)
            .Select(organization => organization.Name)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        await notifier.SendInvitationAsync(invitation.Email, organizationName, token, cancellationToken)
            .ConfigureAwait(false);

        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OperationResult> RevokeAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManagePeople))
        {
            return OperationResult.Failure("Members_Error_NotAllowed");
        }

        var invitation = await database.Invitations
            .FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null || !invitation.IsOpenAt(timeProvider.GetUtcNow()))
        {
            return OperationResult.Failure("Members_Error_InvitationNotFound");
        }

        invitation.RevokedAt = timeProvider.GetUtcNow();

        Writer().Record(
            invitation.OrganizationId,
            currentUser.UserId,
            currentUser.Email,
            AuditActions.InvitationRevoked,
            nameof(Invitation),
            invitation.Id,
            invitation.Email);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<InvitationDetails?> DescribeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken).ConfigureAwait(false);

        if (invitation?.Organization is null)
        {
            return null;
        }

        var hasAccount = await database.Users
            .AnyAsync(user => user.NormalizedEmail == invitation.NormalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        var invitedBy = await database.Users
            .Where(user => user.Id == invitation.InvitedBy)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new InvitationDetails(
            invitation.Email,
            invitation.Organization.Name,
            invitation.Role,
            invitedBy ?? string.Empty,
            !hasAccount);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AcceptAsync(
        string token,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken).ConfigureAwait(false);

        if (invitation is null)
        {
            return OperationResult.Failure("Invitation_Error_NotUsable");
        }

        var now = timeProvider.GetUtcNow();

        var user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == invitation.NormalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrEmpty(password))
            {
                return OperationResult.Failure("Invitation_Error_AccountDetailsMissing");
            }

            user = new User
            {
                Email = invitation.Email,
                NormalizedEmail = invitation.NormalizedEmail,
                DisplayName = displayName.Trim(),

                // Following a link that only reached this address is proof enough that it is
                // theirs; asking them to confirm it a second time would say otherwise.
                EmailConfirmed = true,
            };

            var problems = await credentials.SetPasswordAsync(user, password).ConfigureAwait(false);
            if (problems.Count > 0)
            {
                return new OperationResult(false, problems);
            }

            database.Users.Add(user);
        }

        var membership = await database.Memberships
            .IgnoreQueryFilters() // Nobody is in an organization while they are still joining one.
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == user.Id && candidate.OrganizationId == invitation.OrganizationId,
                cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            database.Memberships.Add(new Membership
            {
                // Set here rather than by the guard: the person is not yet in this organization,
                // so there is no request context to take it from.
                OrganizationId = invitation.OrganizationId,
                UserId = user.Id,
                Role = invitation.Role,
                JoinedAt = now,
            });
        }
        else
        {
            membership.Status = MembershipStatus.Active;
            membership.Role = invitation.Role;
            membership.RemovedAt = null;
        }

        invitation.AcceptedAt = now;

        Writer().Record(
            invitation.OrganizationId,
            user.Id,
            user.Email,
            AuditActions.MemberJoined,
            nameof(Membership),
            user.Id,
            user.DisplayName,
            after: new { Role = invitation.Role.ToString() });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <summary>
    /// The open invitation a token refers to, or <c>null</c>. The lookup crosses the tenant
    /// boundary on purpose: the link is all the person has, and they are not yet in the
    /// organization it points at.
    /// </summary>
    private async Task<Invitation?> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (Tokens.HashOf(token) is not { } hash)
        {
            return null;
        }

        var invitation = await database.Invitations
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Organization)
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        return invitation?.IsOpenAt(timeProvider.GetUtcNow()) == true ? invitation : null;
    }

    private AuditWriter Writer() => new(database, timeProvider);
}
