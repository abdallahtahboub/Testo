using Microsoft.AspNetCore.Components;

namespace Testo.UI.Shared.Theming;

/// <summary>
/// The visitor's theme choice for the current request or interactive session.
/// </summary>
/// <remarks>
/// The host fills it from the theme cookie while the page is rendered on the server. The
/// value then travels with the prerendered page into the interactive session, where no
/// HTTP request (and therefore no cookie) is available.
/// </remarks>
public sealed class ThemeState
{
    /// <summary>The chosen theme.</summary>
    [PersistentState]
    public ThemeChoice Choice { get; set; }
}
