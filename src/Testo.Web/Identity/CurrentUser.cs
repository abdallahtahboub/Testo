using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.UI.Shared.Identity;

namespace Testo.Web.Identity;

/// <summary>
/// Answers "who is asking" and "which organization" from the session the host established.
/// </summary>
/// <remarks>
/// Both interfaces are answered by one class because they are answered from one place: the
/// session. Reading <see cref="SignedInUser"/> rather than the HTTP context is what lets this
/// work in an interactive session as well, where there is no request to read
/// (<c>CLAUDE.md</c>, render modes).
/// </remarks>
internal sealed class CurrentUser(SignedInUser session) : ICurrentUser, IOrganizationContext
{
    /// <inheritdoc />
    public bool IsSignedIn => session.UserId is not null;

    /// <inheritdoc />
    public Guid? UserId => session.UserId;

    /// <inheritdoc />
    public string? Email => session.Email;

    /// <inheritdoc />
    public string? DisplayName => session.DisplayName;

    /// <inheritdoc />
    public Role? Role => session.Role;

    /// <inheritdoc />
    public Guid? OrganizationId => session.OrganizationId;

    /// <inheritdoc />
    public bool Can(Permission permission) => session.Role is { } role && Permissions.Allows(role, permission);

    /// <inheritdoc />
    public Guid RequireOrganizationId() =>
        session.OrganizationId
        ?? throw new InvalidOperationException(
            "This request is acting in no organization. Data inside the tenant boundary cannot be "
            + "read or written here; the caller is most likely missing an authorization check.");
}
