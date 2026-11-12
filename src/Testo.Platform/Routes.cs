namespace Testo.Platform;

/// <summary>
/// URLs that more than one project needs. Modules link to each other through these
/// constants, never by referencing each other's page types.
/// </summary>
public static class Routes
{
    /// <summary>Root of the product app. Everything below it is interactive.</summary>
    public const string App = "/app";

    /// <summary>The page shown for unknown URLs.</summary>
    public const string NotFound = "/not-found";

    /// <summary>The page shown after an unexpected server error.</summary>
    public const string Error = "/error";

    /// <summary>Form target that stores the visitor's theme choice.</summary>
    public const string ThemePreference = "/preferences/theme";

    /// <summary>Form target that stores the visitor's language choice.</summary>
    public const string CulturePreference = "/preferences/culture";
}
