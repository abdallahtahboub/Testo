namespace Autestiq.Worker.Tests;

/// <summary>
/// Which address a crawl opens next, and whether it stopped short. No browser: the rule is arithmetic
/// on addresses, and the cases that matter most are ones neither of our applications happens to have.
/// </summary>
public class CrawlFrontierTests
{
    private static List<string> Drain(CrawlFrontier frontier, Func<string, string> shapeOf)
    {
        var opened = new List<string>();

        while (frontier.Next() is { } path)
        {
            opened.Add(path);
            frontier.Saw(path, shapeOf(path));
        }

        return opened;
    }

    [Fact]
    public void OneAddressOfEveryKind_ComesBeforeASecondAddressOfAny()
    {
        var frontier = new CrawlFrontier("/");
        frontier.Note("/day/1");
        frontier.Note("/day/2");
        frontier.Note("/day/3");
        frontier.Note("/about");

        Assert.Equal("/", frontier.Next());
        Assert.Equal("/day/1", frontier.Next());
        Assert.Equal("/about", frontier.Next());
        Assert.Equal("/day/2", frontier.Next());
    }

    [Fact]
    public void AKindWhoseSecondAddressIsTheSameKindOfPage_IsSettled_AndTheRestAreLeftAlone()
    {
        var frontier = new CrawlFrontier("/");

        foreach (var day in Enumerable.Range(1, 30))
        {
            frontier.Note($"/day/{day}");
        }

        var opened = Drain(frontier, path => path == "/" ? "home" : "a day");

        // The first, and one more as a check. Not thirty.
        Assert.Equal(["/", "/day/1", "/day/2"], opened);
        Assert.False(frontier.HasUnseen, "thirty dates of a settled kind are not a missing part of the application.");
    }

    [Fact]
    public void AKindWhoseSecondAddressIsADifferentPage_IsOpenedInFull()
    {
        // An application that numbers its pages: /page/1 is "about" and /page/2 is "contact". The
        // numbers meant more than a date, and one sample would have hidden the rest.
        var frontier = new CrawlFrontier("/");

        foreach (var number in Enumerable.Range(1, 5))
        {
            frontier.Note($"/page/{number}");
        }

        var opened = Drain(frontier, path => "the page at " + path);

        Assert.Equal(["/", "/page/1", "/page/2", "/page/3", "/page/4", "/page/5"], opened);
    }

    [Fact]
    public void AKindThatHasShownTwoDifferentPages_IsNeverSettledByALaterMatch()
    {
        var frontier = new CrawlFrontier("/page/1");
        frontier.Note("/page/2");
        frontier.Note("/page/3");
        frontier.Note("/page/4");

        var shapes = new Dictionary<string, string>
        {
            ["/page/1"] = "one",
            ["/page/2"] = "two",
            ["/page/3"] = "one",
            ["/page/4"] = "four",
        };

        Assert.Equal(["/page/1", "/page/2", "/page/3", "/page/4"], Drain(frontier, path => shapes[path]));
    }

    [Fact]
    public void StoppingWithANewKindStillWaiting_IsStoppingShort()
    {
        var frontier = new CrawlFrontier("/");
        frontier.Note("/about");

        Assert.Equal("/", frontier.Next());

        Assert.True(frontier.HasUnseen);
    }

    [Fact]
    public void StoppingBeforeAKindHasBeenChecked_IsStoppingShort()
    {
        // One sample is not enough to say the rest are the same.
        var frontier = new CrawlFrontier("/day/1");
        frontier.Note("/day/2");

        var first = frontier.Next();
        Assert.Equal("/day/1", first);
        frontier.Saw("/day/1", "a day");

        Assert.True(frontier.HasUnseen);
    }

    [Fact]
    public void AnAddressNotedTwice_IsOpenedOnce_WhateverItsCase()
    {
        var frontier = new CrawlFrontier("/");
        frontier.Note("/About");
        frontier.Note("/about");
        frontier.Note("/");

        Assert.Equal(["/", "/About"], Drain(frontier, path => path));
    }

    [Fact]
    public void WhenTheFirstAddressOfAKindWasNeverSeen_TheNextOneStandsInForIt()
    {
        // A page that would not load says nothing about its kind. The next address of the kind is the
        // first sample, and the one after it is the check.
        var frontier = new CrawlFrontier("/day/1");
        frontier.Note("/day/2");
        frontier.Note("/day/3");
        frontier.Note("/day/4");

        Assert.Equal("/day/1", frontier.Next());   // opened, and it failed: nothing is recorded

        Assert.Equal("/day/2", frontier.Next());
        frontier.Saw("/day/2", "a day");

        Assert.Equal("/day/3", frontier.Next());
        frontier.Saw("/day/3", "a day");

        Assert.Null(frontier.Next());
        Assert.False(frontier.HasUnseen);
    }

    [Theory]
    [InlineData("/book/initial/time?date=2026-11-16", "/book/initial/time?date=#-#-#")]
    [InlineData("/booking/BK-1001", "/booking/BK-#")]
    [InlineData("/about", "/about")]
    public void AKind_IsAnAddressWithItsNumbersMasked(string path, string kind)
    {
        Assert.Equal(kind, CrawlFrontier.KindOf(path));
    }
}
