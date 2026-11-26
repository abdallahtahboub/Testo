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

        // Shown only to people who may read the log, which is also all the service gives anyone else: nothing.
        navigation.Add(new NavItem(
            "audit",
            LocalizedText.From<SettingsResources>("Nav_Audit"),
            Routes.AuditLog,
            "Document",
            NavGroup.Settings,
            Order: 20,
            FeatureFlag: FeatureFlag,
            RequiredPermission: Permission.ReadAuditLog));

        // Everyone who may read the organization may see what it has used: the service's own rule (Permission.Read).
        navigation.Add(new NavItem(
            "usage",
            LocalizedText.From<SettingsResources>("Nav_Usage"),
            Routes.Usage,
            "Clock",
            NavGroup.Settings,
            Order: 30,
            FeatureFlag: FeatureFlag,
            RequiredPermission: Permission.Read));
    }
}
