using System.Text;
using Autestiq.Application.Projects;

namespace Autestiq.Application.Tests.Support;

/// <summary>
/// A stand-in for Data Protection with its essential property: a value reads back only under the purpose it was
/// written with. The real one, with a key ring, is tested where the host is (E2E).
/// </summary>
internal sealed class PurposeBoundProtector : ISecretProtector
{
    public string Protect(string purpose, string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(purpose + "\n" + new string(value.Reverse().ToArray())));

    public string? Unprotect(string purpose, string ciphertext)
    {
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
        var split = text.IndexOf('\n', StringComparison.Ordinal);
        return split > 0 && text[..split] == purpose ? new string(text[(split + 1)..].Reverse().ToArray()) : null;
    }
}
