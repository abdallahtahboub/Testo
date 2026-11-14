using Microsoft.Extensions.Localization;
using Testo.Platform.Localization;

namespace Testo.UI.Shared.Localization;

/// <summary>
/// Looks up a <see cref="LocalizedText"/> — a resource key that lives in somebody else's resource
/// file — in the language the visitor is reading.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets the chrome render text it does not own. The site header draws a menu
/// registered by <c>Testo.Website</c>, and the app shell draws navigation registered by the
/// modules; in both cases the label names the resource assembly it came from, and the design
/// system holds none of that copy.
/// </para>
/// <para>
/// A key with no translation comes back as the key itself, which is how
/// <see cref="IStringLocalizer"/> behaves and is better than an empty menu: a visitor sees
/// something odd and a test catches it, where a blank would pass both.
/// </para>
/// </remarks>
public sealed class LocalizedTextLookup(IStringLocalizerFactory factory)
{
    /// <summary>The text, in the current language.</summary>
    public string this[LocalizedText text] => Resolve(text);

    /// <summary>The text, in the current language.</summary>
    public string Resolve(LocalizedText text) =>
        factory.Create(text.ResourceType)[text.Key];

    /// <summary>The text, or <c>null</c> when there is no text to show.</summary>
    public string? Resolve(LocalizedText? text) => text is { } value ? Resolve(value) : null;
}
