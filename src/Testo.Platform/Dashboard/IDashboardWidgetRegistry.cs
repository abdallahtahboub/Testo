namespace Testo.Platform.Dashboard;

/// <summary>Collects the dashboard widgets of all modules.</summary>
public interface IDashboardWidgetRegistry
{
    /// <summary>Adds a widget.</summary>
    void Add(DashboardWidget widget);

    /// <summary>All registered widgets.</summary>
    IReadOnlyList<DashboardWidget> Widgets { get; }
}
