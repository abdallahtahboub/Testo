using System.Globalization;
using Testo.Platform.Localization;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Platform;

public class SupportedCulturesTests
{
    [Fact]
    public void All_Always_StartsWithTheDefaultLanguage()
    {
        Assert.Equal(SupportedCultures.Default, SupportedCultures.All[0]);
        Assert.Equal(["en", "de"], SupportedCultures.All);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]
    public void Resolve_AnyCulture_MapsToTheBestSupportedLanguage(string culture, string expected)
    {
        Assert.Equal(expected, SupportedCultures.Resolve(CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void Current_RegionalUiCulture_ReturnsItsLanguage()
    {
        using var scope = new CultureScope("de-CH");

        Assert.Equal("de", SupportedCultures.Current);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("DE", true)]
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupported_Culture_TellsWhetherItShips(string? culture, bool expected)
    {
        Assert.Equal(expected, SupportedCultures.IsSupported(culture));
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("de", "Deutsch")]
    public void NativeName_SupportedLanguage_IsTheNameInThatLanguage(string culture, string expected)
    {
        Assert.Equal(expected, SupportedCultures.NativeName(culture));
    }
}
