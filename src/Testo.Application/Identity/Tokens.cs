using System.Buffers.Text;
using System.Security.Cryptography;

namespace Testo.Application.Identity;

/// <summary>
/// The tokens that go into invitation and password-reset links, and the hashes kept instead of
/// them.
/// </summary>
/// <remarks>
/// The token exists in the email and nowhere else. What is stored is its SHA-256 hash, so a
/// stolen backup is not a set of working links. The comparison is done on the hash, which is
/// already fixed length and unpredictable, so no salt or slow hash is needed here — unlike a
/// password, a token is random to begin with.
/// </remarks>
internal static class Tokens
{
    /// <summary>256 bits of randomness, in a form that survives being put in a URL.</summary>
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>What is stored for a token, or <c>null</c> when the token is not a token at all.</summary>
    public static byte[]? HashOf(string token)
    {
        try
        {
            return SHA256.HashData(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            // Somebody edited the link, or a mail client broke it across two lines.
            return null;
        }
    }
}
