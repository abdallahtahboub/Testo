using Bunit;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TLinkButtonTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsAPrimaryLink()
    {
        var link = Render<TLinkButton>(parameters => parameters
            .Add(component => component.Href, "/app")
            .AddChildContent("Open app")).Find("a");

        Assert.Equal("/app", link.GetAttribute("href"));
        Assert.Equal("Open app", link.TextContent);
        Assert.Contains("t-link-button--primary", link.ClassList);
    }

    [Fact]
    public void Render_Secondary_UsesTheSecondaryStyle()
    {
        var link = Render<TLinkButton>(parameters => parameters
            .Add(component => component.Href, "/en")
            .Add(component => component.Variant, ButtonVariant.Secondary)).Find("a");

        Assert.Contains("t-link-button--secondary", link.ClassList);
        Assert.DoesNotContain("t-link-button--primary", link.ClassList);
    }

    [Fact]
    public void Render_ExtraAttributes_ArePassedToTheLink()
    {
        var link = Render<TLinkButton>(parameters => parameters
            .Add(component => component.Href, "/en")
            .AddUnmatched("data-testid", "cta")
            .AddUnmatched("rel", "nofollow")).Find("a");

        Assert.Equal("cta", link.GetAttribute("data-testid"));
        Assert.Equal("nofollow", link.GetAttribute("rel"));
    }
}
