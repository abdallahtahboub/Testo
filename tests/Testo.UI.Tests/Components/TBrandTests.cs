using Bunit;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TBrandTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_ShowsTheConfiguredProductName()
    {
        // The name comes from BrandOptions, never from a hard-coded text.
        Assert.Equal("Acme QA", Render<TBrand>().Find(".t-brand__name").TextContent);
    }

    [Fact]
    public void Render_Default_HidesTheDecorativeMarkFromAssistiveTechnology()
    {
        var mark = Render<TBrand>().Find("svg");

        Assert.Equal("true", mark.GetAttribute("aria-hidden"));
        Assert.Equal("false", mark.GetAttribute("focusable"));
    }
}
