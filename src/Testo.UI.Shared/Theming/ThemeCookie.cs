namespace Testo.UI.Shared.Theming;

/// <summary>
/// The cookie that stores the visitor's theme choice. The server reads it to render the right
/// theme straight away (no flash); the theme script writes it when the visitor switches.
/// </summary>
public static class ThemeCookie
{
    /// <summary>The cookie name.</summary>
    public const string Name = "testo.theme";

    /// <summary>The choices in the order the theme switch shows them.</summary>
    public static IReadOnlyList<ThemeChoice> Choices { get; } = [ThemeChoice.Light, ThemeChoice.Dark, ThemeChoice.System];

    /// <summary>Reads a cookie or form value. Anything unknown or missing means <see cref="ThemeChoice.System"/>.</summary>
    public static ThemeChoice Parse(string? value)
    {
        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
        {
            return ThemeChoice.Light;
        }

        return string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase) ? ThemeChoice.Dark : ThemeChoice.System;
    }

    /// <summary>The value used in the cookie, in forms and in the <c>data-theme</c> attribute.</summary>
    public static string ToValue(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => "light",
        ThemeChoice.Dark => "dark",
        _ => "system",
    };
}
