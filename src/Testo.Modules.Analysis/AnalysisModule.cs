using Testo.Platform.Modules;

namespace Testo.Modules.Analysis;

/// <summary>
/// Journey stage 3, test analysis: requirements, test conditions and traceability. The pages arrive in Milestone 5.2.
/// </summary>
public sealed class AnalysisModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "analysis";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Analysis";
}
