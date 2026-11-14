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
    public void Render_Default_OffersLanguageThemeAndTheTwoThingsAVisitorMightDo()
    {
        var header = RenderLayout().Find("header");

        Assert.NotNull(header.QuerySelector("form[action='/preferences/culture']"));
        Assert.NotNull(header.QuerySelector("form[action='/preferences/theme']"));

        // Signing in and asking for a demo. Not "open app": somebody arriving on the public site
        // has no session yet, so a link straight into the product would only bounce them back.
        // Scoped to the tools, because a menu panel also features the demo page — which is the
        // point of featuring it, and would otherwise make this assertion pass by accident.
        var tools = header.QuerySelector(".site-header__tools")!;

        Assert.Equal("Sign in", tools.QuerySelector("a[href='/account/sign-in']")?.TextContent?.Trim());
        Assert.Equal("Request a demo", tools.QuerySelector("a[href='/en/request-demo']")?.TextContent?.Trim());
    }

    [Fact]
    public void Render_Footer_ShowsTheCurrentYearAndTheProductName()
    {
        Assert.Equal($"© {Now.Year} Acme QA", RenderLayout().Find("footer .site-footer__copyright").TextContent);
    }

    [Fact]
    public void Render_Header_ShowsEverySectionOfTheRegisteredMenu()
    {
        var header = RenderLayout().Find("header");

        Assert.Equal(
            ["Product", "Solutions", "Resources", "Company"],
            header.QuerySelectorAll("details.site-menu > summary > span").Select(element => element.TextContent.Trim()));

        // Pricing sits beside the sections rather than inside one, because it is a page and not a
        // subject.
        Assert.Equal("Pricing", header.QuerySelector("a.site-nav__link")?.TextContent?.Trim());
    }

    [Fact]
    public void Render_Header_EverySectionPanelStartsWithTheSectionsOwnPage()
    {
        // The reason this matters: a panel that can only be reached by hovering is no use on a
        // phone, so every section must be a place a visitor can simply go.
        var header = RenderLayout().Find("header");

        foreach (var panel in header.QuerySelectorAll("details.site-menu .site-menu__panel"))
        {
            Assert.NotEmpty(panel.QuerySelectorAll("a.site-menu__link"));
        }
    }

    [Fact]
    public void Render_Header_SaysWhichLinksLeadToAPlaceholder()
    {
        // A visitor who follows a link to an unwritten page should have been told beforehand.
        var header = RenderLayout().Find("header");

        Assert.NotEmpty(header.QuerySelectorAll(".site-menu__placeholder"));
        Assert.All(
            header.QuerySelectorAll(".site-menu__placeholder"),
            element => Assert.Equal("Placeholder", element.TextContent.Trim()));
    }

    [Fact]
    public void Render_Header_InGerman_UsesTheGermanMenu()
    {
        using var german = new CultureScope("de");

        var header = RenderLayout().Find("header");

        Assert.Equal(
            ["Produkt", "Lösungen", "Material", "Unternehmen"],
            header.QuerySelectorAll("details.site-menu > summary > span").Select(element => element.TextContent.Trim()));
    }

    [Fact]
    public void Render_Footer_HasEveryColumnAndTheLegalRow()
    {
        var footer = RenderLayout().Find("footer");

        Assert.Equal(4, footer.QuerySelectorAll(".site-footer__column").Length);

        // German law wants the imprint and the privacy notice reachable from every page.
        Assert.Equal(
            ["Imprint", "Privacy", "Terms", "Accessibility"],
            footer.QuerySelectorAll(".site-footer__legal a").Select(element => element.TextContent.Trim()));
    }

    [Fact]
    public void Render_Footer_SaysTheNewsletterFieldGoesNowhere()
    {
        // A field that looks like it signs somebody up and does not is worse than no field: it
        // collects an address and a false expectation.
        var note = RenderLayout().Find("footer .site-footer__note");

        Assert.Contains("not connected to anything yet", note.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_CookieNotice_SaysWhatIsSetAndAsksForNothing()
    {
        var notice = RenderLayout().Find("[data-testid='cookie-notice']");

        Assert.Contains("three cookies", notice.TextContent, StringComparison.Ordinal);

        // No consent to give, so no button to press. A dialogue over nothing is theatre.
        Assert.Empty(notice.QuerySelectorAll("button"));
    }
}
