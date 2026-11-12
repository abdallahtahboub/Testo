using Testo.Platform.Modules;

namespace Testo.Modules.Reporting;

/// <summary>
/// Journey stages 2, 8 and 9: monitoring, reporting and release readiness, and test completion. The pages arrive in Milestone 5.6.
/// </summary>
public sealed class ReportingModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "reporting";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Reporting";
}
