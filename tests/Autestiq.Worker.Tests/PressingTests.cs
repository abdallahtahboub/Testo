using Autestiq.Contracts.Execution;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// Pressing the demo shop's controls to find out what they lead to (ADR-0021), and typing fixed values
/// into its forms first (ADR-0022).
/// </summary>
/// <remarks>
/// In the collection that runs one at a time, because the shop's defects are switched for the whole
/// process: an exploration made while another test has a defect switched on describes a different shop.
/// </remarks>
[Collection(Serially.Name)]
public class PressingTests(DemoShopFixture shop)
{
    private Task<WorkerResult> PressedAsync() => Explorations.OnceAsync(shop.Origin, pressing: true, forms: false);

    private Task<WorkerResult> FilledAsync() => Explorations.OnceAsync(shop.Origin, pressing: true, forms: true);

    /// <summary>The shop's one account, as an environment's stored test account would hand it over (ADR-0033).</summary>
    private static readonly Dictionary<string, string> Account = new(StringComparer.Ordinal)
    {
        ["username"] = DemoShop.Shop.Account,
        ["password"] = DemoShop.Shop.Password,
    };

    private Task<WorkerResult> SignedInAsync() =>
        Explorations.OnceAsync(shop.Origin, pressing: true, forms: true, secrets: Account, secretsLabel: "account");

    [Fact]
    public async Task Pressing_ShowsWhereAControlLeads()
    {
        // The fault the first measured benchmark turned on: "add to basket" goes to the basket, and a plan
        // that does not know it presses the next button on a page it has already left.
        var snapshot = Explorations.SnapshotOf(await PressedAsync());

        var basket = snapshot.Pages.FirstOrDefault(page => page.ReachedBy is ["open /", "click add-pot"]);

        Assert.NotNull(basket);
        Assert.Equal("/basket", basket.Path);
        Assert.Contains(basket.Elements, element => element.TestId == "line-pot");
    }

    [Fact]
    public async Task Pressing_ReachesABasketHoldingTwoProducts()
    {
        // Add one, go back to the catalogue, add another. Which two does not matter: what a plan needs
        // is to have seen that a basket can hold two lines, and how it came to.
        var snapshot = Explorations.SnapshotOf(await PressedAsync());

        var basket = snapshot.Pages.FirstOrDefault(page =>
            page.Elements.Count(element => element.TestId is { } id && id.StartsWith("line-total-", StringComparison.Ordinal)) == 2);

        Assert.NotNull(basket);

        var steps = Assert.IsAssignableFrom<IReadOnlyList<string>>(basket.ReachedBy);
        Assert.Equal(4, steps.Count);
        Assert.Equal("open /", steps[0]);
        Assert.Equal("click to-catalogue", steps[2]);
    }

    [Fact]
    public async Task Pressing_NeverPlacesAnOrder()
    {
        // Finding states is the job. Buying something on the way is not, with forms or without.
        foreach (var result in new[] { await PressedAsync(), await FilledAsync() })
        {
            var snapshot = Explorations.SnapshotOf(result);

            Assert.False(Explorations.Pressed(snapshot, "place-order"), "the control that places an order was pressed.");

            // The sitemap lists the confirmation page, so the crawl reads it. It must not be reached by a press.
            Assert.DoesNotContain(snapshot.Pages, page =>
                page.Path.StartsWith("/order/placed", StringComparison.Ordinal) && page.ReachedBy is { Count: > 0 });
        }
    }

    [Fact]
    public async Task Explore_WithoutBeingAskedToPress_PressesNothing()
    {
        // Off unless asked for. An exploration of an application nobody chose to exercise only reads it.
        var result = await Jobs.ExploreAsync(shop.Origin);
        var snapshot = Explorations.SnapshotOf(result);

        Assert.DoesNotContain(snapshot.Pages, page => page.ReachedBy is { Count: > 0 });
    }

    [Fact]
    public async Task Pressing_WithoutForms_TypesNothing()
    {
        // Pressing and typing are asked for separately, and the second is the larger thing to ask.
        var snapshot = Explorations.SnapshotOf(await PressedAsync());

        Assert.DoesNotContain(snapshot.Pages, page =>
            page.ReachedBy is { } steps && steps.Any(step => step.Contains("type", StringComparison.Ordinal)));

        // What only a typed value can reach is not there.
        Assert.False(Explorations.Shows(snapshot, "no-results"));
        Assert.False(Explorations.Shows(snapshot, "discount-state"));
    }

