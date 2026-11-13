namespace Testo.UI.Shared.Components;

/// <summary>One option of a <c>TRadioGroup</c>.</summary>
/// <typeparam name="TValue">The type of the value behind the option.</typeparam>
/// <param name="Value">What choosing this option means.</param>
/// <param name="Label">The visible text.</param>
/// <param name="Hint">What this option does, when the label alone is not enough.</param>
/// <param name="Disabled">Whether this one option cannot be chosen.</param>
public sealed record TRadioOption<TValue>(TValue Value, string Label, string? Hint = null, bool Disabled = false);
