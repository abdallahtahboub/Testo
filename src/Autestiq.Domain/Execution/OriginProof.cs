using Autestiq.Contracts.Execution;
using Autestiq.Domain.Common;

namespace Autestiq.Domain.Execution;

/// <summary>
/// An organization's proof that it controls an origin: a token it was given, published on the origin and checked
/// by a worker. No run may be aimed at an origin that is not ours without one that is verified and unexpired.
/// </summary>
/// <remarks>
/// One per organization and origin. A second organization that wants the same origin gets a token of its own and
/// must publish that: one organization's proof is never another's.
/// </remarks>
public sealed class OriginProof : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The origin, normalized (<see cref="Origins.Normalize"/>).</summary>
    public required string Origin { get; set; }

    /// <summary>The token the origin's proof file must hold.</summary>
    public required string Token { get; set; }

    /// <summary>Where it stands.</summary>
    public OriginProofStatus Status { get; set; } = OriginProofStatus.Issued;

    /// <summary>When the token was issued.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>When a check was last asked for.</summary>
    public DateTimeOffset? CheckRequestedAt { get; set; }

    /// <summary>When a dispatcher took the check to a worker; <c>null</c> while it waits.</summary>
    public DateTimeOffset? CheckStartedAt { get; set; }

    /// <summary>When the last check finished.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>When the origin last held the token.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>When the proof stops counting, and the origin must be checked again.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Why the last check failed.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Whether a run may be aimed at the origin now, on this proof.</summary>
    public bool CountsAt(DateTimeOffset now) => Status == OriginProofStatus.Verified && ExpiresAt > now;
}
