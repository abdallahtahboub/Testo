using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Resources;
using Autestiq.Platform;
using Autestiq.Platform.Localization;
using Autestiq.Platform.Modules;
using Autestiq.Platform.Navigation;

namespace Autestiq.Modules.Settings;

/// <summary>
/// The organization's administration (Enterprise programme E1): its members and invitations, and in later parts its
/// audit log, its usage and its settings.
/// </summary>
public sealed class SettingsModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "settings";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Settings";

    /// <inheritdoc />
    public void RegisterNavigation(INavigationRegistry navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        // Shown only to people who manage people: the page and its services refuse everyone else as well.
        navigation.Add(new NavItem(
            "members",
            LocalizedText.From<SettingsResources>("Nav_Members"),
            Routes.Members,
            "Person",
            NavGroup.Settings,
            Order: 10,
            FeatureFlag: FeatureFlag,
            RequiredPermission: Permission.ManagePeople));
    }
}
