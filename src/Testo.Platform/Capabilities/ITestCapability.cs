using Testo.Platform.Localization;

namespace Testo.Platform.Capabilities;

/// <summary>
/// A pluggable way of executing tests, for example "AI E2E web testing". A capability brings
/// its own UI for every place the platform shows it, so the execution pages and the
/// settings stay generic: adding a capability never changes an existing module.
/// </summary>
public interface ITestCapability
{
    /// <summary>Stable, lowercase identifier used in URLs, for example <c>e2e-web</c>.</summary>
    string Id { get; }

    /// <summary>The visible name.</summary>
    LocalizedText Name { get; }

    /// <summary>A one-sentence description.</summary>
    LocalizedText Description { get; }

    /// <summary>The name of an icon from the design system's icon set.</summary>
    string Icon { get; }

    /// <summary>The kind of testing the capability performs.</summary>
    CapabilityKind Kind { get; }

    /// <summary>
    /// The flag that switches the capability on, for example <c>Capability.E2EWeb</c>.
    /// <see langword="null"/> means it is always on.
    /// </summary>
    string? FeatureFlag { get; }

    /// <summary>Component shown under Settings → Capabilities.</summary>
    Type SettingsComponent { get; }

    /// <summary>Component shown when a user starts a run with this capability.</summary>
    Type RunSetupComponent { get; }

    /// <summary>Component that shows a run while it is in progress. Receives a <c>RunId</c> parameter.</summary>
    Type RunViewComponent { get; }

    /// <summary>Component that shows the results of a finished run. Receives a <c>RunId</c> parameter.</summary>
    Type ResultsComponent { get; }
}
