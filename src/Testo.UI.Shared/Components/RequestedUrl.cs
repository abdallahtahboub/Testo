namespace Testo.UI.Shared.Components;

/// <summary>
/// The address the visitor asked for, when the server shows a different page in its place
/// (the not-found page or the error page).
/// </summary>
/// <remarks>
/// In that case the framework reports the address of the substitute page as the current
/// one. The host cascades this value so that links and forms still point at the address
/// in the visitor's address bar.
/// </remarks>
/// <param name="Value">The address relative to the site root, with query string, for example <c>/de/pricing?plan=team</c>.</param>
public sealed record RequestedUrl(string Value);
