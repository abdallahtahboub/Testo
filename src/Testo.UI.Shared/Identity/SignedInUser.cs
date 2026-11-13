using Microsoft.AspNetCore.Components;
using Testo.Contracts.Identity;

namespace Testo.UI.Shared.Identity;

/// <summary>
/// Who is signed in, for the current request or interactive session.
/// </summary>
/// <remarks>
/// The host fills this from the session cookie while the page is rendered on the server. The
/// value then travels with the prerendered page into the interactive session, where there is no
/// HTTP request and therefore no cookie — the same route the theme takes
/// (see <see cref="Theming.ThemeState"/>).
/// </remarks>
public sealed class SignedInUser
{
    /// <summary>The person, or <c>null</c> when nobody is signed in.</summary>
    [PersistentState]
    public Guid? UserId { get; set; }

    /// <summary>Their address.</summary>
    [PersistentState]
    public string? Email { get; set; }

    /// <summary>The name shown next to their work.</summary>
    [PersistentState]
    public string? DisplayName { get; set; }

    /// <summary>The organization this session is acting in.</summary>
    [PersistentState]
    public Guid? OrganizationId { get; set; }

    /// <summary>Its name, for the top bar.</summary>
    [PersistentState]
    public string? OrganizationName { get; set; }

    /// <summary>What they may do there.</summary>
    [PersistentState]
    public Role? Role { get; set; }
}
