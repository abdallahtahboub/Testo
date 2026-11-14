using System.Net;
using Testo.DemoShop;
using Testo.Worker.Tests.Support;

namespace Testo.Worker.Tests;

/// <summary>
/// Each defect, switched on, changes what a page says.
/// </summary>
/// <remarks>
/// <para>
/// These are tests of the measuring instrument rather than of the product, and they are the
/// foundation of every number the AI benchmark will report. A "false positive" means the planner
/// said passed while the application was broken — so if a defect did not actually break anything,
/// every false positive counted against it would be a lie in our favour.
/// </para>
/// <para>
/// Over HTTP rather than through a browser: what is under test is the shop's arithmetic, and a
/// browser would add thirty seconds and nothing else.
/// </para>
/// </remarks>
[Collection(Serially.Name)]
public class DemoShopDefectTests(DemoShopFixture shop)
{
    [Fact]
    public async Task WithNoDefects_TheCheckoutAsksForWhatTheBasketSaid()
    {
        // The baseline. A target that is broken by default would make every measurement against
        // it suspect, so "sound" is the shop's resting state.
        using var sound = DemoShopDefects.On();
        using var client = Client();

        await AddAsync(client, "pot");
        await AddAsync(client, "cup");

        Assert.Contains("£53.25", await GetAsync(client, "/basket"), StringComparison.Ordinal);
        Assert.Contains("£53.25", await GetAsync(client, "/checkout"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckoutTotal_SwitchedOn_ChargesForTheFirstItemOnly()
    {
        using var defect = DemoShopDefects.On(Defects.CheckoutTotal);
        using var client = Client();

        await AddAsync(client, "pot");
        await AddAsync(client, "cup");

        var checkout = await GetAsync(client, "/checkout");

        // The basket still says the truth, which is what makes it findable and what makes it the
        // kind of bug that reaches production.
        Assert.Contains("£53.25", checkout, StringComparison.Ordinal);
        Assert.Contains("£42.00", checkout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuantityIgnored_SwitchedOn_ChargesForOneOfEachHoweverManyThereAre()
    {
        using var defect = DemoShopDefects.On(Defects.QuantityIgnored);
        using var client = Client();

        await AddAsync(client, "cup");
        await AddAsync(client, "cup");
        await AddAsync(client, "cup");

        var basket = await GetAsync(client, "/basket");

        // The page disagrees with itself, which is exactly how this bug survives review: the line
        // adds up and the total does not.
        Assert.Contains(">3<", basket, StringComparison.Ordinal);
        Assert.Contains("line-total-cup\">£33.75", basket, StringComparison.Ordinal);
        Assert.Contains("basket-total\">£11.25", basket, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuantityIgnored_SwitchedOff_ChargesForAllOfThem()
    {
        using var sound = DemoShopDefects.On();
        using var client = Client();

        await AddAsync(client, "cup");
        await AddAsync(client, "cup");
        await AddAsync(client, "cup");

        Assert.Contains("basket-total\">£33.75", await GetAsync(client, "/basket"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscountTwice_SwitchedOn_TakesTenPerCentOffTwice()
    {
        using var defect = DemoShopDefects.On(Defects.DiscountTwice);
        using var client = Client();

        await AddAsync(client, "pot");

        // £42.00 less a tenth is £37.80. Less a tenth again is £34.02.
        Assert.Contains("£34.02", await GetAsync(client, "/checkout?code=SAVE10"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscountTwice_SwitchedOff_TakesItOffOnce()
    {
        using var sound = DemoShopDefects.On();
        using var client = Client();

        await AddAsync(client, "pot");

        Assert.Contains("£37.80", await GetAsync(client, "/checkout?code=SAVE10"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignInAnyPassword_SwitchedOn_LetsTheWrongPasswordIn()
    {
        // The one defect that is a security hole rather than a wrong number, and the one most
        // worth a tester catching.
        using var defect = DemoShopDefects.On(Defects.SignInAnyPassword);
        using var client = Client();

        Assert.Contains(
            "Signed in as",
            await SignInAsync(client, Shop.Account, "not the password"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignInAnyPassword_SwitchedOff_RefusesTheWrongPassword()
    {
        using var sound = DemoShopDefects.On();
        using var client = Client();

        Assert.Contains(
            "not the right details",
            await SignInAsync(client, Shop.Account, "not the password"),
            StringComparison.Ordinal);

        Assert.Contains(
            "Signed in as",
            await SignInAsync(client, Shop.Account, Shop.Password),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveWrongItem_SwitchedOn_TakesOutSomethingElse()
    {
        using var defect = DemoShopDefects.On(Defects.RemoveWrongItem);
        using var client = Client();

        await AddAsync(client, "pot");
        await AddAsync(client, "cup");

        using var removed = await client.PostAsync(
            "/basket/remove",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("id", "cup")]),
            TestContext.Current.CancellationToken);

        var basket = await GetAsync(client, "/basket");

        // Asked for the cup to go; the teapot went.
        Assert.Contains("line-cup", basket, StringComparison.Ordinal);
        Assert.DoesNotContain("line-pot", basket, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveWrongItem_SwitchedOff_TakesOutTheOneAskedFor()
    {
        using var sound = DemoShopDefects.On();
        using var client = Client();

        await AddAsync(client, "pot");
        await AddAsync(client, "cup");

        using var removed = await client.PostAsync(
            "/basket/remove",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("id", "cup")]),
            TestContext.Current.CancellationToken);

        var basket = await GetAsync(client, "/basket");

        Assert.Contains("line-pot", basket, StringComparison.Ordinal);
        Assert.DoesNotContain("line-cup", basket, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyOrderAllowed_SwitchedOn_LetsNothingBeOrdered()
    {
        using var defect = DemoShopDefects.On(Defects.EmptyOrderAllowed);
        using var client = Client();

        Assert.Contains("place-order", await GetAsync(client, "/checkout"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyOrderAllowed_SwitchedOff_SaysThereIsNothingToPayFor()
    {
        using var sound = DemoShopDefects.On();
        using var client = Client();

        var checkout = await GetAsync(client, "/checkout");

        Assert.Contains("nothing-to-pay", checkout, StringComparison.Ordinal);
        Assert.DoesNotContain("place-order", checkout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDefectSwitch_RefusesANameItDoesNotKnow()
    {
        // A typo that silently meant "no defects" would flatter every result measured afterwards.
        using var client = Client();

        using var response = await client.PostAsync(
            "/_defects",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("defects", "no-such-defect")]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient Client() => shop.CreateClient();

    private static async Task<string> GetAsync(HttpClient client, string path) =>
        await client.GetStringAsync(path, TestContext.Current.CancellationToken);

    private static async Task AddAsync(HttpClient client, string id)
    {
        using var response = await client.PostAsync(
            "/basket/add",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("id", id)]),
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> SignInAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsync(
            "/sign-in",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("email", email),
                new KeyValuePair<string, string>("password", password),
            ]),
            TestContext.Current.CancellationToken);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
