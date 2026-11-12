using System.Security.Cryptography;

namespace Testo.Web.Security;

/// <summary>
/// A random value, new for every request, that marks the one inline script an app page
/// needs (the import map) as ours. The content security policy allows only that script.
/// </summary>
internal sealed class CspNonce
{
    public string Value { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
}
