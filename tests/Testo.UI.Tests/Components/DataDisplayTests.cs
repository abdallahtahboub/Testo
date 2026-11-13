using Bunit;
using Testo.Contracts.Testing;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TSparklineTests : DesignSystemTestContext
{
    [Fact]
    public void Render_FewerThanTwoValues_DrawsNothing()
    {
        Assert.Empty(Render<TSparkline>(parameters => parameters.Add(line => line.Values, [5d])).FindAll("svg"));
    }

    [Fact]
    public void Render_Values_DrawsOnePointEachAndMarksTheLatest()
    {
        var cut = Render<TSparkline>(parameters => parameters.Add(line => line.Values, [1d, 5d, 3d, 9d]));

        var points = cut.Find("polyline").GetAttribute("points")!.Split(' ');
        Assert.Equal(4, points.Length);
        Assert.NotNull(cut.Find("circle"));
    }

    [Fact]
    public void Render_FlatValues_StaysOnOneLineInsteadOfDividingByZero()
    {
        var cut = Render<TSparkline>(parameters => parameters.Add(line => line.Values, [4d, 4d, 4d]));

        var ys = cut.Find("polyline").GetAttribute("points")!.Split(' ').Select(point => point.Split(',')[1]).Distinct();

        Assert.Single(ys);
    }

    [Fact]
    public void Render_Always_IsDecorativeBecauseTheFigureCarriesTheMeaning()
    {
        Assert.Equal("true", Render<TSparkline>(parameters => parameters.Add(line => line.Values, [1d, 2d])).Find("svg").GetAttribute("aria-hidden"));
    }
}

