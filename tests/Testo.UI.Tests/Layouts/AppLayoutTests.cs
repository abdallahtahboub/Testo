using Bunit;
using Microsoft.AspNetCore.Components;
using Testo.UI.Shared.Layouts;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Layouts;

public class AppLayoutTests : DesignSystemTestContext
{
    private IRenderedComponent<AppLayout> RenderLayout() =>
        Render<AppLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Dashboard</h1>"))));

    [Fact]
    public void Render_Default_StartsWithTheSkipLink()
    {
        var first = RenderLayout().TabStops()[0];

        Assert.Contains("t-skip-link", first.ClassList);
    }

    [Fact]
    public void Render_Default_HasHeaderNavigationAndMainLandmarks()
    {
        var cut = RenderLayout();

        Assert.Single(cut.FindAll("header"));
        var navigation = Assert.Single(cut.FindAll("nav"));
        Assert.Equal("App navigation", navigation.GetAttribute("aria-label"));
        var main = Assert.Single(cut.FindAll("main"));
        Assert.Equal("main-content", main.Id);
        Assert.Equal("-1", main.GetAttribute("tabindex"));
        Assert.Equal("Dashboard", main.QuerySelector("h1")?.TextContent);
    }

    [Fact]
    public void Render_WithoutModules_ExplainsTheEmptyNavigation()
    {
        Assert.Equal("Modules appear here once they are switched on.", RenderLayout().Find("nav").TextContent.Trim());
    }

    [Fact]
    public void Render_Default_LinksTheBrandToTheAppAndOffersTheWebsite()
    {
        var header = RenderLayout().Find("header");

        var brand = header.QuerySelector("a.app-topbar__brand");
        Assert.Equal("/app", brand?.GetAttribute("href"));
        Assert.Equal("Acme QA app home", brand?.GetAttribute("aria-label"));
        Assert.Equal("Website", header.QuerySelector("a[href='/en']")?.TextContent);
    }

    [Fact]
    public void Render_Default_OffersLanguageAndTheme()
    {
        var header = RenderLayout().Find("header");

        Assert.NotNull(header.QuerySelector("form[action='/preferences/culture']"));
        Assert.NotNull(header.QuerySelector("form[action='/preferences/theme']"));
    }

    [Fact]
    public void Render_German_UsesGermanTexts()
    {
        using var german = new CultureScope("de");

        var cut = RenderLayout();

        Assert.Equal("App-Navigation", cut.Find("nav").GetAttribute("aria-label"));
        Assert.Equal("/de", cut.Find("header a.app-topbar__link").GetAttribute("href"));
    }
}