    [Fact]
    public async Task Forms_ReachTheSearchThatFindsNothing()
    {
        // One of the three things the coverage measure said the demo shop's crawl never showed.
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var nothing = snapshot.Pages.FirstOrDefault(page => page.Elements.Any(element => element.TestId == "no-results"));

        Assert.NotNull(nothing);
        Assert.Equal(["open /", "type test values, then click do-search"], nothing.ReachedBy);

        // What was typed is ours, and it is in the address for anybody to check: a fixed value, not
        // anything the page offered.
        Assert.Equal("/?q=Test+value", nothing.Path);
    }

    [Fact]
    public async Task Forms_ReachWhatTheCheckoutSaysAboutACodeItDoesNotKnow()
    {
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        Assert.True(Explorations.Shows(snapshot, "discount-state"));
    }

    [Fact]
    public async Task Forms_ReachTheSignInRefusal_AndNeverSignIn()
    {
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        Assert.True(Explorations.Shows(snapshot, "sign-in-error"));

        // Without a test account (ADR-0033) no value the exploration types is a credential, so the signed-in
        // account page stays unseen.
        Assert.False(Explorations.Shows(snapshot, "signed-in-as"));
    }

    [Fact]
    public async Task Forms_WithTheTestAccount_SignIn_AndSeeWhatIsBehindIt()
    {
        // ADR-0033 §5: with the environment's test account, the explorer signs in and reads the page behind it.
        var snapshot = Explorations.SnapshotOf(await SignedInAsync());

        var account = snapshot.Pages.FirstOrDefault(page => page.Elements.Any(element => element.TestId == "signed-in-as"));

        Assert.NotNull(account);
        var steps = Assert.IsAssignableFrom<IReadOnlyList<string>>(account.ReachedBy);
        Assert.Contains(steps, step => step.StartsWith("sign in with the test account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Forms_WithTheTestAccount_NeverPutItsValuesInTheDescription()
    {
        // The description goes to a model, and credentials never do. The shop shows the user name once signed in.
        var text = Explorations.Text(Explorations.SnapshotOf(await SignedInAsync()));

        Assert.DoesNotContain(DemoShop.Shop.Password, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DemoShop.Shop.Account, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[secret:username]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forms_WithAWrongPassword_StayOnTheRefusal()
    {
        // What the account opens is what the shop says it opens, not what the explorer assumes.
        var wrong = new Dictionary<string, string>(Account, StringComparer.Ordinal) { ["password"] = "not-the-password" };
        var snapshot = Explorations.SnapshotOf(
            await Explorations.OnceAsync(shop.Origin, pressing: true, forms: true, secrets: wrong, secretsLabel: "wrong"));

        Assert.False(Explorations.Shows(snapshot, "signed-in-as"));
        Assert.True(Explorations.Shows(snapshot, "sign-in-error"));
    }

    [Fact]
    public async Task Pressing_WithTheTestAccountButWithoutForms_NeverSignsIn()
    {
        // Signing in is typing into a form: an exploration that may not type may not sign in, account or not.
        var snapshot = Explorations.SnapshotOf(
            await Explorations.OnceAsync(shop.Origin, pressing: true, forms: false, secrets: Account, secretsLabel: "account"));

        Assert.False(Explorations.Shows(snapshot, "signed-in-as"));
        Assert.DoesNotContain(snapshot.Pages, page =>
            page.ReachedBy is { } steps && steps.Any(step => step.StartsWith("sign in", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task EveryPageReachedByPressing_SaysWhereItStarted()
    {
        // A path means nothing without its start: the same press does different things on different pages.
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var reached = snapshot.Pages.Where(page => page.ReachedBy is { Count: > 0 }).ToList();

        Assert.NotEmpty(reached);
        Assert.All(reached, page => Assert.StartsWith("open /", page.ReachedBy![0], StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pressing_IsTheSameOnEveryRun()
    {
        // A plan is only reproducible if its input is (ADR-0019 §5). Everything is compared: the pages,
        // the order, how each was reached and every word on it.
        var first = Explorations.SnapshotOf(await FilledAsync());

        var second = Explorations.SnapshotOf(await Jobs.ExploreAsync(
            shop.Origin,
            Explorations.Budget,
            exploreStates: true,
            submitForms: true));

        Assert.Equal(Explorations.Text(first), Explorations.Text(second));
    }

    [Fact]
    public async Task Pressing_StaysWithinAHundredVisits()
    {
        // An earlier version of this search made 253 visits to the same shop and took three minutes,
        // most of it pressing "sign in" four times running. The count is what a change to the search
        // has to answer for.
        var visits = Explorations.Visits(await FilledAsync());

        Assert.InRange(visits, 1, 100);
    }
}
