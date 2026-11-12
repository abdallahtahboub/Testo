using Bunit;
using Testo.Platform;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TLanguageSwitchTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_OffersEverySupportedLanguageInItsOwnName()
    {
        var cut = Render<TLanguageSwitch>();

        Assert.Equal("Language", cut.Find("[role=group]").GetAttribute("aria-label"));
        var buttons = cut.FindAll("button[name=culture]");
        Assert.Equal(["en", "de"], buttons.Select(button => button.GetAttribute("value")));
        Assert.Equal(["en", "de"], buttons.Select(button => button.GetAttribute("lang")));
        Assert.Equal(["English", "Deutsch"], buttons.Select(button => button.QuerySelector(".t-visually-hidden")?.TextContent));
        Assert.Equal(["EN", "DE"], buttons.Select(button => button.QuerySelector("[aria-hidden=true]")?.TextContent));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    public void Render_CurrentCulture_MarksItsLanguageAsCurrent(string culture, string expected)
    {
        using var scope = new CultureScope(culture);

        var current = Render<TLanguageSwitch>().FindAll("button[aria-current=true]");

        Assert.Equal(expected, Assert.Single(current).GetAttribute("value"));
    }

    [Fact]
    public void Render_Default_PostsToTheLanguageEndpoint()
    {
        Assert.Equal(Routes.CulturePreference, Render<TLanguageSwitch>().Find("form").GetAttribute("action"));
    }

    [Fact]
    public void Render_German_ShowsTheGermanGroupLabel()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Sprache", Render<TLanguageSwitch>().Find("[role=group]").GetAttribute("aria-label"));
    }
}
