using System.Xml.Linq;
using System.Text.RegularExpressions;
using Testo.Platform;
using Testo.Platform.Localization;
using Testo.UI.Tests.Support;
using Testo.Web.Seo;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// The files search engines read, and the pages that ask not to be read.
/// </summary>
/// <remarks>
/// Both are generated from <c>PublicPages</c>, so what these tests really check is that the
/// catalogue and the pages still agree with each other. Two descriptions of the same site always
/// drift; this is where that is noticed.
/// </remarks>
public partial class SeoTests
{
    private const string Origin = "https://testo.example";

    [Fact]
    public void SiteMap_ListsEveryWrittenPageInBothLanguages()
    {
        var map = XDocument.Parse(SiteMapEndpoints.SiteMap(Origin));
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var locations = map.Descendants(sitemap + "loc").Select(element => element.Value).ToList();

        Assert.Equal(PublicPages.Indexed.Count * SupportedCultures.All.Count, locations.Count);
        Assert.Contains(Origin + "/en", locations);
        Assert.Contains(Origin + "/de", locations);
        Assert.Contains(Origin + "/en/product/execution", locations);
        Assert.Contains(Origin + "/de/product/execution", locations);
    }

    [Fact]
    public void SiteMap_LeavesOutThePagesThatAreNotWrittenYet()
    {
        var text = SiteMapEndpoints.SiteMap(Origin);

        foreach (var page in PublicPages.NotIndexed)
        {
            Assert.DoesNotContain($"{Origin}/en/{page}<", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SiteMap_GivesEveryEntryItsAlternatesInEveryLanguage()
    {
        // Without these a search engine treats the English and German pages as two pages competing
        // with each other, which is the opposite of what a translated site wants (ADR-0006).
        var map = XDocument.Parse(SiteMapEndpoints.SiteMap(Origin));
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";

        foreach (var url in map.Descendants(sitemap + "url"))
        {
            var alternates = url.Elements(xhtml + "link")
                .Select(link => link.Attribute("hreflang")?.Value ?? "")
                .ToList();

            Assert.Equal(SupportedCultures.All, alternates);
        }
    }

    [Fact]
    public void Robots_KeepsCrawlersOutOfThePagesThatNeedASession()
    {
        var robots = SiteMapEndpoints.Robots(Origin);

        Assert.Contains("Disallow: /app", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /account", robots, StringComparison.Ordinal);
        Assert.Contains($"Sitemap: {Origin}/sitemap.xml", robots, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPageThatAsksNotToBeIndexed_IsInTheCatalogue()
    {
        // The page and the catalogue each say which pages are placeholders. This is the test that
        // stops them disagreeing, in either direction.
        var declared = PageFiles()
            .Where(IsNotIndexed)
            .Select(RouteOf)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PublicPages.NotIndexed.Order(StringComparer.Ordinal), declared);
    }

    /// <summary>Every routable page of the public website.</summary>
    private static IReadOnlyList<string> PageFiles() =>
    [
        .. RepositoryRoot.SourceFiles("*.razor")
            .Where(file => RepositoryRoot.Relative(file).StartsWith("src/Testo.Website/Pages/", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("@page", StringComparison.Ordinal))
            .Select(RepositoryRoot.Relative)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// The components that set <c>NoIndex</c> for whatever page renders them: the industry view and
    /// the empty listing. A page of four lines that delegates to one of these is as much a
    /// placeholder as one that says so itself.
    /// </summary>
    private static IReadOnlyList<string> NotIndexedViews { get; } =
    [
        .. RepositoryRoot.SourceFiles("*.razor")
            .Where(file => RepositoryRoot.Relative(file).StartsWith("src/Testo.Website/Components/", StringComparison.Ordinal))
            .Where(file => NoIndex().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetFileNameWithoutExtension(file)),
    ];

    /// <summary>Whether this page asks search engines to skip it, directly or through a view.</summary>
    private static bool IsNotIndexed(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));

        return NoIndex().IsMatch(text)
            || NotIndexedViews.Any(view => text.Contains("<" + view, StringComparison.Ordinal));
    }

    private static string RouteOf(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));
        var marker = text.IndexOf("@page \"/en", StringComparison.Ordinal);
        var start = marker + "@page \"".Length;
        var end = text.IndexOf('"', start);

        return text[start..end].TrimStart('/')["en".Length..].Trim('/');
    }

    [GeneratedRegex(@"NoIndex\s*=\s*""true""")]
    private static partial Regex NoIndex();
}
