using Autestiq.Contracts.Common;

namespace Autestiq.Contracts.Execution;

/// <summary>Where an organization's proof that it controls an origin stands (Milestone 3.5).</summary>
public enum OriginProofStatus
{
    /// <summary>A token was issued and nobody has asked for it to be checked yet.</summary>
    Issued = 1,

    /// <summary>A check was asked for and waits for, or is with, a worker.</summary>
    Checking = 2,

    /// <summary>The origin held the token when it was last checked, and the proof has not expired.</summary>
    Verified = 3,

    /// <summary>The last check did not find the token; the reason says why.</summary>
    Failed = 4,
}

/// <summary>What to publish on an origin to prove control of it.</summary>
/// <param name="Origin">The origin, as it is stored: scheme, host and port.</param>
/// <param name="Token">The text the file must hold, and nothing else.</param>
/// <param name="FileUrl">Where the file must be served.</param>
public sealed record OriginProofRequest(string Origin, string Token, string FileUrl);

/// <summary>An organization's proof for one origin.</summary>
public sealed record OriginProofState(
    string Origin,
    OriginProofStatus Status,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ExpiresAt,
    string? FailureReason);

/// <summary>
/// Proving that an organization controls an origin before any run may be aimed at it. Requires
/// <see cref="Identity.Permission.ManageProjects"/> to change, <see cref="Identity.Permission.Read"/> to read.
/// </summary>
public interface IOriginProofService
{
    /// <summary>The token to publish for an origin: a new one the first time, the same one after.</summary>
    Task<OperationResult<OriginProofRequest>> RequestAsync(string origin, CancellationToken cancellationToken = default);

    /// <summary>Asks for the origin to be checked. The check is done by a worker, and its result read later.</summary>
    Task<OperationResult> CheckAsync(string origin, CancellationToken cancellationToken = default);

    /// <summary>Where the proof for an origin stands, or <c>null</c> when none was asked for.</summary>
    Task<OriginProofState?> GetAsync(string origin, CancellationToken cancellationToken = default);
}

/// <summary>Origins written one way, so that "HTTPS://Shop.Example" and "https://shop.example:443/" are one.</summary>
public static class Origins
{
    /// <summary>
    /// The origin as <c>scheme://host:port</c>, lower case, with the port always written; or <c>null</c> for
    /// anything that is not a plain http or https origin: a path, a query, a fragment or a user name.
    /// </summary>
    public static string? Normalize(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0
            || uri.AbsolutePath is not ("/" or "")
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.IdnHost.Length == 0)
        {
            return null;
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{uri.Scheme}://{uri.IdnHost.ToLowerInvariant()}:{uri.Port}");
    }
}
