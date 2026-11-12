using System.Security.Cryptography;

namespace Testo.Web.Security;

/// <summary>
/// A random value, new for every request, that marks the one inline script an app page
/// needs (the import map) as ours. The content security policy allows only that script.
/// </summary>
internal sealed class CspNonce
{
    /// <summary>
    /// 128 random bits as hexadecimal digits. Hexadecimal on purpose: the value is written
    /// into an HTML attribute and into a response header, and must read the same in both.
    /// Base64 can contain "+", which HTML writes as "&amp;#x2B;".
    /// </summary>
    public string Value { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
