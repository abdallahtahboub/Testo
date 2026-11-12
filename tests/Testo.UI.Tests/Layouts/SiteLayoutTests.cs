using Bunit;
using Microsoft.AspNetCore.Components;
using Testo.UI.Shared.Layouts;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Layouts;

public class SiteLayoutTests : DesignSystemTestContext
{
    private IRenderedComponent<SiteLayout> RenderLayout() =>
        Render<SiteLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Page</h1>"))));

    [Fact]
    public void Render_Default_StartsWithTheSkipLink()
    {
        var first = RenderLayout().TabStops()[0];

        Assert.Contains("t-skip-link", first.ClassList);
        Assert.EndsWith("#main-content", first.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Default_HasOneHeaderMainAndFooterLandmark()
    {
        var cut = RenderLayout();

        Assert.Single(cut.FindAll("header"));
        Assert.Single(cut.FindAll("footer"));
        var main = Assert.Single(cut.FindAll("main"));
        Assert.Equal("main-content", main.Id);
        // tabindex="-1" lets the skip link move keyboard focus into the main region.
        Assert.Equal("-1", main.GetAttribute("tabindex"));
        Assert.Equal("Page", main.QuerySelector("h1")?.TextContent);
    }

    [Fact]
    public void Render_Default_LinksTheBrandToTheHomePageOfTheCurrentLanguage()
    {
        var brand = RenderLayout().Find("header a.site-header__brand");

        Assert.Equal("/en", brand.GetAttribute("href"));
        Assert.Equal("Acme QA home page", brand.GetAttribute("aria-label"));
    }

    [Fact]
    public void Render_German_LinksToTheGermanHomePage()
    {
        using var german = new CultureScope("de");

        var brand = RenderLayout().Find("header a.site-header__brand");

        Assert.Equal("/de", brand.GetAttribute("href"));
        Assert.Equal("Acme QA Startseite", brand.GetAttribute("aria-label"));
    }

    [Fact]
    public void Render_Default_OffersLanguageThemeAndTheApp()
    {
        var header = RenderLayout().Find("header");

        Assert.NotNull(header.QuerySelector("form[action='/preferences/culture']"));
        Assert.NotNull(header.QuerySelector("form[action='/preferences/theme']"));
        Assert.Equal("Open app", header.QuerySelector("a[href='/app']")?.TextContent);
    }

    [Fact]
    public void Render_Footer_ShowsTheCurrentYearAndTheProductName()
    {
        Assert.Equal($"© {Now.Year} Acme QA", RenderLayout().Find("footer p").TextContent);
    }
}
