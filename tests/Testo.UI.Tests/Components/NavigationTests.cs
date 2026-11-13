using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TTabsTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<TTab> Views =
    [
        new("list", "List"),
        new("board", "Board", IconName.Folder),
        new("open", "Open", Count: 12),
    ];

    private IRenderedComponent<TTabs> RenderTabs(string selected = "list", Action<string>? onSelect = null) =>
        Render<TTabs>(parameters => parameters
            .Add(tabs => tabs.Label, "Defect views")
            .Add(tabs => tabs.Tabs, Views)
            .Add(tabs => tabs.Selected, selected)
            .Add(tabs => tabs.SelectedChanged, id => onSelect?.Invoke(id))
            .AddChildContent("<p id=\"panel-content\">Rows</p>"));

    [Fact]
    public void Render_Default_FollowsTheTabsPattern()
    {
        var cut = RenderTabs();

        Assert.Equal("Defect views", cut.Find("[role=tablist]").GetAttribute("aria-label"));
        Assert.Equal(3, cut.FindAll("[role=tab]").Count);
        var panel = cut.Find("[role=tabpanel]");
        Assert.Equal(cut.Find("[aria-selected=true]").Id, panel.GetAttribute("aria-labelledby"));
        Assert.Equal(panel.Id, cut.Find("[aria-selected=true]").GetAttribute("aria-controls"));
    }

    [Fact]
    public void Render_Always_KeepsOnlyTheSelectedTabInTheTabOrder()
    {
        // Tab moves out of the strip; the arrow keys move within it.
        var tabs = RenderTabs("board").FindAll("[role=tab]");

        Assert.Equal(["-1", "0", "-1"], tabs.Select(tab => tab.GetAttribute("tabindex")));
    }

    [Fact]
    public void Render_OnlyTheSelectedPanel_IsInTheDocument()
    {
        var cut = RenderTabs();

        Assert.Single(cut.FindAll("[role=tabpanel]"));
        Assert.Equal("Rows", cut.Find("#panel-content").TextContent);
    }

    [Theory]
    [InlineData("ArrowRight", "board")]
    [InlineData("ArrowDown", "board")]
    [InlineData("ArrowLeft", "open")]
    [InlineData("End", "open")]
    [InlineData("Home", "list")]
    public void KeyDown_ArrowKeys_MoveBetweenTabsAndWrapAround(string key, string expected)
    {
        string? selected = null;
        var cut = RenderTabs("list", id => selected = id);

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(expected == "list" ? null : expected, selected);
    }

    [Fact]
    public void KeyDown_OtherKey_ChangesNothing()
    {
        string? selected = null;
        var cut = RenderTabs("list", id => selected = id);

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Null(selected);
    }

    [Fact]
    public void Click_Tab_SelectsIt()
    {
        string? selected = null;
        var cut = RenderTabs("list", id => selected = id);

        cut.FindAll("[role=tab]")[2].Click();

        Assert.Equal("open", selected);
    }

    [Fact]
    public void Click_TheSelectedTab_RaisesNothing()
    {
        var raised = 0;
        var cut = RenderTabs("list", _ => raised++);

        cut.FindAll("[role=tab]")[0].Click();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Render_Count_IsShownBesideTheLabel()
    {
        Assert.Equal("12", RenderTabs().Find(".t-tabs__count").TextContent);
    }
}

public class TBreadcrumbsTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<TBreadcrumb> Trail =
    [
        new("Orion", "/app/p/orion"),
        new("Defects", "/app/p/orion/defects"),
        new("DEF-734"),
    ];

    [Fact]
    public void Render_Default_IsANavigationLandmarkWithAnOrderedList()
    {
        var cut = Render<TBreadcrumbs>(parameters => parameters.Add(crumbs => crumbs.Items, Trail));

        Assert.Equal("Breadcrumb", cut.Find("nav").GetAttribute("aria-label"));
        Assert.Equal("OL", cut.Find("ol").TagName);
        Assert.Equal(3, cut.FindAll("li").Count);
    }

    [Fact]
    public void Render_LastCrumb_IsTheCurrentPageAndNotALink()
    {
        // Linking to where you already are is noise.
        var cut = Render<TBreadcrumbs>(parameters => parameters.Add(crumbs => crumbs.Items, Trail));

        Assert.Equal(2, cut.FindAll("a").Count);
        var current = cut.Find("[aria-current=page]");
        Assert.Equal("DEF-734", current.TextContent);
        Assert.Equal("SPAN", current.TagName);
    }

    [Fact]
    public void Render_Separators_AreDecorativeAndSitBetweenCrumbsOnly()
    {
        var cut = Render<TBreadcrumbs>(parameters => parameters.Add(crumbs => crumbs.Items, Trail));

        var separators = cut.FindAll("svg");
        Assert.Equal(2, separators.Count);
        Assert.All(separators, separator => Assert.Equal("true", separator.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void Render_German_NamesTheTrailInGerman()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Navigationspfad", Render<TBreadcrumbs>(parameters => parameters.Add(crumbs => crumbs.Items, Trail))
            .Find("nav").GetAttribute("aria-label"));
    }
}

