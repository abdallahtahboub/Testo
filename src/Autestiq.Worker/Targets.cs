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
// Public so that how a target becomes an element is tested against small fixed pages, not only through runs.
public static class Targets
{
    /// <summary>What can hold a repeated control or value: the containers "within" may name. Ours, and fixed.</summary>
    internal static readonly string Containers = TargetScopes.Selector;

    /// <summary>The element a target names, on this page.</summary>
    public static ILocator Locate(IPage page, ActionTarget target) => Locate(page, target, exactName: true);

    /// <summary>
    /// The element a plan's step names. The exact name first; a name a person or a model shortened ("Basket" for
    /// the link "Basket (2)") is matched loosely only when nothing has the exact name, after a short wait for the
    /// page to show it. What the explorer offers a plan is always checked with exact names (<see cref="Locate(IPage, ActionTarget)"/>).
    /// </summary>
    public static async Task<ILocator> LocateForStepAsync(IPage page, ActionTarget target)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(target);

        var exact = Locate(page, target);

        if (target.Kind != TargetKind.RoleAndName)
        {
            return exact;
        }

        try
        {
            await exact.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = ExactNameWaitMilliseconds })
                .ConfigureAwait(false);
            return exact;
        }
        catch (TimeoutException)
        {
            return Locate(page, target, exactName: false);
        }
    }

    /// <summary>How long a step waits for its exact name before it settles for a looser one.</summary>
    private const float ExactNameWaitMilliseconds = 2_000;

    private static ILocator Locate(IPage page, ActionTarget target, bool exactName)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(target);

        // A role's name is matched exactly, whitespace aside: "Password" is not "Confirm password". The names a
        // plan is shown are the browser's own accessible names, so an exact match finds the element meant.
        if (target.Within is not { Length: > 0 } within)
        {
            return target.Kind switch
            {
                TargetKind.TestId => page.GetByTestId(target.Value),
                TargetKind.RoleAndName => page.GetByRole(Role(target.Role), new PageGetByRoleOptions { Name = target.Value, Exact = exactName }),
                TargetKind.Text => page.GetByText(target.Value, new PageGetByTextOptions { Exact = true }),
                TargetKind.Label => page.GetByLabel(target.Value, new PageGetByLabelOptions { Exact = true }),
                _ => throw Unknown(),
            };
        }

        // The innermost container that holds both the text and the element: it comes last in document order,
        // after any it is nested in. Holding the text alone is not enough, because the innermost container
        // with a product's name can be the cell that shows only the name, and the price is in the next one.
        var holder = page.Locator(Containers)
            .Filter(new LocatorFilterOptions { HasText = within, Has = Locate(page, target with { Within = null }, exactName) })
            .Last;

        return target.Kind switch
        {
            TargetKind.TestId => holder.GetByTestId(target.Value),
            TargetKind.RoleAndName => holder.GetByRole(Role(target.Role), new LocatorGetByRoleOptions { Name = target.Value, Exact = exactName }),
            TargetKind.Text => holder.GetByText(target.Value, new LocatorGetByTextOptions { Exact = true }),
            TargetKind.Label => holder.GetByLabel(target.Value, new LocatorGetByLabelOptions { Exact = true }),
            _ => throw Unknown(),
        };
    }

    private static PlaywrightException Unknown() => new("A target is named in a way this worker does not know.");

    /// <summary>
    /// The roles a target may name. A closed list again: an unknown role is a refusal, not a guess.
    /// </summary>
    public static AriaRole Role(string? role) => role?.ToLowerInvariant() switch
    {
        "button" => AriaRole.Button,
        "link" => AriaRole.Link,
        "textbox" => AriaRole.Textbox,
        "searchbox" => AriaRole.Searchbox,
        "checkbox" => AriaRole.Checkbox,
        "radio" => AriaRole.Radio,
        "combobox" => AriaRole.Combobox,
        "heading" => AriaRole.Heading,
        "menuitem" => AriaRole.Menuitem,
        "tab" => AriaRole.Tab,
        _ => throw new PlaywrightException($"'{role}' is not a role a target may name."),
    };

    /// <summary>The roles <see cref="Role"/> accepts.</summary>
    public static IReadOnlyList<string> NameableRoles { get; } =
        ["button", "link", "textbox", "searchbox", "checkbox", "radio", "combobox", "heading", "menuitem", "tab"];
}
