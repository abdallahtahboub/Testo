namespace Autestiq.Contracts.Planning;

/// <summary>
/// Everything a plan may ask a browser to do. Closed on purpose: a plan cannot express an action
/// that is not on this list, because there is no value it could carry that would mean one.
/// </summary>
/// <remarks>
/// The names are stored inside plans, so they are assigned explicitly and never reordered.
/// Adding one is a decision, not a convenience: each addition is a new thing a hostile page could
/// try to talk the planner into (<c>docs/decisions/0019-the-planner-boundary.md</c> §2).
/// </remarks>
public enum ActionKind
{
    /// <summary>Go to a path within the run's own target origin.</summary>
    Goto = 1,

    /// <summary>Click a target.</summary>
    Click = 2,

    /// <summary>Type text into a target.</summary>
    Type = 3,

    /// <summary>Press a named key, such as <c>Enter</c>.</summary>
    Press = 4,

    /// <summary>Wait until a target is on the page.</summary>
    WaitFor = 5,

    /// <summary>Check that a target reads exactly the expected text.</summary>
    AssertText = 6,

    /// <summary>Check that a target is visible.</summary>
    AssertVisible = 7,

    /// <summary>Check that the browser is on the expected path.</summary>
    AssertUrl = 8,

    /// <summary>
    /// Check that a target is <em>not</em> on the page, or is on it and not visible.
    /// </summary>
    /// <remarks>
    /// Added after the first measured benchmark run, in which the planner asked for it three times
    /// and said why each time: half of what a person wants checked is an absence. "The cup is gone
    /// from the basket", "there is no way to place an order", "the error is no longer shown".
    /// Without it a plan can only assert what is there, which cannot catch a defect whose symptom
    /// is that something stayed. ADR-0019 §2 records the decision; it adds no new capability, since
    /// the executor already has to locate the element either way.
    /// </remarks>
    AssertNotVisible = 9,

    /// <summary>Choose the option of a list (a <c>select</c>) whose visible label is exactly the text.</summary>
    /// <remarks>
    /// Added on 2026-11-16, after eShopOnWeb showed that a shop's brand and type filters could not be used by any
    /// plan. The option is named by its label, which the crawl reads from the page, never by a value.
    /// </remarks>
    Select = 10,
}

/// <summary>How a target is named. Two ways, both stable, neither of them a selector language.</summary>
/// <remarks>
/// A CSS selector or an XPath would be a small grammar we would then have to defend: attribute
/// injection, <c>:has()</c>, and markup that reads as code to whoever is looking. A test id or a
/// role with its accessible name says what a person means and nothing else.
/// </remarks>
public enum TargetKind
{
    /// <summary>The element whose <c>data-testid</c> is this.</summary>
    TestId = 1,

    /// <summary>The element with this ARIA role and this accessible name.</summary>
    RoleAndName = 2,

    /// <summary>
    /// The element whose visible text is exactly this. Matched as text by the browser, never read as a
    /// selector, so an application that has no test ids can still be checked (ADR-0024).
    /// </summary>
    Text = 3,

    /// <summary>
    /// The form field whose label is exactly this: the visible label tied to it, or its accessible label. How a person
    /// names a field, and the only way to name one whose role is not one a target may name, a number or a date (ADR-0042).
    /// </summary>
    Label = 4,
}

/// <summary>Which element on the page an action is about.</summary>
/// <param name="Kind">How the element is named.</param>
/// <param name="Value">The test id; the accessible name when <see cref="Role"/> is set; or the exact visible text.</param>
/// <param name="Role">The ARIA role, for <see cref="TargetKind.RoleAndName"/>.</param>
/// <param name="Within">
/// When several elements fit, the text of the row, list item, form, section, landmark, dialog or region the right
/// one is inside (<see cref="TargetScopes"/>):
/// "the Add to basket button within the item that says Cast iron teapot". Matched as text, never as a
/// selector (ADR-0024).
/// </param>
public sealed record ActionTarget(TargetKind Kind, string Value, string? Role = null, string? Within = null)
{
    /// <summary>A target named by the exact text it shows.</summary>
    public static ActionTarget ByText(string text) => new(TargetKind.Text, text);

    /// <summary>The same target, looked for only inside the nearest row, item, form or section that contains this text.</summary>
    public ActionTarget InsideOf(string within) => this with { Within = within };

    /// <summary>A target named by its test id.</summary>
    public static ActionTarget ByTestId(string testId) => new(TargetKind.TestId, testId);

    /// <summary>A form field named by its label.</summary>
    public static ActionTarget ByLabel(string label) => new(TargetKind.Label, label);

    /// <summary>A target named by what it is and what it says.</summary>
    public static ActionTarget ByRole(string role, string name) => new(TargetKind.RoleAndName, name, role);

    /// <summary>What to show a person reading the plan.</summary>
    public override string ToString() =>
        (Kind switch
        {
            TargetKind.TestId => $"#{Value}",
            TargetKind.Text => $"the text “{Value}”",
            TargetKind.Label => $"the field labelled “{Value}”",
            _ => $"{Role} “{Value}”",
        })
        + (Within is { Length: > 0 } within ? $" within “{within}”" : "");
}

/// <summary>
/// One action of a plan.
/// </summary>
/// <remarks>
/// There is deliberately no field for a script, an expression, a selector or a URL to another
/// origin. A plan that wanted one would have nowhere to put it.
/// </remarks>
/// <param name="Kind">What to do.</param>
/// <param name="Description">What this step is for, in a person's words. Shown on the run page.</param>
/// <param name="Target">Which element, for the actions that need one.</param>
/// <param name="Path">The path within the target origin, for <see cref="ActionKind.Goto"/> and <see cref="ActionKind.AssertUrl"/>.</param>
/// <param name="Text">The text to type, the key to press, or the text an assertion expects.</param>
/// <param name="SecretName">
/// The name of a value this run was given, to type instead of <paramref name="Text"/>. A plan
/// therefore never contains a password, which is why a stored plan is not a secret (ADR-0019 §4).
/// </param>
public sealed record PlannedAction(
    ActionKind Kind,
    string Description,
    ActionTarget? Target = null,
    string? Path = null,
    string? Text = null,
    string? SecretName = null)
{
    /// <summary>Whether this action checks something, as opposed to doing something.</summary>
    public bool IsAssertion =>
        Kind is ActionKind.AssertText or ActionKind.AssertVisible or ActionKind.AssertUrl
            or ActionKind.AssertNotVisible;
}
