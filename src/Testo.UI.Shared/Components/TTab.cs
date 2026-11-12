namespace Testo.UI.Shared.Components;

/// <summary>One tab of a <c>TTabs</c>.</summary>
/// <param name="Id">A stable identifier, used in the URL and as the panel's name.</param>
/// <param name="Label">The visible text.</param>
/// <param name="Icon">An optional icon before the text.</param>
/// <param name="Count">A count beside the label, such as the number of open defects.</param>
public sealed record TTab(string Id, string Label, IconName? Icon = null, int? Count = null);
