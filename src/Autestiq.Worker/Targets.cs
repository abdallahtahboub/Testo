using Microsoft.Playwright;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <summary>
/// How a target becomes an element in the browser: the one place that decides it, for planned runs and
/// for exploration alike.
/// </summary>
/// <remarks>
/// <para>
/// Every way of reaching an element is one Playwright expresses directly: by test id, by role and name, by
/// exact text. A value from a plan or a page is only ever handed to one of those as a value, never joined
/// into a selector, so nothing a model writes or a page says is read as markup (ADR-0019, ADR-0024).
/// </para>
/// <para>
/// "Within" narrows the search to the innermost row, list item, form or section that contains a text. The
/// list of containers is a fixed selector of ours; the text is matched as text.
/// </para>
/// </remarks>
internal static class Targets
{
    /// <summary>What can hold a repeated control or value: the containers "within" may name. Ours, and fixed.</summary>
    internal const string Containers = "tr, li, article, section, form, fieldset, dl";

    /// <summary>The element a target names, on this page.</summary>
    public static ILocator Locate(IPage page, ActionTarget target)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(target);

        // The innermost container with the text comes last in document order, after any it is nested in.
        var scope = target.Within is { Length: > 0 } within
            ? page.Locator(Containers).Filter(new LocatorFilterOptions { HasText = within }).Last
            : page.Locator(":root");

        return target.Kind switch
        {
            TargetKind.TestId => scope.GetByTestId(target.Value),
            TargetKind.RoleAndName => scope.GetByRole(Role(target.Role), new LocatorGetByRoleOptions { Name = target.Value, Exact = false }),
            TargetKind.Text => scope.GetByText(target.Value, new LocatorGetByTextOptions { Exact = true }),
            _ => throw new PlaywrightException("A target is named in a way this worker does not know."),
        };
    }

    /// <summary>
    /// The roles a target may name. A closed list again: an unknown role is a refusal, not a guess.
    /// </summary>
    public static AriaRole Role(string? role) => role?.ToLowerInvariant() switch
    {
        "button" => AriaRole.Button,
        "link" => AriaRole.Link,
        "textbox" => AriaRole.Textbox,
        "checkbox" => AriaRole.Checkbox,
        "radio" => AriaRole.Radio,
        "combobox" => AriaRole.Combobox,
        "heading" => AriaRole.Heading,
        _ => throw new PlaywrightException($"'{role}' is not a role a target may name."),
    };

    /// <summary>The roles <see cref="Role"/> accepts.</summary>
    public static IReadOnlyList<string> NameableRoles { get; } =
        ["button", "link", "textbox", "checkbox", "radio", "combobox", "heading"];
}
