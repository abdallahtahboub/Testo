using Bunit;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TCardTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsAPlainSurfaceWithItsContent()
    {
        var cut = Render<TCard>(parameters => parameters.AddChildContent("<p>Body</p>"));

        var card = cut.Find(".t-card");
        Assert.Equal("DIV", card.TagName);
        Assert.Equal("Body", cut.Find(".t-card__body p").TextContent);
        Assert.Empty(cut.FindAll(".t-card__header"));
        Assert.Empty(cut.FindAll(".t-card__footer"));
    }

    [Fact]
    public void Render_Title_BecomesAHeading()
    {
        var cut = Render<TCard>(parameters => parameters.Add(card => card.Title, "Release readiness"));

        var heading = cut.Find(".t-card__title");
        Assert.Equal("H3", heading.TagName);
        Assert.Equal("Release readiness", heading.TextContent);
    }

    [Fact]
    public void Render_HeaderAndFooter_AreShownInTheirOwnRegions()
    {
        var cut = Render<TCard>(parameters => parameters
            .Add(card => card.Title, "Open defects")
            .Add(card => card.Header, "<span class=\"count\">12</span>")
            .AddChildContent("<p>Body</p>")
            .Add(card => card.Footer, "<a href=\"/app\">All defects</a>"));

        Assert.Equal("12", cut.Find(".t-card__header .count").TextContent);
        Assert.Equal("/app", cut.Find(".t-card__footer a").GetAttribute("href"));
    }

    [Fact]
    public void Render_Href_MakesTheWholeCardALink()
    {
        var card = Render<TCard>(parameters => parameters
            .Add(component => component.Href, "/app/p/orion/defects")
            .Add(component => component.Title, "Defects")).Find(".t-card");

        Assert.Equal("A", card.TagName);
        Assert.Equal("/app/p/orion/defects", card.GetAttribute("href"));
        Assert.Contains("t-card--interactive", card.ClassList);
    }

    [Fact]
    public void Render_WithoutHref_IsNotMarkedInteractive()
    {
        Assert.DoesNotContain("t-card--interactive", Render<TCard>().Find(".t-card").ClassList);
    }
}

public class TAvatarTests : DesignSystemTestContext
{
    [Theory]
    [InlineData("Mara Lindqvist", "ML")]
    [InlineData("Tom", "T")]
    [InlineData("Jonas van der Berg", "JB")]
    [InlineData("  spaced   out  ", "SO")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    public void Initials_AnyName_TakesTheFirstAndLastWord(string? name, string expected)
    {
        Assert.Equal(expected, TAvatar.Initials(name));
    }

    [Fact]
    public void Render_NextToAName_IsDecorative()
    {
        // The name is already on screen; reading it twice helps nobody.
        var avatar = Render<TAvatar>(parameters => parameters.Add(component => component.Name, "Mara Lindqvist")).Find(".t-avatar");

        Assert.Equal("true", avatar.GetAttribute("aria-hidden"));
        Assert.Equal("ML", avatar.TextContent.Trim());
        Assert.Equal("Mara Lindqvist", avatar.GetAttribute("title"));
    }

    [Fact]
    public void Render_Standalone_CarriesTheName()
    {
        var avatar = Render<TAvatar>(parameters => parameters
            .Add(component => component.Name, "Mara Lindqvist")
            .Add(component => component.Standalone, true)).Find(".t-avatar");

        Assert.Equal("img", avatar.GetAttribute("role"));
        Assert.Equal("Mara Lindqvist", avatar.GetAttribute("aria-label"));
        Assert.False(avatar.HasAttribute("aria-hidden"));
    }

    [Fact]
    public void Render_Image_ReplacesTheInitialsAndHasNoDuplicateAltText()
    {
        var cut = Render<TAvatar>(parameters => parameters
            .Add(component => component.Name, "Mara Lindqvist")
            .Add(component => component.ImageUrl, "/img/mara.jpg"));

        Assert.Empty(cut.FindAll(".t-avatar__initials"));
        Assert.Equal("", cut.Find("img").GetAttribute("alt"));
    }
}
