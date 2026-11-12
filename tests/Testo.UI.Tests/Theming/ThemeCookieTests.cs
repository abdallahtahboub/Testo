using Testo.UI.Shared.Theming;

namespace Testo.UI.Tests.Theming;

public class ThemeCookieTests
{
    [Theory]
    [InlineData("light", ThemeChoice.Light)]
    [InlineData("LIGHT", ThemeChoice.Light)]
    [InlineData("dark", ThemeChoice.Dark)]
    [InlineData("system", ThemeChoice.System)]
    [InlineData("", ThemeChoice.System)]
    [InlineData(null, ThemeChoice.System)]
    [InlineData("<script>", ThemeChoice.System)]
    public void Parse_AnyValue_FallsBackToSystemForUnknownInput(string? value, ThemeChoice expected)
    {
        Assert.Equal(expected, ThemeCookie.Parse(value));
    }

    [Theory]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.Dark)]
    [InlineData(ThemeChoice.System)]
    public void ToValue_AnyChoice_RoundTripsThroughParse(ThemeChoice choice)
    {
        Assert.Equal(choice, ThemeCookie.Parse(ThemeCookie.ToValue(choice)));
    }
}
