namespace Testo.Platform.Dashboard;

/// <summary>How much room a widget asks for. The dashboard grid turns this into columns.</summary>
public enum WidgetSize
{
    /// <summary>A single figure, such as a KPI tile.</summary>
    Small,

    /// <summary>A chart or a short list.</summary>
    Medium,

    /// <summary>A wide chart or table.</summary>
    Large,

    /// <summary>The full width of the dashboard.</summary>
    Full,
}
