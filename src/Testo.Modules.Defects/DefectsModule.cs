using Testo.Platform.Modules;

namespace Testo.Modules.Defects;

/// <summary>
/// Journey stage 7, defect management: logging, triage, lifecycle and links. The pages arrive in Milestone 5.5.
/// </summary>
public sealed class DefectsModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "defects";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Defects";
}
