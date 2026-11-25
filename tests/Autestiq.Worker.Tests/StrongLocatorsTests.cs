using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// Stronger locators (ADR-0042): a field by its label, the menu and tab roles, and scopes that are landmarks, dialogs and
/// regions. Each is still chosen by the explorer only when the browser finds exactly one element for it.
/// </summary>
public class StrongLocatorsTests(StrongLocatorsFixture site) : IClassFixture<StrongLocatorsFixture>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<WorkerResult>>> Crawls = new();

    private async Task<IReadOnlyList<SnapshotElement>> ElementsAsync()
    {
        var result = await Crawls.GetOrAdd(site.Origin, origin => new Lazy<Task<WorkerResult>>(
            () => Jobs.ExploreAsync(origin, Explorations.Budget, cancellation: CancellationToken.None))).Value;

        return [.. Explorations.Elements(Explorations.SnapshotOf(result))];
    }

    private static SnapshotElement One(IReadOnlyList<SnapshotElement> elements, string role, string name) =>
        Assert.Single(elements, element => element.Role == role && element.Name == name);

    [Fact]
    public async Task AFieldWhoseRoleIsNotNameable_IsNamedByItsLabel()
    {
        // A number is a "spinbutton", which is not a role a target may name: its label is its name.
        Assert.Equal((TargetKind?)TargetKind.Label, One(await ElementsAsync(), "textbox", "Quantity").NamedBy);
    }

    [Fact]
    public async Task ADateField_WasNeverDescribed_AndNowIsReachable()
    {
        // Not among the fields the crawl looked for before ADR-0042; the browser gives it the textbox role.
        Assert.NotNull(One(await ElementsAsync(), "textbox", "Delivery date").NamedBy);
    }

    [Fact]
    public async Task APasswordField_IsReachable_ByTheRoleTheBrowserGivesIt()
    {
        // Playwright gives a password field the textbox role, so it was never out of reach; it was said to be, wrongly.
        var elements = await ElementsAsync();

        Assert.Equal(TargetKind.RoleAndName, One(elements, "textbox", "Password").NamedBy);
        Assert.Equal(TargetKind.RoleAndName, One(elements, "textbox", "Repeat password").NamedBy);
    }

    [Fact]
    public async Task AFieldWithARole_IsStillNamedByRoleFirst()
    {
        Assert.Equal(TargetKind.RoleAndName, One(await ElementsAsync(), "textbox", "Email").NamedBy);
    }

    [Fact]
    public async Task AMenusEntries_AndTabs_AreNamedByTheirRoles()
    {
        var elements = await ElementsAsync();

        Assert.Equal(TargetKind.RoleAndName, One(elements, "menuitem", "Settings").NamedBy);
        Assert.Equal(TargetKind.RoleAndName, One(elements, "tab", "History").NamedBy);
    }

    [Fact]
    public async Task TheSameControlTwice_IsToldApartByTheDialogItSitsIn()
    {
        var closes = (await ElementsAsync()).Where(element => element.Role == "button" && element.Name == "Close").ToList();

        Assert.Equal(2, closes.Count);

        // The dialog's Close is told apart by the dialog. The other, outside any container, is not given the dialog's
        // name, which would find the dialog's Close: it is named by nothing it does not own.
        Assert.Single(closes, close => close.NamedBy == TargetKind.RoleAndName && close.Within == "We use cookies");
    }

    [Fact]
    public async Task TheSameLinkInTheHeaderAndTheFooter_IsToldApartByTheLandmark()
    {
        var signIns = (await ElementsAsync()).Where(element => element.Role == "link" && element.Name == "Sign in").ToList();

        Assert.Equal(2, signIns.Count);
        Assert.All(signIns, link => Assert.NotNull(link.NamedBy));
        Assert.Contains(signIns, link => link.Within == "Orchard Ltd");
    }

    [Fact]
    public async Task APlan_TypesIntoAPasswordField_ByItsLabel()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the page", Path: "/"),
            new(ActionKind.Type, "Type a password", ActionTarget.ByLabel("Password"), Text: "Orchard-2026"),
            new(ActionKind.Type, "Type a quantity", ActionTarget.ByLabel("Quantity"), Text: "3"),
            new(ActionKind.AssertVisible, "The field is there", ActionTarget.ByLabel("Delivery date")),
            new(ActionKind.Click, "Choose the History tab", ActionTarget.ByRole("tab", "History")),
            new(ActionKind.Click, "Close the cookie notice", ActionTarget.ByRole("button", "Close").InsideOf("We use cookies")),
        ];

        var result = await Jobs.PlannedAsync(site.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task AFieldNamedByALabelNoFieldHas_FailsTheStep()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the page", Path: "/"),
            new(ActionKind.Type, "Type into a field that is not there", ActionTarget.ByLabel("Pass"), Text: "x"),
        ];

        Assert.Equal(RunStatus.Failed, (await Jobs.PlannedAsync(site.Origin, plan)).Status);
    }
}
