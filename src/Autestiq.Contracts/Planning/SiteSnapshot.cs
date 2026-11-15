namespace Autestiq.Contracts.Planning;

/// <summary>
/// One element a plan could act on: what it is, what it says, and the test id it has if it has one.
/// </summary>
/// <remarks>
/// Four fields, and each one is attack surface, because all of this goes into a prompt
/// (ADR-0019 §5). Nothing here is markup, an attribute, a script or a URL.
/// </remarks>
/// <param name="Role">Its ARIA role: <c>button</c>, <c>link</c>, <c>textbox</c>, <c>heading</c>.</param>
/// <param name="Name">Its accessible name — what a person would call it.</param>
/// <param name="TestId">Its <c>data-testid</c>, when it has one. The stable way to name it.</param>
/// <param name="Text">What it reads, trimmed, when that is not already its name.</param>
/// <param name="Within">
/// When other elements on the page share its role and name, the text of the row, item, form or section it
/// sits in, which is how a target tells it apart (ADR-0024). Absent when the element is unique.
/// </param>
/// <param name="NamedBy">
/// How a plan should name it: the first of test id, role and name, or exact text, that the browser confirmed
/// finds this element and no other, with <see cref="Within"/> when that was needed. Null when nothing does,
/// and then a plan cannot name it reliably (ADR-0024).
/// </param>
public sealed record SnapshotElement(
    string Role,
    string Name,
    string? TestId = null,
    string? Text = null,
    string? Within = null,
    TargetKind? NamedBy = null);

/// <summary>One page as a deterministic crawl found it.</summary>
/// <param name="Path">The path within the origin, such as <c>/basket</c>.</param>
/// <param name="Title">The page's title.</param>
/// <param name="Heading">Its one <c>h1</c>, when it has one.</param>
/// <param name="Elements">What is on it that a plan could act on or check.</param>
/// <param name="ReachedBy">
/// The actions that led here from the start page, when the crawl acted on the application rather
/// than only reading it: for example <c>click add-pot</c>, then <c>click to-catalogue</c>. Empty for
/// a page the crawl reached by following a link.
/// </param>
public sealed record PageSnapshot(
    string Path,
    string Title,
    string? Heading,
    IReadOnlyList<SnapshotElement> Elements,
    IReadOnlyList<string>? ReachedBy = null);

/// <summary>
/// The application as a crawl found it: the pages that were reachable, and what is on each.
/// </summary>
/// <remarks>
/// <para>
/// Collected by walking in-origin links from the start page, within a budget, with no model
/// involved. That matters twice: the input to planning is reproducible, so the same scenario plans
/// the same way; and the model never chooses where to look, so it cannot be led somewhere by the
/// page it is reading.
/// </para>
/// <para>
/// <strong>This is untrusted input.</strong> Every string in here was written by whoever wrote the
/// application under test, and goes into a prompt on purpose. Treat it as data everywhere.
/// </para>
/// </remarks>
/// <param name="Origin">Where the crawl started: the run's own target.</param>
/// <param name="Pages">The pages found, in the order they were visited.</param>
/// <param name="Truncated">
/// Whether the crawl stopped at its budget rather than running out of pages, so a planner knows
/// that what it is looking at is not everything there is.
/// </param>
public sealed record SiteSnapshot(string Origin, IReadOnlyList<PageSnapshot> Pages, bool Truncated)
{
    /// <summary>Nothing was found, or nothing was looked for.</summary>
    public static SiteSnapshot Empty(string origin) => new(origin, [], false);

    /// <summary>Whether a page offers this target, which is what the validator asks.</summary>
    public bool Offers(ActionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Pages.Any(page => page.Elements.Any(element => Matches(element, target)));
    }

    /// <summary>Whether the crawl saw this path.</summary>
    public bool HasPath(string path) =>
        Pages.Any(page => string.Equals(page.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every test id the crawl saw, for telling a planner what it may name.</summary>
    public IReadOnlyList<string> TestIds =>
    [
        .. Pages
            .SelectMany(page => page.Elements)
            .Select(element => element.TestId)
            .Where(testId => testId is { Length: > 0 })
            .Select(testId => testId!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static bool Matches(SnapshotElement element, ActionTarget target) => target.Kind switch
    {
        TargetKind.TestId => string.Equals(element.TestId, target.Value, StringComparison.Ordinal),

        // A name is what a person reads, so it is compared the way a person would.
        TargetKind.RoleAndName =>
            string.Equals(element.Role, target.Role, StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.Name, target.Value, StringComparison.OrdinalIgnoreCase)
            && WithinMatches(element, target),

        TargetKind.Text =>
            string.Equals(element.Text ?? element.Name, target.Value, StringComparison.Ordinal)
            && WithinMatches(element, target),

        _ => false,
    };

    private static bool WithinMatches(SnapshotElement element, ActionTarget target) =>
        target.Within is not { Length: > 0 } within
        || string.Equals(element.Within, within, StringComparison.OrdinalIgnoreCase);
}
