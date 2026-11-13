namespace Testo.UI.Shared.Components;

/// <summary>The shape a skeleton stands in for.</summary>
public enum SkeletonShape
{
    /// <summary>A line of body text. Several of these make a paragraph.</summary>
    Text,

    /// <summary>A heading: taller, and not full width.</summary>
    Heading,

    /// <summary>A rectangle: a card, a chart, an image.</summary>
    Block,

    /// <summary>A circle: an avatar.</summary>
    Circle,
}
