using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testo.Domain.Identity;

namespace Testo.Persistence.Identity;

/// <summary>
/// Lets ASP.NET Core Identity read and write our own <see cref="User"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// Identity ships stores of its own, but they require the user to inherit from
/// <c>IdentityUser</c>, which would put a storage concern into the domain and a reference to
/// Identity into every layer above it. Writing the store instead keeps that dependency in this
/// project alone, which is what ADR-0013 promises: swapping to an external provider changes one
/// project.
/// </para>
/// <para>
/// There is no user name separate from the address. A person signs in with their email, so
/// Identity's notion of a user name is mapped onto it rather than invented alongside it.
/// </para>
/// </remarks>
public sealed class UserStore(TestoDbContext database) :
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>,
    IUserTwoFactorStore<User>,
    IUserAuthenticatorKeyStore<User>
{
    /// <inheritdoc />
    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.Id.ToString());
    }

    /// <inheritdoc />
    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.Email);
    }

    /// <inheritdoc />
    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Email = userName ?? user.Email;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.NormalizedEmail);
    }

    /// <inheritdoc />
    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedEmail = normalizedName ?? user.NormalizedEmail;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.ConcurrencyStamp = Guid.CreateVersion7().ToString("N");
        database.Users.Update(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else changed the same account first. Identity turns this into a result the
            // screen can show, rather than an error page.
            return IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure" });
        }

        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        database.Users.Remove(user);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? await database.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken).ConfigureAwait(false)
            : null;

    /// <inheritdoc />
    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        FindByEmailAsync(normalizedUserName, cancellationToken);

    /// <inheritdoc />
    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await database.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash);
    }

    /// <inheritdoc />
    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash is not null);
    }

    /// <inheritdoc />
    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Email = email ?? user.Email;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.Email);
    }

    /// <inheritdoc />
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.EmailConfirmed);
    }

    /// <inheritdoc />
    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.NormalizedEmail);
    }

    /// <inheritdoc />
    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedEmail = normalizedEmail ?? user.NormalizedEmail;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.SecurityStamp);
    }

    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.LockoutEnd);
    }

    /// <inheritdoc />
    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(++user.AccessFailedCount);
    }

    /// <inheritdoc />
    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.AccessFailedCount);
    }

    /// <summary>
    /// Always on. Guessing a password is the attack this product is most likely to meet, so there
    /// is no account for which the lockout is switched off (<c>docs/identity-and-access.md</c> §5).
    /// </summary>
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <inheritdoc cref="GetLockoutEnabledAsync" />
    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.TwoFactorEnabled);
    }

    /// <inheritdoc />
    public Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.AuthenticatorKey = key;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.AuthenticatorKey);
    }

    /// <summary>Nothing to release: the database context belongs to the request, not to this store.</summary>
    public void Dispose()
    {
        // Intentionally empty.
    }
}
