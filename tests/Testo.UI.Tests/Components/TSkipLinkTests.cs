using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TSkipLinkTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_LinksToTheMainRegionOfTheCurrentPage()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/en/pricing?plan=team");

        var link = Render<TSkipLink>().Find("a");

        Assert.Equal("/en/pricing?plan=team#main-content", link.GetAttribute("href"));
        Assert.Equal("Skip to main content", link.TextContent);
    }

    [Fact]
    public void Render_Default_LetsTheBrowserHandleTheJump()
    {
        // Without target="_top", Blazor's interactive router intercepts the click and
        // keyboard focus stays where it was.
        Assert.Equal("_top", Render<TSkipLink>().Find("a").GetAttribute("target"));
    }

    [Fact]
    public void Render_OnASubstitutePage_LinksToTheAddressTheVisitorAskedFor()
    {
        // On the not-found page the framework reports "/not-found" as the current address.
        Services.GetRequiredService<NavigationManager>().NavigateTo("/not-found");

        var link = Render<TSkipLink>(parameters => parameters.AddCascadingValue(new RequestedUrl("/de/missing?x=1"))).Find("a");

        Assert.Equal("/de/missing?x=1#main-content", link.GetAttribute("href"));
    }

    [Fact]
    public void Render_CustomTarget_UsesItsId()
    {
        var link = Render<TSkipLink>(parameters => parameters.Add(component => component.TargetId, "results")).Find("a");

        Assert.EndsWith("#results", link.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_German_ShowsTheGermanText()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Zum Hauptinhalt springen", Render<TSkipLink>().Find("a").TextContent);
    }

    [Fact]
    public void Navigate_ToAnotherPage_UpdatesTheLink()
    {
        var cut = Render<TSkipLink>();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/app/settings");

        cut.WaitForAssertion(() => Assert.Equal("/app/settings#main-content", cut.Find("a").GetAttribute("href")));
    }
}
