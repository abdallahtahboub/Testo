namespace Testo.UI.Shared.Components;

/// <summary>How tall a <c>TSegmentedBar</c> is drawn, which decides whether it can carry text.</summary>
public enum SegmentedBarSize
{
    /// <summary>8px, no text. Beside a list or a table whose rows already give the figures.</summary>
    Thin,

    /// <summary>28px. Alone in a card, each part labelled with its share.</summary>
    Tall,
}
