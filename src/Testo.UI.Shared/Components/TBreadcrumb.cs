namespace Testo.UI.Shared.Components;

/// <summary>One step of a breadcrumb trail.</summary>
/// <param name="Label">The visible text.</param>
/// <param name="Href">Where it leads. The current page has none.</param>
public sealed record TBreadcrumb(string Label, string? Href = null);