public class TPaginationTests : DesignSystemTestContext
{
    private IRenderedComponent<TPagination> RenderPages(int pageIndex, int total, Action<int>? onChange = null) =>
        Render<TPagination>(parameters => parameters
            .Add(pages => pages.PageIndex, pageIndex)
            .Add(pages => pages.PageSize, 20)
            .Add(pages => pages.TotalCount, total)
            .Add(pages => pages.PageIndexChanged, index => onChange?.Invoke(index)));

    [Fact]
    public void Render_Always_SaysWhichRowsAreOnScreen()
    {
        // "41 to 60 of 237" tells a person more than "page 3 of 12".
        Assert.Equal("41 to 60 of 237", RenderPages(2, 237).Find(".t-pagination__status").TextContent);
    }

    [Fact]
    public void Render_LastPage_StopsAtTheRealTotal()
    {
        Assert.Equal("221 to 237 of 237", RenderPages(11, 237).Find(".t-pagination__status").TextContent);
    }

    [Fact]
    public void Render_Empty_SaysZeroRatherThanShowingAnImpossibleRange()
    {
        Assert.Equal("0 to 0 of 0", RenderPages(0, 0).Find(".t-pagination__status").TextContent);
    }

    [Fact]
    public void Render_Status_IsAnnouncedAfterThePageChanges()
    {
        Assert.Equal("polite", RenderPages(0, 100).Find(".t-pagination__status").GetAttribute("aria-live"));
    }

    [Fact]
    public void Render_FirstPage_CannotGoBack()
    {
        var buttons = RenderPages(0, 100).FindAll("button");

        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Render_LastPage_CannotGoForward()
    {
        var buttons = RenderPages(4, 100).FindAll("button");

        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.True(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Click_Next_AsksForTheNextPage()
    {
        int? asked = null;
        var cut = RenderPages(1, 100, index => asked = index);

        cut.FindAll("button")[1].Click();

        Assert.Equal(2, asked);
    }

    [Fact]
    public void Render_Buttons_AreNamedForScreenReaders()
    {
        var buttons = RenderPages(1, 100).FindAll("button");

        Assert.Equal("Previous page", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next page", buttons[1].GetAttribute("aria-label"));
    }
}

public class TConfirmTests : DesignSystemTestContext
{
    private IRenderedComponent<TConfirm> RenderConfirm(Action<ComponentParameterCollectionBuilder<TConfirm>>? configure = null) =>
        Render<TConfirm>(parameters =>
        {
            parameters.Add(confirm => confirm.Title, "Delete test suite Checkout — regression?");
            parameters.Add(confirm => confirm.ConfirmLabel, "Delete suite");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Default_IsAModalAlertNamedByItsQuestion()
    {
        var cut = RenderConfirm();

        var dialog = cut.Find("[role=alertdialog]");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal(cut.Find(".t-confirm__title").Id, dialog.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find(".t-confirm__body").Id, dialog.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Render_Buttons_AreTheVerbAndTheWayOut()
    {
        // Never "OK": a person reading only the buttons still knows what happens.
        var buttons = RenderConfirm().FindAll("button");

        Assert.Equal("Cancel", buttons[0].TextContent.Trim());
        Assert.Equal("Delete suite", buttons[1].TextContent.Trim());
    }

    [Fact]
    public void Render_Destructive_MakesTheConfirmingButtonADangerButton()
    {
        Assert.Contains("t-button--danger", RenderConfirm().FindAll("button")[1].ClassList);
    }

    [Fact]
    public void Render_NotDestructive_MakesItAPrimaryButton()
    {
        Assert.Contains("t-button--primary", RenderConfirm(parameters => parameters
            .Add(confirm => confirm.Destructive, false)).FindAll("button")[1].ClassList);
    }

    [Fact]
    public void Render_Consequence_SaysWhatElseIsAffected()
    {
        var cut = RenderConfirm(parameters => parameters
            .Add(confirm => confirm.Consequence, "47 test cases stay; their membership of this suite is removed."));

        Assert.Contains("47 test cases stay", cut.Find(".t-confirm__consequence").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Click_Confirm_RaisesOnConfirmOnly()
    {
        var confirmed = 0;
        var cancelled = 0;
        var cut = RenderConfirm(parameters => parameters
            .Add(confirm => confirm.OnConfirm, () => confirmed++)
            .Add(confirm => confirm.OnCancel, () => cancelled++));

        cut.FindAll("button")[1].Click();

        Assert.Equal(1, confirmed);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public void Render_Working_StopsASecondConfirmation()
    {
        var buttons = RenderConfirm(parameters => parameters.Add(confirm => confirm.Working, true)).FindAll("button");

        Assert.True(buttons[1].HasAttribute("disabled"));
        Assert.Equal("true", buttons[1].GetAttribute("aria-busy"));
    }

    [Fact]
    public void Render_German_UsesTheGermanWayOut()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Abbrechen", RenderConfirm().FindAll("button")[0].TextContent.Trim());
    }
}
