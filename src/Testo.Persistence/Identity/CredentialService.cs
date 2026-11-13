using Microsoft.AspNetCore.Identity;
using Testo.Domain.Identity;

namespace Testo.Persistence.Identity;

/// <summary>
/// Everything that needs ASP.NET Core Identity, expressed in the product's own terms.
/// </summary>
/// <remarks>
/// This is the whole of the Identity dependency. The application layer calls these methods and
/// never sees <c>UserManager</c>, <c>IdentityResult</c> or a token provider, which is what lets a
/// per-organization identity provider be added later by replacing one project (ADR-0013).
/// </remarks>
public sealed class CredentialService(UserManager<User> users, IPasswordHasher<User> hasher)
{
    /// <summary>
    /// A hash of a password nobody has, used to spend the same time on an address that has no
    /// account as on one that does. Without it, a quick "no" tells an attacker the address is
    /// unknown (<c>docs/identity-and-access.md</c> §5).
    /// </summary>
    private readonly string _decoyHash = hasher.HashPassword(
        new User { Email = "decoy", NormalizedEmail = "DECOY", DisplayName = "decoy" },
        "a password that is nobody's");

    /// <summary>Whether this is the person's password.</summary>
    public bool VerifyPassword(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash ?? _decoyHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

    /// <summary>
    /// Spends the time a real check would take, for an address with no account. The result is
    /// discarded; only the delay matters.
    /// </summary>
    public void SpendTheSameTime(string password) =>
        hasher.VerifyHashedPassword(
            new User { Email = "decoy", NormalizedEmail = "DECOY", DisplayName = "decoy" },
            _decoyHash,
            password);

    /// <summary>
    /// Sets a password, after checking it against the rules and the breached-password list.
    /// Returns the resource keys of whatever it failed on.
    /// </summary>
    public async Task<IReadOnlyList<string>> SetPasswordAsync(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return [.. result.Errors.Select(error => KeyFor(error.Code))];
            }
        }

        user.PasswordHash = hasher.HashPassword(user, password);

        // Every existing session of this person stops being valid, which is the point of changing
        // a password after it has been lost.
        user.SecurityStamp = Guid.CreateVersion7().ToString("N");
        return [];
    }

    /// <summary>
    /// The resource key for one of Identity's error codes.
    /// </summary>
    /// <remarks>
    /// Mapped rather than built from the code, so that every key a person can be shown exists as
    /// a literal somewhere. A key assembled at run time cannot be checked against the resource
    /// files, and the first anyone hears of a missing one is a person seeing its name.
    /// </remarks>
    private static string KeyFor(string code) => code switch
    {
        "PasswordTooShort" => "Password_PasswordTooShort",
        "PasswordRequiresUniqueChars" => "Password_PasswordRequiresUniqueChars",
        "Breached" => "Password_Breached",
        _ => "Password_NotAcceptable",
    };

    /// <summary>A new shared secret for an authenticator application.</summary>
    public string NewAuthenticatorKey() => users.GenerateNewAuthenticatorKey();

    /// <summary>Whether a six-digit code matches the person's authenticator.</summary>
    public Task<bool> VerifyAuthenticatorCodeAsync(User user, string code) =>
        users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
}
