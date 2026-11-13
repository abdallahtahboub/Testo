namespace Testo.UI.Shared.Components;

/// <summary>
/// What a form control needs from the field around it, so the two stay connected without
/// either having to invent an id.
/// </summary>
/// <param name="ControlId">The id the control must carry, which the label points at.</param>
/// <param name="DescribedBy">The ids of the hint and the error, for <c>aria-describedby</c>.</param>
/// <param name="IsInvalid">Whether the field is currently showing an error.</param>
public readonly record struct TFieldContext(string ControlId, string? DescribedBy, bool IsInvalid);
