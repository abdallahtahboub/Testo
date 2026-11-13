namespace Testo.Domain.Common;

/// <summary>
/// Marks an entity as living inside the tenant boundary. Every such entity is filtered by the
/// current organization automatically, so the <c>WHERE</c> clause is never written by hand
/// (<c>docs/domain-model.md</c> §4, ADR-0012).
/// </summary>
/// <remarks>
/// <see cref="Identity.User"/> deliberately does not implement this: a person is one account that
/// may belong to several organizations. <see cref="Identity.Membership"/> is the join.
/// </remarks>
public interface IBelongsToOrganization
{
    /// <summary>
    /// The owning organization. Set from the request context when the row is created, and refused
    /// afterwards — an entity cannot be moved between organizations.
    /// </summary>
    Guid OrganizationId { get; set; }
}
