using Testo.Domain.Common;

namespace Testo.Domain.Audit;

/// <summary>
/// One thing that happened: who, what, to which object, when, from where, and the values before
/// and after (<c>docs/identity-and-access.md</c> §4).
/// </summary>
/// <remarks>
/// Append-only. Not edited, not deleted, not by an owner either. A QA platform is asked to prove
/// who approved a release and who still had access in March, and the answer has to be better than
/// a shrug. Kept for two years.
/// </remarks>
public sealed class AuditEntry : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The person who acted, or <c>null</c> when the system itself did.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// The address the actor used, kept even for a failed sign-in where no account is known, so
    /// that "somebody tried to get in as me" has an answer.
    /// </summary>
    public string? ActorEmail { get; set; }

    /// <summary>What happened, as a stable name such as <c>member.role_changed</c>.</summary>
    public required string Action { get; set; }

    /// <summary>The kind of object it happened to, such as <c>Membership</c>.</summary>
    public required string SubjectType { get; set; }

    /// <summary>Which object, where there is one.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>A readable name for the object, so the log makes sense after the row is gone.</summary>
    public string? SubjectName { get; set; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The address the request came from.</summary>
    public string? IpAddress { get; set; }

    /// <summary>The values before the change, as JSON. <c>null</c> when the object was created.</summary>
    public string? Before { get; set; }

    /// <summary>The values after the change, as JSON. <c>null</c> when the object was destroyed.</summary>
    public string? After { get; set; }
}
