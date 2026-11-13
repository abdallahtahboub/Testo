using Microsoft.AspNetCore.Identity;
using Testo.Domain.Identity;

namespace Testo.Persistence.Identity;

/// <summary>
/// Refuses passwords that are already known to attackers
/// (<c>docs/identity-and-access.md</c> §1).
/// </summary>
/// <remarks>
/// <para>
/// A twelve-character minimum rules out most of what appears in a breach list, so this catches
/// what survives it: a long password that is still a familiar phrase, a keyboard run, or one
/// character repeated.
/// </para>
/// <para>
/// This is a floor, not the finished feature. Checking against the full Have I Been Pwned set,
/// by sending the first five characters of the hash and comparing the rest locally, needs an
/// outbound host and belongs with the other integrations. Until then nothing here depends on a
/// third party being reachable while somebody is choosing a password.
/// </para>
/// </remarks>
public sealed class BreachedPasswordValidator : IPasswordValidator<User>
{
    /// <summary>
    /// Long passwords that nonetheless appear near the top of every breach list. Compared in
    /// lower case, so "Passwordpassword" is caught too.
    /// </summary>
    private static readonly HashSet<string> WellKnown = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "password123456", "password1234567", "password12345678",
        "123456789012", "1234567890123", "12345678901234", "123456789012345",
        "qwertyuiopasdfgh", "qwertyuiop1234", "qwertyuiopasdf", "1qaz2wsx3edc4rfv",
        "administrator", "administrator1", "letmein123456", "iloveyou123456",
        "welcome123456", "trustno1trustno1", "passw0rdpassw0rd", "correcthorsebatterystaple",
        "thisismypassword", "mypasswordis123", "changemechangeme", "superman123456",
        "abcdefghijkl", "abcdefghijklmnop", "zxcvbnmasdfgh", "asdfghjkl12345",
        "football123456", "baseball123456", "princess123456", "sunshine123456",
        "monkey1234567", "dragon1234567", "master1234567", "shadow1234567",
        "qazwsxedcrfv", "poiuytrewqlkj", "temppassword123", "defaultpassword",
        "passwordisgood", "secretpassword", "pleaseletmein123", "iamtheadministrator",
    };

    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        if (WellKnown.Contains(password) || IsOneCharacterRepeated(password) || IsARun(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "Breached" }));
        }

        return Task.FromResult(IdentityResult.Success);
    }

    /// <summary>"aaaaaaaaaaaa" is long, and worthless.</summary>
    private static bool IsOneCharacterRepeated(string password) =>
        password.All(character => character == password[0]);

    /// <summary>"abcdefghijkl" and "987654321098" are long, and nearly as worthless.</summary>
    private static bool IsARun(string password)
    {
        var step = password[1] - password[0];

        if (step is not (1 or -1))
        {
            return false;
        }

        for (var index = 2; index < password.Length; index++)
        {
            if (password[index] - password[index - 1] != step)
            {
                return false;
            }
        }

        return true;
    }
}
