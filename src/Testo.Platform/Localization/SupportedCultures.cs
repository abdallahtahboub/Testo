using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Testo.Platform.Localization;

/// <summary>The languages the product ships in. English is the default and the neutral resource language.</summary>
public static class SupportedCultures
{
    /// <summary>The language used when nothing else is known.</summary>
    public const string Default = "en";

    /// <summary>All supported languages, default first.</summary>
    public static IReadOnlyList<string> All { get; } = [Default, "de"];

    /// <summary>Returns whether <paramref name="culture"/> is one of the supported languages.</summary>
    public static bool IsSupported([NotNullWhen(true)] string? culture) =>
        culture is not null && All.Contains(culture, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The supported language of the current request or circuit. A regional culture maps to
    /// its language ("de-AT" becomes "de"); anything unsupported maps to <see cref="Default"/>.
    /// </summary>
    public static string Current => Resolve(CultureInfo.CurrentUICulture);

    /// <summary>Maps any culture to the supported language that fits it best.</summary>
    public static string Resolve(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        for (var candidate = culture; !string.IsNullOrEmpty(candidate.Name); candidate = candidate.Parent)
        {
            var match = All.FirstOrDefault(supported => string.Equals(supported, candidate.Name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return Default;
    }

    /// <summary>The language's name in the language itself ("English", "Deutsch").</summary>
    public static string NativeName(string culture) => CultureInfo.GetCultureInfo(culture).NativeName;
}
