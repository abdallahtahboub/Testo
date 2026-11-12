using System.Diagnostics.CodeAnalysis;

namespace Testo.Platform.Localization;

/// <summary>
/// Helpers for the URLs of the public website, where the language is the first path
/// segment: <c>/en/pricing</c>, <c>/de/pricing</c> (ADR-0006).
/// </summary>
public static class LocalizedPath
{
    /// <summary>Reads the language from a public URL: <c>/de/pricing</c> gives <c>de</c>.</summary>
    /// <returns><see langword="false"/> when the URL does not start with a supported language.</returns>
    public static bool TryGetCulture(string? path, [NotNullWhen(true)] out string? culture)
    {
        culture = null;
        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            return false;
        }

        var end = path.AsSpan(1).IndexOfAny('/', '?', '#');
        var segment = end < 0 ? path.AsSpan(1) : path.AsSpan(1, end);

        foreach (var supported in SupportedCultures.All)
        {
            if (segment.Equals(supported, StringComparison.OrdinalIgnoreCase))
            {
                culture = supported;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the URL of a public page in a language: <c>("de", "pricing")</c> gives
    /// <c>/de/pricing</c>, and <c>("de")</c> gives the home page <c>/de</c>.
    /// </summary>
    public static string For(string culture, string? pagePath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(culture);

        var trimmed = pagePath?.Trim('/');
        return string.IsNullOrEmpty(trimmed) ? $"/{culture}" : $"/{culture}/{trimmed}";
    }

    /// <summary>
    /// Returns the same public URL in another language: <c>("/en/pricing?plan=team", "de")</c>
    /// gives <c>/de/pricing?plan=team</c>. A URL without a language prefix (the app) is
    /// returned unchanged.
    /// </summary>
    public static string WithCulture(string url, string culture)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(culture);

        return TryGetCulture(url, out var current)
            ? string.Concat("/", culture, url.AsSpan(1 + current.Length))
            : url;
    }
}