public class TKpiTileTests : DesignSystemTestContext
{
    private IRenderedComponent<TKpiTile> RenderTile(Action<ComponentParameterCollectionBuilder<TKpiTile>>? configure = null) =>
        Render<TKpiTile>(parameters =>
        {
            parameters.Add(tile => tile.Label, "Open defects");
            parameters.Add(tile => tile.Value, "42");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Default_ShowsTheLabelAndTheValue()
    {
        var cut = RenderTile();

        Assert.Equal("Open defects", cut.Find(".t-kpi__label").TextContent);
        Assert.Equal("42", cut.Find(".t-kpi__value").TextContent);
        Assert.Empty(cut.FindAll(".t-kpi__delta"));
    }

    [Fact]
    public void Render_Href_MakesTheWholeTileALinkToWhereTheNumberComesFrom()
    {
        // Mara has to defend these numbers, so every one of them is traceable.
        Assert.Equal("/app/p/orion/defects", RenderTile(parameters => parameters
            .Add(tile => tile.Href, "/app/p/orion/defects")).Find("a").GetAttribute("href"));
    }

    [Theory]
    // More open defects is a rise, and bad news.
    [InlineData(0.12, false, "t-kpi__delta--bad", "ArrowUp")]
    [InlineData(-0.12, false, "t-kpi__delta--good", "ArrowDown")]
    // A higher pass rate is a rise, and good news.
    [InlineData(0.12, true, "t-kpi__delta--good", "ArrowUp")]
    [InlineData(-0.12, true, "t-kpi__delta--bad", "ArrowDown")]
    public void Render_Delta_ColoursByWhetherItIsGoodNewsNotByDirection(double delta, bool higherIsBetter, string expectedClass, string expectedDirection)
    {
        var cut = RenderTile(parameters => parameters
            .Add(tile => tile.Delta, delta)
            .Add(tile => tile.HigherIsBetter, higherIsBetter));

        Assert.Contains(expectedClass, cut.Find(".t-kpi__delta").ClassList);
        // The arrow still points the way the number moved.
        var expectedUp = expectedDirection == "ArrowUp";
        Assert.Equal(expectedUp, cut.Markup.Contains("M10 16.25V4.25", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_NoChange_IsNeutral()
    {
        Assert.Contains("t-kpi__delta--neutral", RenderTile(parameters => parameters.Add(tile => tile.Delta, 0d)).Find(".t-kpi__delta").ClassList);
    }

    [Fact]
    public void Render_Delta_IsShownAsAPercentageWithoutItsSign()
    {
        // The arrow carries the direction; a minus sign beside a down arrow says it twice.
        Assert.Contains("12", RenderTile(parameters => parameters.Add(tile => tile.Delta, -0.12)).Find(".t-kpi__delta").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("-", RenderTile(parameters => parameters.Add(tile => tile.Delta, -0.12)).Find(".t-kpi__delta").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Trend_DrawsASparkline()
    {
        Assert.NotNull(RenderTile(parameters => parameters.Add(tile => tile.Trend, [1d, 4d, 2d, 7d])).Find("polyline"));
    }
}

public class TTimelineTests : DesignSystemTestContext
{
    private IRenderedComponent<TTimeline> RenderTimeline() =>
        Render<TTimeline>(parameters => parameters.AddChildContent<TTimelineItem>(step => step
                .Add(item => item.Title, "Open the home page")
                .Add(item => item.Status, TestStatus.Passed)
                .Add(item => item.Duration, TimeSpan.FromSeconds(1.4)))
            .AddChildContent<TTimelineItem>(step => step
                .Add(item => item.Title, "Add to basket")
                .Add(item => item.Status, TestStatus.Running)
                .Add(item => item.IsCurrent, true)));

    [Fact]
    public void Render_Steps_AreAnOrderedListBecauseTheOrderIsTheMeaning()
    {
        var cut = RenderTimeline();

        Assert.Equal("OL", cut.Find(".t-timeline").TagName);
        Assert.Equal(2, cut.FindAll("li").Count);
    }

    [Fact]
    public void Render_CurrentStep_IsMarkedForScreenReadersToo()
    {
        var current = RenderTimeline().FindAll("li")[1];

        Assert.Equal("step", current.GetAttribute("aria-current"));
        Assert.Contains("t-timeline__item--current", current.ClassList);
    }

    [Fact]
    public void Render_Status_IsSaidInWordsNotOnlyDrawn()
    {
        var first = RenderTimeline().FindAll("li")[0];

        Assert.Equal("Passed", first.QuerySelector(".t-visually-hidden")?.TextContent);
        Assert.NotNull(first.QuerySelector("svg"));
    }

    [Fact]
    public void Render_Duration_IsReadableRatherThanPrecise()
    {
        Assert.Equal("1.4 s", RenderTimeline().Find(".t-timeline__duration").TextContent);
    }

    [Fact]
    public void Render_LongDuration_IsShownInMinutesAndSeconds()
    {
        var cut = Render<TTimeline>(parameters => parameters.AddChildContent<TTimelineItem>(step => step
            .Add(item => item.Title, "Full regression")
            .Add(item => item.Status, TestStatus.Passed)
            .Add(item => item.Duration, TimeSpan.FromSeconds(185))));

        Assert.Equal("3 min 5 s", cut.Find(".t-timeline__duration").TextContent);
    }

    [Fact]
    public void Render_OutsideATimeline_FailsLoudly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Render<TTimelineItem>(parameters => parameters
                .Add(item => item.Title, "Orphan")
                .Add(item => item.Status, TestStatus.Passed)));

        Assert.Contains(nameof(TTimeline), exception.Message, StringComparison.Ordinal);
    }
}

public class TBarChartTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<ChartPoint> Severities =
    [
        new("Critical", 3, Tone.Danger),
        new("Major", 11, Tone.Warning),
        new("Minor", 24, Tone.Info),
    ];

    private IRenderedComponent<TBarChart> RenderChart(Action<ComponentParameterCollectionBuilder<TBarChart>>? configure = null) =>
        Render<TBarChart>(parameters =>
        {
            parameters.Add(chart => chart.Title, "Open defects by severity");
            parameters.Add(chart => chart.Points, Severities);
            parameters.Add(chart => chart.CategoryHeader, "Severity");
            parameters.Add(chart => chart.ValueHeader, "Defects");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Always_LabelsEveryBarWithItsNameAndValue()
    {
        // The chart has to be readable without counting gridlines, and without colour.
        var cut = RenderChart();

        Assert.Equal(["Critical", "Major", "Minor"], cut.FindAll(".t-chart__row-label").Select(label => label.TextContent));
        Assert.Equal(["3", "11", "24"], cut.FindAll(".t-chart__row-value").Select(value => value.TextContent));
    }

    [Fact]
    public void Render_Always_CarriesItsNumbersAsATableForScreenReaders()
    {
        var cut = RenderChart();

        var table = cut.Find("table");
        Assert.Contains("t-visually-hidden", table.ClassList);
        Assert.Equal("Open defects by severity", cut.Find("caption").TextContent);
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Equal("Severity", cut.FindAll("thead th")[0].TextContent);
    }

    [Fact]
    public void Render_Bars_AreScaledToTheLargestValue()
    {
        var cut = RenderChart();

        // The longest bar reaches the full width; the shortest is an eighth of it.
        var paths = cut.FindAll("path").Select(path => path.GetAttribute("d")!).ToList();
        Assert.Contains("H96", paths[2], StringComparison.Ordinal);
        Assert.DoesNotContain("H96", paths[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Maximum_ScalesToItSoChartsCanBeComparedWithEachOther()
    {
        var cut = RenderChart(parameters => parameters.Add(chart => chart.Maximum, 100));

        // 24 of 100 is a quarter, not the full width.
        Assert.DoesNotContain("H96", cut.FindAll("path")[2].GetAttribute("d")!, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ZeroValue_DrawsNoBarRatherThanAStub()
    {
        var cut = Render<TBarChart>(parameters => parameters
            .Add(chart => chart.Title, "Runs")
            .Add(chart => chart.Points, [new ChartPoint("None", 0)])
            .Add(chart => chart.CategoryHeader, "Day")
            .Add(chart => chart.ValueHeader, "Runs"));

        Assert.Equal("", cut.Find("path").GetAttribute("d"));
    }

    [Fact]
    public void Render_Tone_ColoursTheBarByItsMeaning()
    {
        Assert.Contains("t-chart__bar--danger", RenderChart().FindAll("svg")[0].ClassList);
    }

    [Fact]
    public void Render_WithoutTone_UsesOneHueForEveryBar()
    {
        // Nominal categories do not each deserve their own colour.
        var cut = Render<TBarChart>(parameters => parameters
            .Add(chart => chart.Title, "Runs per day")
            .Add(chart => chart.Points, [new ChartPoint("Mon", 4), new ChartPoint("Tue", 9)])
            .Add(chart => chart.CategoryHeader, "Day")
            .Add(chart => chart.ValueHeader, "Runs"));

        Assert.All(cut.FindAll("svg"), svg => Assert.Contains("t-chart__bar--brand", svg.ClassList));
    }
}

public class TSegmentedBarTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<ChartPoint> Outcomes =
    [
        new("Passed", 47, Tone.Success),
        new("Failed", 3, Tone.Danger),
        new("Blocked", 0, Tone.Warning),
    ];

    private IRenderedComponent<TSegmentedBar> RenderBar(Action<ComponentParameterCollectionBuilder<TSegmentedBar>>? configure = null) =>
        Render<TSegmentedBar>(parameters =>
        {
            parameters.Add(bar => bar.Title, "Tests in this run");
            parameters.Add(bar => bar.Points, Outcomes);
            parameters.Add(bar => bar.CategoryHeader, "Outcome");
            parameters.Add(bar => bar.ValueHeader, "Tests");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Parts_AreSizedByTheirShareOfTheWhole()
    {
        var parts = RenderBar().FindAll(".t-segmented__part");

        // 47 and 3 of 50; the empty part is left out entirely.
        Assert.Equal(2, parts.Count);
        Assert.Contains("flex-grow:0.94", parts[0].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("flex-grow:0.06", parts[1].GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_SmallPart_HasNoNumberPrintedOnItBecauseItWouldNotFit()
    {
        var parts = RenderBar().FindAll(".t-segmented__part");

        Assert.NotNull(parts[0].QuerySelector(".t-segmented__part-value"));
        Assert.Null(parts[1].QuerySelector(".t-segmented__part-value"));
    }

    [Fact]
    public void Render_Legend_NamesEveryPartSoColourIsNeverTheOnlySignal()
    {
        var items = RenderBar().FindAll(".t-segmented__legend-item");

        Assert.Equal(2, items.Count);
        Assert.Contains("Passed", items[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Thin_CarriesNoTextBecauseTheRowsBesideItDo()
    {
        var cut = RenderBar(parameters => parameters
            .Add(bar => bar.Size, SegmentedBarSize.Thin)
            .Add(bar => bar.ShowLegend, false));

        Assert.Empty(cut.FindAll(".t-segmented__part-value"));
        Assert.Empty(cut.FindAll(".t-segmented__legend"));
    }

    [Fact]
    public void Render_Always_HidesTheBarFromScreenReadersAndGivesThemTheTable()
    {
        var cut = RenderBar();

        Assert.Equal("true", cut.Find(".t-segmented__bar").GetAttribute("aria-hidden"));
        // The table keeps every part, including the empty one.
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public void Render_NoData_DrawsNothingRatherThanDividingByZero()
    {
        var cut = Render<TSegmentedBar>(parameters => parameters
            .Add(bar => bar.Title, "Tests")
            .Add(bar => bar.Points, [new ChartPoint("Passed", 0, Tone.Success)])
            .Add(bar => bar.CategoryHeader, "Outcome")
            .Add(bar => bar.ValueHeader, "Tests"));

        Assert.Empty(cut.FindAll(".t-segmented__part"));
    }
}
