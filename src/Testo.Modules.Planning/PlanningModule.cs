using Testo.Platform.Modules;

namespace Testo.Modules.Planning;

/// <summary>
/// Journey stage 1, test planning: test plans, risks, entry and exit criteria. The pages arrive in Milestone 5.1.
/// </summary>
public sealed class PlanningModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "planning";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Planning";
}
