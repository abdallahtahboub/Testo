using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// A client-side application's pages are routes in the fragment, drawn after the page has loaded (R9, OWASP Juice
/// Shop). The crawl must tell the routes apart and read each once it is drawn.
/// </summary>
public class HashRoutedExploreTests(HashRoutedSiteFixture site) : IClassFixture<HashRoutedSiteFixture>
{
    private Task<Contracts.Execution.WorkerResult> CrawlAsync() => Jobs.ExploreAsync(site.Origin, Explorations.Budget);

    [Fact]
    public async Task EveryRouteInTheFragment_IsAPageOfItsOwn()
    {
        var snapshot = Explorations.SnapshotOf(await CrawlAsync());

        Assert.True(snapshot.HasPath("/#/about"));
        Assert.True(snapshot.HasPath("/#/contact"));
    }

    [Fact]
    public async Task AFragmentThatIsAPlaceOnThePage_IsNotAPage()
    {
        var snapshot = Explorations.SnapshotOf(await CrawlAsync());

        Assert.DoesNotContain(snapshot.Pages, page => page.Path.Contains("#main", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APage_IsReadOnceItIsDrawn_NotAtLoad()
    {
        // Each route's text is fetched, and appears 600 ms after its page has loaded.
        var snapshot = Explorations.SnapshotOf(await CrawlAsync());

        Assert.Contains(snapshot.Pages, page => page.Path == "/#/about" && page.Heading == "About the orchard"
            && page.Elements.Any(element => element.Text == "Founded in 2014"));
        Assert.Contains(snapshot.Pages, page => page.Path == "/#/contact" && page.Heading == "Write to us");
        Assert.Contains(snapshot.Pages, page => page.Path == "/" && page.Heading == "Welcome");
    }
}
