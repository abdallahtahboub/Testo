namespace Testo.Contracts.Identity;

/// <summary>A person as they appear in the member list of an organization.</summary>
/// <param name="MembershipId">The membership, which is what a role change or removal acts on.</param>
/// <param name="UserId">The person's account.</param>
/// <param name="DisplayName">The name shown next to their work.</param>
/// <param name="Email">Their address.</param>
/// <param name="Role">What they may do here.</param>
/// <param name="JoinedAt">When they joined.</param>
/// <param name="IsActive">Whether the membership still grants access.</param>
/// <param name="HasTwoFactor">Whether they have a second factor, which an admin needs to see.</param>
public sealed record OrganizationMember(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string Email,
    Role Role,
    DateTimeOffset JoinedAt,
    bool IsActive,
    bool HasTwoFactor);

/// <summary>An invitation that has been sent and not yet used.</summary>
/// <param name="InvitationId">The invitation.</param>
/// <param name="Email">Where it was sent. It is valid only for this address.</param>
/// <param name="Role">The role the person will hold once they accept.</param>
/// <param name="InvitedByName">Who sent it.</param>
/// <param name="SentAt">When it was sent.</param>
/// <param name="ExpiresAt">When it stops working.</param>
public sealed record PendingInvitation(
    Guid InvitationId,
    string Email,
    Role Role,
    string InvitedByName,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt);

/// <summary>What somebody arriving on an invitation link is shown before they accept.</summary>
/// <param name="Email">The address it was sent to.</param>
/// <param name="OrganizationName">The organization they are being invited to.</param>
/// <param name="Role">The role they are being offered.</param>
/// <param name="InvitedByName">Who invited them.</param>
/// <param name="NeedsAccount">
/// Whether this address has no account yet, and so must choose a name and a password before the
/// invitation can be accepted.
/// </param>
public sealed record InvitationDetails(
    string Email,
    string OrganizationName,
    Role Role,
    string InvitedByName,
    bool NeedsAccount);

/// <summary>An organization the signed-in person belongs to, for the switcher.</summary>
/// <param name="OrganizationId">The organization.</param>
/// <param name="Name">Its name.</param>
/// <param name="Slug">Its short name.</param>
/// <param name="Role">The role this person holds there.</param>
public sealed record OrganizationSummary(Guid OrganizationId, string Name, string Slug, Role Role);

/// <summary>One line of the audit log.</summary>
/// <param name="Id">The entry.</param>
/// <param name="At">When it happened.</param>
/// <param name="ActorName">Who did it, or the address they tried to sign in with.</param>
/// <param name="Action">What happened, as a stable name such as <c>member.role_changed</c>.</param>
/// <param name="SubjectType">The kind of object it happened to.</param>
/// <param name="SubjectName">A readable name for that object.</param>
/// <param name="IpAddress">Where the request came from.</param>
/// <param name="Before">The values before, as JSON.</param>
/// <param name="After">The values after, as JSON.</param>
public sealed record AuditRecord(
    Guid Id,
    DateTimeOffset At,
    string? ActorName,
    string Action,
    string SubjectType,
    string? SubjectName,
    string? IpAddress,
    string? Before,
    string? After);
