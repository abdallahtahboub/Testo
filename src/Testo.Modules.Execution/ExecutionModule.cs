using Testo.Platform.Modules;

namespace Testo.Modules.Execution;

/// <summary>
/// Journey stages 5, 6 and 10: suites and environments, test runs, and test health. The pages arrive in Milestone 5.4.
/// </summary>
public sealed class ExecutionModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "execution";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Execution";
}
