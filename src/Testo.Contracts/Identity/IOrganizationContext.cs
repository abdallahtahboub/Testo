namespace Testo.Contracts.Identity;

/// <summary>
/// Which organization the current request is acting in. Everything inside the tenant boundary is
/// filtered by this, automatically rather than by hand (<c>docs/domain-model.md</c> §4).
/// </summary>
public interface IOrganizationContext
{
    /// <summary>
    /// The organization, or <c>null</c> on a public page and while somebody is still signing in.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// The organization, refusing when there is none. Data access uses this, so that a query that
    /// should have been scoped fails loudly instead of quietly reading across the boundary.
    /// </summary>
    /// <exception cref="InvalidOperationException">No organization is in context.</exception>
    Guid RequireOrganizationId();
}
