using Testo.Platform.Modules;

namespace Testo.Modules.Settings;

/// <summary>
/// The settings area: integrations, team, capability settings and personal preferences. The pages arrive in Milestone 5.7.
/// </summary>
public sealed class SettingsModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "settings";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Settings";
}
