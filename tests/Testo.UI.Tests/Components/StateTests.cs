using Bunit;
using Microsoft.AspNetCore.Components;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TSkeletonTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_AnnouncesLoadingOnceAndHidesTheShapes()
    {
        var cut = Render<TSkeleton>(parameters => parameters.Add(skeleton => skeleton.Lines, 4));

        var region = cut.Find(".t-skeleton");
        Assert.Equal("status", region.GetAttribute("role"));
        Assert.Equal("Loading…", region.GetAttribute("aria-label"));
        var shapes = cut.FindAll(".t-skeleton__shape");
        Assert.Equal(4, shapes.Count);
        Assert.All(shapes, shape => Assert.Equal("true", shape.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void Render_Paragraph_EndsOnAShortLineTheWayRealTextDoes()
    {
        var shapes = Render<TSkeleton>(parameters => parameters.Add(skeleton => skeleton.Lines, 3)).FindAll(".t-skeleton__shape");

        Assert.Null(shapes[0].GetAttribute("style"));
        Assert.Contains("width:60%", shapes[2].GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_SingleLine_IsFullWidth()
    {
        Assert.Null(Render<TSkeleton>().Find(".t-skeleton__shape").GetAttribute("style"));
    }

    [Theory]
    [InlineData(SkeletonShape.Text, "t-skeleton__shape--text")]
    [InlineData(SkeletonShape.Heading, "t-skeleton__shape--heading")]
    [InlineData(SkeletonShape.Block, "t-skeleton__shape--block")]
    [InlineData(SkeletonShape.Circle, "t-skeleton__shape--circle")]
    public void Render_Shape_UsesItsClass(SkeletonShape shape, string expected)
    {
        Assert.Contains(expected, Render<TSkeleton>(parameters => parameters.Add(skeleton => skeleton.Shape, shape))
            .Find(".t-skeleton__shape").ClassList);
    }

    [Fact]
    public void Render_German_AnnouncesLoadingInGerman()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Wird geladen …", Render<TSkeleton>().Find(".t-skeleton").GetAttribute("aria-label"));
    }
}

public class TEmptyStateTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsAHeadingThatSaysWhatBelongsHere()
    {
        var cut = Render<TEmptyState>(parameters => parameters.Add(state => state.Title, "No test cases yet"));

        Assert.Equal("H3", cut.Find(".t-empty-state__title").TagName);
        Assert.Equal("No test cases yet", cut.Find(".t-empty-state__title").TextContent);
    }

    [Fact]
    public void Render_TextAndAction_ShowsWhyItIsEmptyAndHowToFillIt()
    {
        var cut = Render<TEmptyState>(parameters => parameters
            .Add(state => state.Title, "No test cases yet")
            .AddChildContent("Generate them from a requirement, or add one by hand.")
            .Add(state => state.Action, "<button>Generate</button>")
            .Add(state => state.Icon, IconName.Document));

        Assert.Contains("Generate them from a requirement", cut.Find(".t-empty-state__text").TextContent, StringComparison.Ordinal);
        Assert.Equal("Generate", cut.Find(".t-empty-state__action button").TextContent);
        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }
}

public class TErrorStateTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsAnAlertWithAGeneralMessage()
    {
        var cut = Render<TErrorState>();

        Assert.Equal("alert", cut.Find(".t-error-state").GetAttribute("role"));
        Assert.Equal("Could not load this", cut.Find(".t-error-state__title").TextContent);
        Assert.Contains("Try again in a moment", cut.Find(".t-error-state__text").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithoutRetry_OffersNoRetry()
    {
        // Some failures cannot be retried; offering it would be a lie.
        Assert.Empty(Render<TErrorState>().FindAll("button"));
    }

    [Fact]
    public void Render_Retry_OffersItAndRaisesTheCallback()
    {
        var retries = 0;
        var cut = Render<TErrorState>(parameters => parameters.Add(state => state.OnRetry, () => retries++));

        cut.Find("button").Click();

        Assert.Equal(1, retries);
    }

    [Fact]
    public void Render_Details_AreHiddenBehindADisclosure()
    {
        // The first line is for everyone; the stack trace is for the people who want it.
        var cut = Render<TErrorState>(parameters => parameters.Add(state => state.Details, "HTTP 503 from the run service"));

        Assert.Equal("DETAILS", cut.Find("details").TagName);
        Assert.Contains("HTTP 503", cut.Find("details pre").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_German_UsesGermanText()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Konnte nicht geladen werden", Render<TErrorState>().Find(".t-error-state__title").TextContent);
    }
}

public class TAsyncViewTests : DesignSystemTestContext
{
    private IRenderedComponent<TAsyncView> RenderView(Action<ComponentParameterCollectionBuilder<TAsyncView>>? configure = null) =>
        Render<TAsyncView>(parameters =>
        {
            parameters.AddChildContent("<table id=\"data\"></table>");
            parameters.Add(view => view.EmptyContent, "<p id=\"empty\">No runs yet</p>");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Data_ShowsOnlyTheData()
    {
        var cut = RenderView();

        Assert.NotNull(cut.Find("#data"));
        Assert.Empty(cut.FindAll("#empty"));
        Assert.Empty(cut.FindAll(".t-skeleton"));
    }

    [Fact]
    public void Render_Loading_ShowsASkeletonInsteadOfTheData()
    {
        var cut = RenderView(parameters => parameters.Add(view => view.Loading, true));

        Assert.NotNull(cut.Find(".t-skeleton"));
        Assert.Empty(cut.FindAll("#data"));
    }

    [Fact]
    public void Render_CustomLoading_ReplacesTheDefaultSkeleton()
    {
        var cut = RenderView(parameters => parameters
            .Add(view => view.Loading, true)
            .Add(view => view.LoadingContent, "<div id=\"custom\"></div>"));

        Assert.NotNull(cut.Find("#custom"));
        Assert.Empty(cut.FindAll(".t-skeleton"));
    }

    [Fact]
    public void Render_Empty_ShowsTheEmptyState()
    {
        var cut = RenderView(parameters => parameters.Add(view => view.IsEmpty, true));

        Assert.Equal("No runs yet", cut.Find("#empty").TextContent);
        Assert.Empty(cut.FindAll("#data"));
    }

    [Fact]
    public void Render_Error_WinsOverEmptyAndShowsTheMessage()
    {
        // Failing to load is not the same as having nothing, and must not look like it.
        var cut = RenderView(parameters => parameters
            .Add(view => view.Error, "The run service did not answer")
            .Add(view => view.IsEmpty, true));

        Assert.Contains("The run service did not answer", cut.Find(".t-error-state").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("#empty"));
    }

    [Fact]
    public void Render_LoadingAndError_ShowsLoadingBecauseItIsStillTrying()
    {
        var cut = RenderView(parameters => parameters
            .Add(view => view.Loading, true)
            .Add(view => view.Error, "Earlier failure"));

        Assert.NotNull(cut.Find(".t-skeleton"));
        Assert.Empty(cut.FindAll(".t-error-state"));
    }

    [Fact]
    public void Render_Stale_SaysWhenTheFiguresAreFromAndStillShowsThem()
    {
        var cut = RenderView(parameters => parameters.Add(view => view.StaleSince, Now.AddHours(-2)));

        Assert.NotNull(cut.Find("#data"));
        Assert.Contains("These figures are from", cut.Find(".t-async__stale").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Fresh_ShowsNoStaleNotice()
    {
        Assert.Empty(RenderView().FindAll(".t-async__stale"));
    }
}
