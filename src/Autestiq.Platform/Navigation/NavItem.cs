using Autestiq.Contracts.Identity;
using Autestiq.Platform.Localization;

namespace Autestiq.Platform.Navigation;

/// <summary>One entry in the app navigation, contributed by a module.</summary>
/// <param name="Id">Stable identifier, unique across all modules, for example <c>defects</c>.</param>
/// <param name="Label">The visible label, taken from the module's resources.</param>
/// <param name="Href">The target URL.</param>
/// <param name="Icon">The name of an icon from the design system's icon set.</param>
/// <param name="Group">The navigation section the entry belongs to.</param>
/// <param name="Order">Position inside the section; lower comes first.</param>
/// <param name="FeatureFlag">Optional flag that hides the entry while it is off.</param>
/// <param name="RequiredPermission">
/// Optional permission a person needs to see the entry. Hiding it spares a person a link that would be refused; the
/// page and its services refuse all the same.
/// </param>
public sealed record NavItem(
    string Id,
    LocalizedText Label,
    string Href,
    string Icon,
    NavGroup Group,
    int Order = 0,
    string? FeatureFlag = null,
    Permission? RequiredPermission = null);
