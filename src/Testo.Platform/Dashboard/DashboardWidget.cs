using Testo.Platform.Localization;

namespace Testo.Platform.Dashboard;

/// <summary>A dashboard widget contributed by a module. The widget loads its own data.</summary>
/// <param name="Id">Stable identifier, unique across all modules.</param>
/// <param name="Title">The visible title, taken from the module's resources.</param>
/// <param name="ComponentType">The Razor component that renders the widget.</param>
/// <param name="Size">How much room the widget asks for.</param>
/// <param name="Order">Position on the dashboard; lower comes first.</param>
/// <param name="FeatureFlag">Optional flag that hides the widget while it is off.</param>
public sealed record DashboardWidget(
    string Id,
    LocalizedText Title,
    Type ComponentType,
    WidgetSize Size = WidgetSize.Medium,
    int Order = 0,
    string? FeatureFlag = null);
