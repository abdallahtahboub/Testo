namespace Autestiq.Contracts.Planning;

/// <summary>
/// The containers a target's "within" may name (ADR-0024, ADR-0042): the row, item, form or section an element sits in,
/// a landmark, a dialog, or a region with a name of its own. One list, which the worker, the explorer and the exported
/// tests all read, so that a scope a plan is told to use is the scope every one of them looks in.
/// </summary>
/// <remarks>
/// Each is a part of a page a person would point at: "the Edit link in Jeff Black's row", "the Close button in the
/// dialog", "the Login link in the navigation". A bare <c>div</c> is not one: it says nothing about what it holds, and
/// allowing it would make every page one container deep.
/// </remarks>
public static class TargetScopes
{
    /// <summary>The elements that are containers by what they are.</summary>
    public static IReadOnlyList<string> Elements { get; } =
        ["tr", "li", "article", "section", "form", "fieldset", "dl", "p", "main", "nav", "aside", "header", "footer", "dialog"];

    /// <summary>The roles that make any element a container: a dialog, a region, a tab's panel, a menu, a group.</summary>
    public static IReadOnlyList<string> Roles { get; } = ["dialog", "alertdialog", "region", "tabpanel", "menu", "group"];

    /// <summary>The containers as a CSS selector list, for the browser and for exported tests.</summary>
    public static string Selector { get; } =
        string.Join(", ", Elements.Concat(Roles.Select(role => $"[role={role}]")));
}
