using Autestiq.Contracts.Planning;
using Autestiq.Worker.Tests.Support;
using Microsoft.Playwright;

namespace Autestiq.Worker.Tests;

/// <summary>
/// How a target becomes an element, on small fixed pages that have the shapes eShopOnWeb showed and none of our
/// own applications has (ADR-0024). A target counts as reaching an element only when it finds exactly one.
/// </summary>
public sealed class TargetsTests : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IPage? page;

    private IPage Page => page ?? throw new InvalidOperationException("The page is opened before each test.");

    public async ValueTask InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = DemoShopFixture.BrowserPath is { Length: > 0 } path ? path : null,
        });
        page = await browser.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    private async Task<int> CountAsync(string html, ActionTarget target)
    {
        await Page.SetContentAsync(html);
        return await Targets.Locate(Page, target).CountAsync();
    }

    [Fact]
    public async Task ARoleAndName_IsMatchedExactly_SoPasswordIsNotConfirmPassword()
    {
        const string form = """
            <form>
              <label for="p">Password</label><input id="p" type="password" />
              <label for="c">Confirm password</label><input id="c" type="password" />
            </form>
            """;

        Assert.Equal(1, await CountAsync(form, ActionTarget.ByRole("textbox", "Password")));
    }

    [Fact]
    public async Task Text_IsMatchedAsThePageWroteIt_NotAsAStylesheetDrawsIt()
    {
        const string catalogue = """<p style="text-transform: uppercase"><span>Roslyn Red Sheet</span></p>""";

        Assert.Equal(1, await CountAsync(catalogue, ActionTarget.ByText("Roslyn Red Sheet")));
        Assert.Equal(0, await CountAsync(catalogue, ActionTarget.ByText("ROSLYN RED SHEET")));
    }

    [Fact]
    public async Task Within_IsTheInnermostContainerThatHoldsBothTheTextAndTheElement()
    {
        // The product's name is in a section of its own and its price in the next: the innermost container with
        // the name holds only the name. The article around both is the one meant.
        const string basket = """
            <article><section>Roslyn Red Sheet</section><section>8.50</section></article>
            <article><section>Kudu Purple Sweatshirt</section><section>8.50</section></article>
            """;

        Assert.Equal(2, await CountAsync(basket, ActionTarget.ByText("8.50")));
        Assert.Equal(1, await CountAsync(basket, ActionTarget.ByText("8.50").InsideOf("Roslyn Red Sheet")));
    }

    [Fact]
    public async Task AStep_NamingAShortenedName_StillFindsTheOneLinkWithIt()
    {
        // "Basket" for the link "Basket (2)": what a person writes, and a count that changes as the test runs.
        await Page.SetContentAsync("""<p><a href="/basket">Basket (2)</a></p>""");

        var found = await Targets.LocateForStepAsync(Page, ActionTarget.ByRole("link", "Basket"));

        Assert.Equal(1, await found.CountAsync());
        Assert.Equal(0, await Targets.Locate(Page, ActionTarget.ByRole("link", "Basket")).CountAsync());
    }

    [Fact]
    public async Task AStep_PrefersTheExactName_OverOneThatOnlyContainsIt()
    {
        await Page.SetContentAsync("""<p><a href="/a">Basket</a> <a href="/b">Back to the basket</a></p>""");

        var found = await Targets.LocateForStepAsync(Page, ActionTarget.ByRole("link", "Basket"));

        Assert.Equal("/a", await found.GetAttributeAsync("href"));
    }

    [Fact]
    public async Task Within_ATextNoContainerShares_FindsNothing()
    {
        const string basket = """<article><section>Roslyn Red Sheet</section><section>8.50</section></article>""";

        Assert.Equal(0, await CountAsync(basket, ActionTarget.ByText("8.50").InsideOf("Kudu Purple Sweatshirt")));
    }
}
