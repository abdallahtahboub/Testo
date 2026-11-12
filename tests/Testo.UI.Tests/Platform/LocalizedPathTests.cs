using Testo.Platform.Localization;

namespace Testo.UI.Tests.Platform;

public class LocalizedPathTests
{
    [Theory]
    [InlineData("/en", "en")]
    [InlineData("/de", "de")]
    [InlineData("/de/", "de")]
    [InlineData("/de/pricing", "de")]
    [InlineData("/DE/pricing", "de")]
    [InlineData("/en?plan=team", "en")]
    [InlineData("/en#top", "en")]
    public void TryGetCulture_UrlWithSupportedPrefix_ReturnsTheLanguage(string url, string expected)
    {
        Assert.True(LocalizedPath.TryGetCulture(url, out var culture));
        Assert.Equal(expected, culture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/app")]
    [InlineData("/app/de")]
    [InlineData("/design")]
    [InlineData("/fr/pricing")]
    [InlineData("de/pricing")]
    public void TryGetCulture_UrlWithoutSupportedPrefix_ReturnsFalse(string? url)
    {
        Assert.False(LocalizedPath.TryGetCulture(url, out var culture));
        Assert.Null(culture);
    }

    [Theory]
    [InlineData("de", null, "/de")]
    [InlineData("de", "", "/de")]
    [InlineData("de", "/", "/de")]
    [InlineData("de", "pricing", "/de/pricing")]
    [InlineData("en", "/product/planning/", "/en/product/planning")]
    public void For_CultureAndPage_BuildsThePublicUrl(string culture, string? page, string expected)
    {
        Assert.Equal(expected, LocalizedPath.For(culture, page));
    }

    [Theory]
    [InlineData("/en", "de", "/de")]
    [InlineData("/en/pricing", "de", "/de/pricing")]
    [InlineData("/de/pricing?plan=team#faq", "en", "/en/pricing?plan=team#faq")]
    [InlineData("/de/pricing", "de", "/de/pricing")]
    public void WithCulture_PublicUrl_SwitchesTheLanguagePrefix(string url, string culture, string expected)
    {
        Assert.Equal(expected, LocalizedPath.WithCulture(url, culture));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app")]
    [InlineData("/app/p/orion/defects?severity=critical")]
    public void WithCulture_UrlWithoutLanguagePrefix_IsReturnedUnchanged(string url)
    {
        Assert.Equal(url, LocalizedPath.WithCulture(url, "de"));
    }
}
