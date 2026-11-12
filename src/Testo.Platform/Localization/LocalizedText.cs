namespace Testo.Platform.Localization;

/// <summary>
/// A text that lives in a module's own resource file. The shell looks it up in the user's
/// language at the moment it is shown, so modules keep their translations to themselves.
/// </summary>
/// <param name="ResourceType">The resource marker class of the module (the one used with <c>IStringLocalizer&lt;T&gt;</c>).</param>
/// <param name="Key">The resource key, for example <c>Nav_Defects</c>.</param>
public readonly record struct LocalizedText(Type ResourceType, string Key)
{
    /// <summary>Creates a text from a resource marker class and a key.</summary>
    public static LocalizedText From<TResources>(string key) => new(typeof(TResources), key);
}
