using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Testo.Application.Execution;

/// <summary>
/// Makes and checks the short-lived links an artifact is fetched through.
/// </summary>
/// <remarks>
/// <para>
/// The link is not the authorization. Whoever follows it still has to be signed in, and the
/// artifact is still read through the tenant filter, so another organization's artifact does not
/// exist to them however good their link is.
/// </para>
/// <para>
/// What the signature adds is a <em>bound</em>: a link that leaks — in a chat message, a bug
/// report, a browser history on a shared machine — stops working. That is the shape object storage
/// will need when the bytes move off this machine, so it is worth having now rather than later
/// (ADR-0018).
/// </para>
/// </remarks>
public sealed class ArtifactLinks(IOptions<ArtifactStoreOptions> options, TimeProvider timeProvider)
{
    private readonly byte[] _key = Key(options.Value.SigningKey);
    private readonly TimeSpan _lifetime = options.Value.LinkLifetime;

    /// <summary>A token that lets this artifact be fetched, for a while.</summary>
    public string Sign(Guid artifactId)
    {
        var expires = timeProvider.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds();

        return $"{expires}.{Base64Url.EncodeToString(Signature(artifactId, expires))}";
    }

    /// <summary>When the token this moment produces stops working.</summary>
    public DateTimeOffset ExpiresAt() => timeProvider.GetUtcNow().Add(_lifetime);

    /// <summary>Whether a token is ours, for this artifact, and not yet expired.</summary>
    public bool IsValid(Guid artifactId, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var dot = token.IndexOf('.', StringComparison.Ordinal);

        if (dot <= 0
            || !long.TryParse(token.AsSpan(0, dot), CultureInfo.InvariantCulture, out var expires)
            || expires < timeProvider.GetUtcNow().ToUnixTimeSeconds())
        {
            return false;
        }

        byte[] presented;

        try
        {
            presented = Base64Url.DecodeFromChars(token.AsSpan(dot + 1));
        }
        catch (FormatException)
        {
            return false;
        }

        // Fixed-time, so that a wrong signature takes as long to reject as a right one.
        return CryptographicOperations.FixedTimeEquals(presented, Signature(artifactId, expires));
    }

    private byte[] Signature(Guid artifactId, long expires) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{artifactId:N}.{expires}"));

    private static byte[] Key(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(configured);
}
