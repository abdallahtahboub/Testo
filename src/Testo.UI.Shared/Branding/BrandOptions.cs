namespace Testo.UI.Shared.Branding;

/// <summary>
/// Brand settings, bound from the <c>Brand</c> configuration section. The product name is a
/// working name: UI text always takes it from here and never hard-codes it.
/// </summary>
public sealed class BrandOptions
{
    /// <summary>Name of the configuration section these options are bound from.</summary>
    public const string SectionName = "Brand";

    /// <summary>The product name shown in the UI.</summary>
    public string ProductName { get; set; } = "Testo";
}
