using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// Exploring an application that has no test ids (ADR-0024): the demo shop with them removed.
/// </summary>
/// <remarks>
/// In the collection that runs one at a time: the shop's defects are switched for the whole process, and this
/// copy shares them with the other.
/// </remarks>
[Collection(Serially.Name)]
public class WithoutTestIdsTests(ShopWithoutTestIdsFixture shop) : IClassFixture<ShopWithoutTestIdsFixture>
{
    private Task<WorkerResult> ExploredAsync() => Explorations.OnceAsync(shop.Origin, pressing: true, forms: true);

    [Fact]
    public async Task TheShopReallyHasNoTestIds()
    {
        // Without this the tests below could pass by reading test ids that were meant to be gone.
        var snapshot = Explorations.SnapshotOf(await ExploredAsync());

        Assert.NotEmpty(snapshot.Pages);
        Assert.DoesNotContain(Explorations.Elements(snapshot), element => element.TestId is not null);
    }

    [Fact]
    public async Task Prices_AreDescribed_AsText()
    {
        var snapshot = Explorations.SnapshotOf(await ExploredAsync());

        Assert.Contains(Explorations.Elements(snapshot), element => element.Role == "text" && element.Text == "£42.00");
    }

    [Fact]
    public async Task ButtonsThatSayTheSameThing_AreToldApartByTheItemTheyAreIn()
    {
        var catalogue = Explorations.SnapshotOf(await ExploredAsync()).Pages.First(page => page.Path == "/");

        var adds = catalogue.Elements.Where(element => element.Role == "button" && element.Name == "Add to basket").ToList();

        Assert.Equal(4, adds.Count);
        Assert.All(adds, element => Assert.False(string.IsNullOrEmpty(element.Within)));
        Assert.Equal(4, adds.Select(element => element.Within).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(adds, element => element.Within!.StartsWith("Cast iron teapot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pressing_ReachesTheBasket_ByTheButtonInTheTeapotsItem()
    {
        var snapshot = Explorations.SnapshotOf(await ExploredAsync());

        var basket = snapshot.Pages.FirstOrDefault(page =>
            page.Path == "/basket"
            && page.ReachedBy is ["open /", var press] && press.Contains("within \"Cast iron teapot", StringComparison.Ordinal));

        Assert.NotNull(basket);
        Assert.Contains(basket.Elements, element => element.Text == "Cast iron teapot");
    }

    [Fact]
    public async Task Pressing_StillNeverPlacesAnOrder()
    {
        // Without a test id the rule is read from the control's words, and "Place the order" says it.
        var snapshot = Explorations.SnapshotOf(await ExploredAsync());

        Assert.DoesNotContain(snapshot.Pages, page =>
            page.ReachedBy is { } steps && steps.Any(step => step.Contains("Place the order", StringComparison.Ordinal)));
        Assert.DoesNotContain(snapshot.Pages, page => page.Path.StartsWith("/order/placed", StringComparison.Ordinal) && page.ReachedBy is { Count: > 0 });
    }

    [Fact]
    public async Task Exploring_IsTheSameOnEveryRun()
    {
        var first = Explorations.SnapshotOf(await ExploredAsync());
        var second = Explorations.SnapshotOf(await Jobs.ExploreAsync(shop.Origin, Explorations.Budget, exploreStates: true, submitForms: true));

        Assert.Equal(Explorations.Text(first), Explorations.Text(second));
    }
}
