using Testo.Platform.Localization;

namespace Testo.Platform.Navigation;

/// <summary>One entry in the app navigation, contributed by a module.</summary>
/// <param name="Id">Stable identifier, unique across all modules, for example <c>defects</c>.</param>
/// <param name="Label">The visible label, taken from the module's resources.</param>
/// <param name="Href">The target URL.</param>
/// <param name="Icon">The name of an icon from the design system's icon set.</param>
/// <param name="Group">The navigation section the entry belongs to.</param>
/// <param name="Order">Position inside the section; lower comes first.</param>
/// <param name="FeatureFlag">Optional flag that hides the entry while it is off.</param>
/// <param name="RequiredRole">Optional role a user needs to see the entry.</param>
public sealed record NavItem(
    string Id,
    LocalizedText Label,
    string Href,
    string Icon,
    NavGroup Group,
    int Order = 0,
    string? FeatureFlag = null,
    string? RequiredRole = null);
