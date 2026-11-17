using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Platform.Features;
using Autestiq.Platform.Localization;
using Autestiq.Platform.Navigation;
using Autestiq.UI.Shared.Layouts;
using Autestiq.UI.Shared.Resources;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Layouts;

/// <summary>The shell's navigation, drawn from what modules registered (Milestone 5).</summary>
public class AppNavTests : DesignSystemTestContext
{
    // Labels from the shared resources, so the test needs no module: any registered resource type works the same.
    private static NavItem Item(string id, NavGroup group, int order, string? flag = null, string label = "App_Nav_Group_Report") =>
        new(id, LocalizedText.From<SharedResources>(label), $"/app/{id}", "Folder", group, order, flag);

    private IRenderedComponent<AppNav> RenderWith(IFeatureFlags flags, params NavItem[] items)
    {
        var registry = new NavigationRegistry();

        foreach (var item in items)
        {
            registry.Add(item);
        }

        Services.AddSingleton<INavigationRegistry>(registry);
        Services.AddSingleton(flags);
        return Render<AppNav>();
    }

    [Fact]
    public void Render_RegisteredItems_GroupedByStage_InTheirOrder_WithTheirLabels()
    {
        var cut = RenderWith(
            new SwitchedFlags(),
            Item("runs", NavGroup.Run, 10),
            Item("projects", NavGroup.Overview, 10, label: "App_Nav_Group_Plan"),
            Item("defects", NavGroup.Run, 5));

        var headings = cut.FindAll(".app-nav__heading").Select(heading => heading.TextContent).ToList();
        Assert.Equal(["Overview", "Run"], headings);

        var links = cut.FindAll("a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Equal(["/app/projects", "/app/defects", "/app/runs"], links);
        Assert.Equal("Plan", cut.Find("[data-testid='nav-projects']").TextContent.Trim());
    }

    [Fact]
    public void Render_AnItemWhoseFlagIsOff_IsNotShown()
    {
        var cut = RenderWith(new SwitchedFlags("Module.Defects"), Item("runs", NavGroup.Run, 1, "Module.Execution"), Item("defects", NavGroup.Run, 2, "Module.Defects"));

        Assert.Single(cut.FindAll("a"));
        Assert.Empty(cut.FindAll("[data-testid='nav-defects']"));
    }

    [Fact]
    public void Render_NothingSwitchedOn_SaysSo()
    {
        var cut = RenderWith(new SwitchedFlags("Module.Execution"), Item("runs", NavGroup.Run, 1, "Module.Execution"));

        Assert.Equal("Modules appear here once they are switched on.", cut.Find(".app-nav__empty").TextContent);
    }

    [Fact]
    public void Render_EachGroup_IsAListNamedByItsHeading()
    {
        var cut = RenderWith(new SwitchedFlags(), Item("runs", NavGroup.Run, 1));

        var list = cut.Find("ul");
        var heading = cut.Find(".app-nav__heading");
        Assert.Equal(heading.Id, list.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Registry_TheSameIdTwice_IsRefused()
    {
        var registry = new NavigationRegistry();
        registry.Add(Item("runs", NavGroup.Run, 1));

        Assert.Throws<InvalidOperationException>(() => registry.Add(Item("RUNS", NavGroup.Run, 2)));
    }

    [Fact]
    public void Render_InGerman_HeadsTheGroupsInGerman()
    {
        using var german = new CultureScope("de");

        var cut = RenderWith(new SwitchedFlags(), Item("runs", NavGroup.Run, 1));

        Assert.Equal("Ausführen", cut.Find(".app-nav__heading").TextContent);
    }
}
