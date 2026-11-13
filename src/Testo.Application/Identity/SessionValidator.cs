using Microsoft.EntityFrameworkCore;
using Testo.Domain.Identity;
using Testo.Persistence;

namespace Testo.Application.Identity;

/// <summary>
/// Checks, on every request, that a session is still one the product would issue today.
/// </summary>
/// <remarks>
/// <para>
/// Removing somebody has to take effect <em>immediately</em>, not at the next expiry — the worst
/// day in <c>docs/ux/users.md</c> is exactly this not working. That means a lookup per request
/// rather than trusting what the cookie says, which is one indexed read on a table that is small
/// and hot.
/// </para>
/// <para>
/// Checking the membership rather than only the account is what keeps a person's other
/// organizations untouched when one of them lets them go.
/// </para>
/// </remarks>
public sealed class SessionValidator(TestoDbContext database)
{
    /// <summary>Whether this session may continue.</summary>
    /// <param name="userId">Who the cookie says this is.</param>
    /// <param name="organizationId">Which organization it says they are in.</param>
    /// <param name="securityStamp">
    /// What the account's stamp was when the session began. A changed password or a reset makes
    /// this no longer match, which ends every session that person has, everywhere.
    /// </param>
    /// <param name="cancellationToken">Stops the work if the request goes away.</param>
    public Task<bool> IsStillValidAsync(
        Guid userId,
        Guid organizationId,
        string securityStamp,
        CancellationToken cancellationToken = default) =>
        database.Memberships
            .IgnoreQueryFilters() // This runs before the organization context is trusted.
            .AnyAsync(
                membership => membership.UserId == userId
                    && membership.OrganizationId == organizationId
                    && membership.Status == MembershipStatus.Active
                    && membership.Organization!.Status == OrganizationStatus.Active
                    && membership.User!.SecurityStamp == securityStamp,
                cancellationToken);
}
