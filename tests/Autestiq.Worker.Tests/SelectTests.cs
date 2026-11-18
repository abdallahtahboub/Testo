using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>Choosing from a list: the select action, and what the crawl tells a plan about a list.</summary>
public class SelectTests(ListPageFixture pages) : IClassFixture<ListPageFixture>
{
    [Fact]
    public async Task Select_ChoosesTheOptionByItsLabel()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Select, "Choose the .NET brand", ActionTarget.ByRole("combobox", "Brand"), Text: ".NET"),
            new(ActionKind.Click, "Show it", ActionTarget.ByRole("button", "Show")),
            new(ActionKind.AssertVisible, "The choice arrived", ActionTarget.ByText("You chose .NET")),
        ];

        var result = await Jobs.PlannedAsync(pages.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task Select_AnOptionTheListDoesNotHave_FailsTheStep()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Select, "Choose a brand that is not offered", ActionTarget.ByRole("combobox", "Brand"), Text: "Java"),
            new(ActionKind.AssertUrl, "Still here", Path: "/"),
        ];

        var result = await Jobs.PlannedAsync(pages.Origin, plan);

        Assert.Equal(RunStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Exploring_DescribesTextInBareSectionsAndDivs_AndCanNameEachPieceExactly()
    {
        var snapshot = Explorations.SnapshotOf(await Jobs.ExploreAsync(pages.Origin));
        var basket = Assert.Single(snapshot.Pages, page => page.Path == "/basket");

        foreach (var text in new[] { "Roslyn Red Sheet", "$ 8.50", "Prism White TShirt", "$ 12.00", "Total: $ 20.50" })
        {
            var element = Assert.Single(basket.Elements, candidate => candidate.Role == "text" && candidate.Name == text);

            // Reached, not only seen: the browser found exactly one element for it (ADR-0024).
            Assert.NotNull(element.NamedBy);
        }
    }

    [Fact]
    public async Task Exploring_KeepsOnlyAFewPagesOfOneKind_ThoughEachSaysSomethingDifferent()
    {
        // Five "Add" buttons, each to a basket naming its product: one kind of page, five things said.
        var snapshot = Explorations.SnapshotOf(await Jobs.ExploreAsync(pages.Origin, Explorations.Budget, exploreStates: true));

        var baskets = snapshot.Pages.Count(page => page.Path.StartsWith("/added", StringComparison.Ordinal));

        Assert.InRange(baskets, 1, 3);
    }

    [Fact]
    public async Task Exploring_DescribesAListWithItsOptions_AndHowToNameIt()
    {
        var snapshot = Explorations.SnapshotOf(await Jobs.ExploreAsync(pages.Origin));

        var list = Assert.Single(Explorations.Elements(snapshot), element => element.Role == "combobox");
        Assert.Equal("Brand", list.Name);
        Assert.Equal("All | .NET | Azure", list.Text);
        Assert.Equal(TargetKind.RoleAndName, list.NamedBy);
    }
}
