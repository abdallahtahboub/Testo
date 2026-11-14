using Testo.Contracts.Execution;
using Testo.Contracts.Planning;
using Testo.Worker.Tests.Support;

namespace Testo.Worker.Tests;

/// <summary>
/// The crawl that describes an application before anything is planned against it. Deterministic on
/// purpose: a plan is only reproducible if its input is (ADR-0019 §5).
/// </summary>
public class ExploreTests(DemoShopFixture shop)
{
    [Fact]
    public async Task Explore_TheDemoShop_FindsItsPagesAndWhatIsOnThem()
    {
        var result = await Jobs.ExploreAsync(shop.Origin);

        Assert.Equal(RunStatus.Passed, result.Status);

        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);
        Assert.Contains("/", snapshot.Pages.Select(page => page.Path));
        Assert.Contains("/basket", snapshot.Pages.Select(page => page.Path));

        // The test ids a plan may name. The catalogue's add buttons are the ones a scenario needs.
        Assert.Contains("add-pot", snapshot.TestIds);
        Assert.Contains("add-cup", snapshot.TestIds);
    }

    [Fact]
    public async Task Explore_SeesThePricesAndNamesThatTheTestsAreAbout()
    {
        // The fault the first real benchmark exposed. The crawl collected only interactive roles
        // and headings, so every value these scenarios assert on — a price, a product name, a
        // basket total — was invisible, and the planner was asked to check a total against a price
        // while being shown four buttons that all read "Add to basket".
        var result = await Jobs.ExploreAsync(shop.Origin);
        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);

        Assert.Contains("name-pot", snapshot.TestIds);
        Assert.Contains("price-pot", snapshot.TestIds);

        var price = snapshot.Pages
            .SelectMany(page => page.Elements)
            .First(element => element.TestId == "price-pot");

        // And it reads what it says, because "what does it currently say" is most of what an
        // assertion needs to be written against.
        Assert.Contains("42", price.Text ?? price.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explore_ReachesThePagesHiddenBehindState()
    {
        // The checkout is behind a basket with something in it, so following links from an empty
        // basket never reaches it. Six of the first benchmark's twelve scenarios were about pages
        // the planner had never been shown. The shop publishes a sitemap, as a real one does.
        var result = await Jobs.ExploreAsync(shop.Origin);
        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);

        var paths = snapshot.Pages.Select(page => page.Path).ToList();

        Assert.Contains("/checkout", paths);
        Assert.Contains("/sign-in", paths);
        Assert.Contains("/order/placed", paths);
    }

    [Fact]
    public async Task Explore_TwiceOverTheSameApplication_DescribesItTheSameWay()
    {
        // The property the whole measurement rests on. If the description varied, the plan would
        // vary, and a flake rate would be measuring us rather than the application under test.
        var first = await Jobs.ExploreAsync(shop.Origin);
        var second = await Jobs.ExploreAsync(shop.Origin);

        Assert.Equal(Describe(first.Snapshot), Describe(second.Snapshot));
    }

    [Fact]
    public async Task Explore_WithAPageBudgetOfOne_StopsAndSaysItIsNotTheWholeApplication()
    {
        var result = await Jobs.ExploreAsync(shop.Origin, RunBudget.Default with { MaxPagesCrawled = 1 });

        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);

        Assert.Single(snapshot.Pages);
        Assert.True(snapshot.Truncated, "a planner must know it is looking at part of an application.");
    }

    [Fact]
    public async Task Explore_DescribesOnlyRolesNamesAndTestIds()
    {
        // Everything here goes into a prompt, so the fields are attack surface and the list stays
        // short. Nothing in a snapshot is markup, an attribute or an address.
        var result = await Jobs.ExploreAsync(shop.Origin);
        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);

        var elements = snapshot.Pages.SelectMany(page => page.Elements).ToList();

        Assert.NotEmpty(elements);
        Assert.All(elements, element =>
        {
            Assert.DoesNotContain("<", element.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("http", element.Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(element.Name.Length <= 120, $"'{element.Name}' was not trimmed.");
        });
    }

    [Fact]
    public async Task Explore_DoesNotFollowALinkOffTheOrigin()
    {
        // The shop links to example.com on purpose. The proxy would refuse it; the crawl should not
        // even ask.
        var result = await Jobs.ExploreAsync(shop.Origin);
        var snapshot = Assert.IsType<SiteSnapshot>(result.Snapshot);

        Assert.All(snapshot.Pages, page => Assert.StartsWith("/", page.Path, StringComparison.Ordinal));
    }

    /// <summary>The snapshot as comparable text, so two crawls can be held against each other.</summary>
    private static string Describe(SiteSnapshot? snapshot) => snapshot is null
        ? ""
        : string.Join(
            "\n",
            snapshot.Pages.Select(page =>
                $"{page.Path} | {page.Title} | {page.Heading} | "
                + string.Join(", ", page.Elements.Select(element => $"{element.Role}:{element.TestId}:{element.Name}"))));
}
