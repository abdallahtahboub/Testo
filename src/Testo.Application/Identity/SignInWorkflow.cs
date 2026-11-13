using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testo.Application.Audit;
using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.Persistence;
using Testo.Persistence.Identity;

namespace Testo.Application.Identity;

/// <summary>
/// Deciding whether somebody may come in. Issuing the session is the host's job, because only it
/// may set a cookie; this decides, and hands over a <see cref="SignInTicket"/>.
/// </summary>
/// <remarks>
/// Every refusal looks and costs the same from outside. A wrong password, an address with no
/// account and an address with an account but no active membership all give
/// <see cref="SignInOutcome.Failed"/> after the same work, so the screen cannot be used to find
/// out who has an account (<c>docs/identity-and-access.md</c> §5).
/// </remarks>
public sealed class SignInWorkflow(
    TestoDbContext database,
    CredentialService credentials,
    TimeProvider timeProvider,
    ILogger<SignInWorkflow> logger)
{
    private const int MaxFailedAttempts = 10;
    private static readonly TimeSpan LockoutPeriod = TimeSpan.FromMinutes(15);

    /// <summary>Checks an address and a password.</summary>
    /// <param name="email">What was typed into the address field.</param>
    /// <param name="password">What was typed into the password field.</param>
    /// <param name="ipAddress">Where the attempt came from, for the log.</param>
    /// <param name="cancellationToken">Stops the work if the request goes away.</param>
    public async Task<(SignInResult Result, SignInTicket? Ticket)> AuthenticateAsync(
        string email,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        var now = timeProvider.GetUtcNow();

        var user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            // Spend the time a real check would have taken, so the reply is not quicker for an
            // address nobody has.
            credentials.SpendTheSameTime(password);
            IdentityLog.UnknownAddress(logger, ipAddress);
            return (new SignInResult(SignInOutcome.Failed), null);
        }

        if (user.LockoutEnd is { } until && until > now)
        {
            return (new SignInResult(SignInOutcome.LockedOut, LockoutEnds: until), null);
        }

        if (!credentials.VerifyPassword(user, password))
        {
            return (await RecordFailureAsync(user, ipAddress, cancellationToken).ConfigureAwait(false), null);
        }

        var membership = await FindMembershipAsync(user.Id, cancellationToken).ConfigureAwait(false);

        if (membership?.Organization is null)
        {
            // The credentials were right, but this person belongs nowhere yet — every membership
            // was withdrawn, or they only ever had an invitation.
            IdentityLog.NoActiveMembership(logger, user.Id);
            return (new SignInResult(SignInOutcome.NoActiveMembership, user.Id), null);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        if (membership.Organization.RequiresTwoFactor && !user.TwoFactorEnabled)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (new SignInResult(SignInOutcome.RequiresTwoFactorSetup, user.Id, membership.OrganizationId), null);
        }

        if (user.TwoFactorEnabled)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (new SignInResult(SignInOutcome.RequiresTwoFactor, user.Id, membership.OrganizationId), null);
        }

        return (
            await CompleteAsync(user, membership, ipAddress, cancellationToken).ConfigureAwait(false),
            TicketFor(user, membership));
    }

    /// <summary>Finishes a sign-in that was waiting for a one-time code.</summary>
    public async Task<(SignInResult Result, SignInTicket? Ticket)> VerifyCodeAsync(
        Guid userId,
        string code,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !await credentials.VerifyAuthenticatorCodeAsync(user, code).ConfigureAwait(false))
        {
            if (user is not null)
            {
                return (await RecordFailureAsync(user, ipAddress, cancellationToken).ConfigureAwait(false), null);
            }

            return (new SignInResult(SignInOutcome.Failed), null);
        }

        var membership = await FindMembershipAsync(user.Id, cancellationToken).ConfigureAwait(false);

        if (membership?.Organization is null)
        {
            return (new SignInResult(SignInOutcome.NoActiveMembership, user.Id), null);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        return (
            await CompleteAsync(user, membership, ipAddress, cancellationToken).ConfigureAwait(false),
            TicketFor(user, membership));
    }

    /// <summary>
    /// The ticket for another organization this person belongs to, for the switcher. Their
    /// credentials are not asked for again; their membership there is.
    /// </summary>
    public async Task<SignInTicket?> TicketForOrganizationAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var membership = await database.Memberships
            .IgnoreQueryFilters() // The person is leaving one organization for another.
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId
                    && candidate.OrganizationId == organizationId
                    && candidate.Status == MembershipStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);

        return membership?.User is null || membership.Organization is null
            ? null
            : TicketFor(membership.User, membership);
    }

    /// <summary>
    /// Which organization a person lands in. The one they joined first, so that signing in twice
    /// puts them in the same place twice.
    /// </summary>
    private Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        database.Memberships
            .IgnoreQueryFilters() // Nobody is in an organization yet; that is what this decides.
            .Include(membership => membership.Organization)
            .Where(membership =>
                membership.UserId == userId
                && membership.Status == MembershipStatus.Active
                && membership.Organization!.Status == OrganizationStatus.Active)
            .OrderBy(membership => membership.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<SignInResult> CompleteAsync(
        User user,
        Membership membership,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        new AuditWriter(database, timeProvider).Record(
            membership.OrganizationId,
            user.Id,
            user.Email,
            AuditActions.SignedIn,
            nameof(User),
            user.Id,
            user.DisplayName,
            ipAddress);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SignInResult(SignInOutcome.Succeeded, user.Id, membership.OrganizationId);
    }

    private async Task<SignInResult> RecordFailureAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;

        var lockedOut = user.AccessFailedCount >= MaxFailedAttempts;
        if (lockedOut)
        {
            // Locked, and the person is told so, with a way back in. A silent lock looks like a
            // broken password to the one person it hurts most (docs/identity-and-access.md §5).
            user.LockoutEnd = timeProvider.GetUtcNow().Add(LockoutPeriod);
            user.AccessFailedCount = 0;
        }

        var membership = await FindMembershipAsync(user.Id, cancellationToken).ConfigureAwait(false);

        if (membership is not null)
        {
            new AuditWriter(database, timeProvider).Record(
                membership.OrganizationId,
                user.Id,
                user.Email,
                AuditActions.SignInFailed,
                nameof(User),
                user.Id,
                user.DisplayName,
                ipAddress,
                after: new { lockedOut });
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return lockedOut
            ? new SignInResult(SignInOutcome.LockedOut, LockoutEnds: user.LockoutEnd)
            : new SignInResult(SignInOutcome.Failed);
    }

    private static SignInTicket TicketFor(User user, Membership membership) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        membership.OrganizationId,
        membership.Organization!.Name,
        membership.Role,
        user.SecurityStamp,
        user.Locale);

    /// <summary>
    /// The form an address is compared in. Upper case and invariant, so that the same person
    /// typing their address differently is still the same person.
    /// </summary>
    internal static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
