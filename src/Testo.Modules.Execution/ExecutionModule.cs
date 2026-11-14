using Testo.Modules.Execution.Resources;
using Testo.Platform;
using Testo.Platform.Localization;
using Testo.Platform.Modules;
using Testo.Platform.Navigation;

namespace Testo.Modules.Execution;

/// <summary>
/// Journey stages 5, 6 and 10: suites and environments, test runs, and test health.
/// </summary>
/// <remarks>
/// Milestone 3.3 built the part that actually runs something: the run list and one run with its
/// steps and evidence. Suites, environments and test health arrive with the rest of the module.
/// </remarks>
public sealed class ExecutionModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "execution";

    /// <inheritdoc />
    public string? FeatureFlag => "Module.Execution";

    /// <inheritdoc />
    public void RegisterNavigation(INavigationRegistry navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        navigation.Add(new NavItem(
            Id,
            LocalizedText.From<ExecutionResources>("Nav_Runs"),
            Routes.Runs,
            "Play",
            NavGroup.Run,
            Order: 10,
            FeatureFlag: FeatureFlag));
    }
}
