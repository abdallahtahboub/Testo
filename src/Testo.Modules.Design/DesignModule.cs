using Testo.Platform.Modules;

namespace Testo.Modules.Design;

/// <summary>
/// Journey stage 4, test design: test cases, coverage techniques and test data. The pages arrive in Milestone 5.3.
/// </summary>
public sealed class DesignModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "design";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Design";
}
