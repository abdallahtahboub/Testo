using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Testo.Application.Audit;
using Testo.Contracts.Common;
using Testo.Domain.Identity;
using Testo.Persistence;
using Testo.Persistence.Identity;

namespace Testo.Application.Identity;

/// <summary>"I have forgotten my password", and what follows.</summary>
/// <remarks>
/// Asking for a reset says nothing about whether the address has an account: the reply is the
/// same either way, and the screen that follows says only that a link has been sent if the
/// address is known (<c>docs/identity-and-access.md</c> §5).
/// </remarks>
public sealed class PasswordResetWorkflow(
    TestoDbContext database,
    CredentialService credentials,
    TenantWriteScope writeScope,
    IInvitationNotifier notifier,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan ValidFor = TimeSpan.FromHours(2);

    /// <summary>Sends a link, if there is somebody to send it to.</summary>
    public async Task RequestAsync(string email, string? ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = SignInWorkflow.Normalize(email);
        var now = timeProvider.GetUtcNow();

        var user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return;
        }

        // Only one live link at a time. Asking again replaces the last one rather than adding to
        // a growing set of ways in.
        var outstanding = await database.PasswordResets
            .Where(reset => reset.UserId == user.Id && reset.UsedAt == null && reset.ExpiresAt > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var superseded in outstanding)
        {
            superseded.UsedAt = now;
        }

        var token = Tokens.New();

        database.PasswordResets.Add(new PasswordReset
        {
            UserId = user.Id,
            TokenHash = Tokens.HashOf(token)!,
            ExpiresAt = now.Add(ValidFor),
            RequestedFromIp = ipAddress,
        });

        using (await RecordAgainstEveryOrganizationAsync(
                user,
                AuditActions.PasswordResetRequested,
                ipAddress,
                cancellationToken)
            .ConfigureAwait(false))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await notifier.SendPasswordResetAsync(user.Email, token, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the new password. Succeeding changes the account's security stamp, which ends every
    /// session that person had — the point of a reset after a password has been lost.
    /// </summary>
    public async Task<OperationResult> ResetAsync(
        string token,
        string newPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (Tokens.HashOf(token) is not { } hash)
        {
            return OperationResult.Failure("PasswordReset_Error_LinkNotUsable");
        }

        var reset = await database.PasswordResets
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();

        if (reset?.User is null || !reset.IsUsableAt(now))
        {
            return OperationResult.Failure("PasswordReset_Error_LinkNotUsable");
        }

        var problems = await credentials.SetPasswordAsync(reset.User, newPassword).ConfigureAwait(false);
        if (problems.Count > 0)
        {
            return new OperationResult(false, problems);
        }

        reset.UsedAt = now;

        // Somebody who could not get in has got in. Whatever locked them out goes with it.
        reset.User.LockoutEnd = null;
        reset.User.AccessFailedCount = 0;

        using (await RecordAgainstEveryOrganizationAsync(
                reset.User,
                AuditActions.PasswordReset,
                ipAddress,
                cancellationToken)
            .ConfigureAwait(false))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return OperationResult.Success;
    }

    /// <summary>
    /// An account is not inside any one organization, but each organization the person belongs to
    /// has a right to see that their credentials changed — it is the sort of thing a security
    /// review asks about.
    /// </summary>
    /// <summary>
    /// Writes the entries and saves, holding the permission for exactly the organizations
    /// involved. Returns nothing; the caller has already made its other changes.
    /// </summary>
    private async Task<IDisposable> RecordAgainstEveryOrganizationAsync(
        User user,
        string action,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var organizationIds = await database.Memberships
            .IgnoreQueryFilters() // Nobody is signed in while they are resetting a password.
            .Where(membership => membership.UserId == user.Id && membership.Status == MembershipStatus.Active)
            .Select(membership => membership.OrganizationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var writer = new AuditWriter(database, timeProvider);

        foreach (var organizationId in organizationIds)
        {
            writer.Record(organizationId, user.Id, user.Email, action, nameof(User), user.Id, user.DisplayName, ipAddress);
        }

        // Nobody is signed in during a reset, so every one of these is a write outside the current
        // organization and each is declared (D-2). The permission ends with the save.
        return writeScope.AllowWritingTo(CollectionsMarshal.AsSpan(organizationIds));
    }
}
