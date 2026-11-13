using Testo.Contracts.Identity;
using Testo.Domain.Common;

namespace Testo.Domain.Identity;

/// <summary>
/// A person's place in one organization, and the role they hold there. The join that lets one
/// account belong to several customers without their data ever meeting.
/// </summary>
public sealed class Membership : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The organization this membership is in.</summary>
    public Organization? Organization { get; set; }

    /// <summary>The person.</summary>
    public Guid UserId { get; set; }

    /// <summary>The person's account.</summary>
    public User? User { get; set; }

    /// <summary>What they may do here.</summary>
    public required Role Role { get; set; }

    /// <summary>Whether the membership still grants access.</summary>
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    /// <summary>When they joined.</summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// When access was withdrawn. Their work stays and is still attributed to them; only the
    /// access goes (<c>docs/identity-and-access.md</c> §3).
    /// </summary>
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>Whether a membership still grants access.</summary>
public enum MembershipStatus
{
    /// <summary>The person may sign in to this organization.</summary>
    Active = 1,

    /// <summary>Access withdrawn. Kept so that history still has a name against it.</summary>
    Removed = 2,
}
