using Testo.Contracts.Identity;
using Testo.Domain.Common;

namespace Testo.Domain.Identity;

/// <summary>
/// An offer of a place in an organization. Single use, seven days, and the token is held only as
/// a hash, so the database cannot hand anyone a working link
/// (<c>docs/identity-and-access.md</c> §3).
/// </summary>
public sealed class Invitation : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The organization the person is invited to.</summary>
    public Organization? Organization { get; set; }

    /// <summary>The address it was sent to. The invitation is only valid for this address.</summary>
    public required string Email { get; set; }

    /// <summary>The upper-case form of <see cref="Email"/>, which lookups use.</summary>
    public required string NormalizedEmail { get; set; }

    /// <summary>The role the person will hold once they accept.</summary>
    public required Role Role { get; set; }

    /// <summary>
    /// The SHA-256 hash of the token in the link. The token itself exists only in the email: a
    /// stolen database backup is then not a set of working invitations.
    /// </summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>When the invitation stops working.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Who sent it.</summary>
    public Guid InvitedBy { get; set; }

    /// <summary>When it was accepted, if it was. An invitation is usable once.</summary>
    public DateTimeOffset? AcceptedAt { get; set; }

    /// <summary>When it was withdrawn, if it was.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Whether the invitation can still be accepted at the given moment.</summary>
    public bool IsOpenAt(DateTimeOffset moment) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > moment;
}
