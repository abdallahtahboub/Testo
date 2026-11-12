using Bunit;
using Testo.Platform;
using Testo.UI.Shared.Components;
using Testo.UI.Shared.Theming;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TThemeSwitchTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_OffersLightDarkAndSystem()
    {
        var cut = Render<TThemeSwitch>();

        Assert.Equal("Color theme", cut.Find("[role=group]").GetAttribute("aria-label"));
        var buttons = cut.FindAll("button[name=theme]");
        Assert.Equal(["light", "dark", "system"], buttons.Select(button => button.GetAttribute("value")));
        Assert.Equal(["Light", "Dark", "Same as system"], buttons.Select(button => button.TextContent.Trim()));
    }

    [Theory]
    [InlineData(ThemeChoice.System, "system")]
    [InlineData(ThemeChoice.Light, "light")]
    [InlineData(ThemeChoice.Dark, "dark")]
    public void Render_ChoiceFromTheServer_PressesExactlyThatOption(ThemeChoice choice, string expected)
    {
        Theme.Choice = choice;

        var pressed = Render<TThemeSwitch>().FindAll("button[aria-pressed=true]");

        Assert.Equal(expected, Assert.Single(pressed).GetAttribute("value"));
    }

    [Fact]
    public void Render_Default_PostsToTheThemeEndpoint()
    {
        Assert.Equal(Routes.ThemePreference, Render<TThemeSwitch>().Find("form").GetAttribute("action"));
    }

    [Fact]
    public void Render_Default_CarriesTheHooksOfTheThemeScript()
    {
        // js/theme.js finds the form by this attribute and reads the cookie name from it.
        var form = Render<TThemeSwitch>().Find("form");

        Assert.True(form.HasAttribute("data-theme-switch"));
        Assert.Equal(ThemeCookie.Name, form.GetAttribute("data-theme-cookie"));
    }

    [Fact]
    public void Render_Icons_AreHiddenFromAssistiveTechnology()
    {
        var icons = Render<TThemeSwitch>().FindAll("button svg");

        Assert.Equal(3, icons.Count);
        Assert.All(icons, icon => Assert.Equal("true", icon.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void Render_German_ShowsGermanLabels()
    {
        using var german = new CultureScope("de");

        var cut = Render<TThemeSwitch>();

        Assert.Equal("Farbschema", cut.Find("[role=group]").GetAttribute("aria-label"));
        Assert.Equal(["Hell", "Dunkel", "Wie Systemeinstellung"], cut.FindAll("button").Select(button => button.TextContent.Trim()));
    }
}
