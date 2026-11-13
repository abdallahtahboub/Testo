namespace Testo.UI.Shared.Components;

/// <summary>One value in a chart.</summary>
/// <param name="Label">What it is. Always shown: a chart never relies on colour alone.</param>
/// <param name="Value">How much.</param>
/// <param name="Tone">
/// Its meaning, for a breakdown by status or severity. Left unset, every point takes the
/// same brand hue, which is what a comparison of like things wants.
/// </param>
public sealed record ChartPoint(string Label, double Value, Tone? Tone = null);
