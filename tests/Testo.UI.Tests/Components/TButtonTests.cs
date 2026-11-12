using Bunit;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TButtonTests : DesignSystemTestContext
{
    private IRenderedComponent<TButton> RenderButton(Action<ComponentParameterCollectionBuilder<TButton>>? configure = null) =>
        Render<TButton>(parameters =>
        {
            parameters.AddChildContent("Start run");
            configure?.Invoke(parameters);
        });

    // ---------- Which element it renders ----------

    [Fact]
    public void Render_WithoutHref_IsAButtonThatActs()
    {
        var button = RenderButton().Find("button");

        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Start run", button.TextContent);
    }

    [Fact]
    public void Render_WithHref_IsALinkThatNavigates()
    {
        var cut = RenderButton(parameters => parameters.Add(button => button.Href, "/app"));

        Assert.Empty(cut.FindAll("button"));
        Assert.Equal("/app", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void Render_SubmitType_IsPassedThrough()
    {
        // A form on a statically rendered page needs a real submit button.
        Assert.Equal("submit", RenderButton(parameters => parameters.Add(button => button.Type, "submit")).Find("button").GetAttribute("type"));
    }

    // ---------- Variant and size ----------

    [Fact]
    public void Render_Default_IsSecondary()
    {
        // Prominence is opted into: a button is quiet unless it says otherwise.
        Assert.Contains("t-button--secondary", RenderButton().Find("button").ClassList);
    }

    [Theory]
    [InlineData(ButtonVariant.Primary, "t-button--primary")]
    [InlineData(ButtonVariant.Secondary, "t-button--secondary")]
    [InlineData(ButtonVariant.Ghost, "t-button--ghost")]
    [InlineData(ButtonVariant.Danger, "t-button--danger")]
    public void Render_Variant_UsesItsStyleAndNoOther(ButtonVariant variant, string expected)
    {
        var variants = new[] { "t-button--primary", "t-button--secondary", "t-button--ghost", "t-button--danger" };

        var classes = RenderButton(parameters => parameters.Add(button => button.Variant, variant)).Find("button").ClassList;

        Assert.Equal([expected], variants.Where(classes.Contains));
    }

    [Theory]
    [InlineData(ButtonSize.Small, "t-button--sm")]
    [InlineData(ButtonSize.Medium, "t-button--md")]
    [InlineData(ButtonSize.Large, "t-button--lg")]
    public void Render_Size_UsesItsClass(ButtonSize size, string expected)
    {
        Assert.Contains(expected, RenderButton(parameters => parameters.Add(button => button.Size, size)).Find("button").ClassList);
    }

    [Fact]
    public void Render_FullWidth_StretchesToItsContainer()
    {
        Assert.Contains("t-button--full", RenderButton(parameters => parameters.Add(button => button.FullWidth, true)).Find("button").ClassList);
    }

    // ---------- Icons ----------

    [Fact]
    public void Render_IconAndLabel_ShowsBothAndHidesTheIconFromScreenReaders()
    {
        var cut = RenderButton(parameters => parameters.Add(button => button.Icon, IconName.Play));

        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
        Assert.Equal("Start run", cut.Find("button").TextContent);
        Assert.DoesNotContain("t-button--icon-only", cut.Find("button").ClassList);
    }

    [Fact]
    public void Render_IconOnly_IsSquareAndNamedForScreenReaders()
    {
        var button = Render<TButton>(parameters => parameters
            .Add(component => component.Icon, IconName.Delete)
            .Add(component => component.AriaLabel, "Delete test case")).Find("button");

        Assert.Contains("t-button--icon-only", button.ClassList);
        Assert.Equal("Delete test case", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Render_IconOnlyWithoutAName_FailsLoudly()
    {
        // Silently shipping an unlabelled icon button is worse than not compiling.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Render<TButton>(parameters => parameters.Add(component => component.Icon, IconName.Delete)));

        Assert.Contains("AriaLabel", exception.Message, StringComparison.Ordinal);
    }

    // ---------- Unavailable ----------

    [Fact]
    public void Render_Disabled_CannotBeUsed()
    {
        Assert.True(RenderButton(parameters => parameters.Add(button => button.Disabled, true)).Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Render_Loading_ShowsASpinnerAndIsBusy()
    {
        var cut = RenderButton(parameters => parameters.Add(button => button.Loading, true));

        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-busy"));
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(cut.Find("svg").HasAttribute("data-spin"));
    }

    [Fact]
    public void Render_LoadingWithAnIcon_ShowsOnlyTheSpinner()
    {
        var cut = RenderButton(parameters => parameters
            .Add(button => button.Icon, IconName.Play)
            .Add(button => button.Loading, true));

        Assert.Single(cut.FindAll("svg"));
    }

    [Fact]
    public void Render_DisabledLink_IsNotFollowable()
    {
        // A link has no disabled attribute, so the href goes instead of being left to click.
        var link = Render<TButton>(parameters => parameters
            .Add(component => component.Href, "/app")
            .Add(component => component.Disabled, true)
            .AddChildContent("Open")).Find("a");

        Assert.False(link.HasAttribute("href"));
        Assert.Equal("true", link.GetAttribute("aria-disabled"));
    }

    // ---------- Behavior ----------

    [Fact]
    public void Click_Always_RaisesOnClick()
    {
        var clicks = 0;
        var cut = RenderButton(parameters => parameters.Add(button => button.OnClick, () => clicks++));

        cut.Find("button").Click();
        cut.Find("button").Click();

        Assert.Equal(2, clicks);
    }

    [Fact]
    public void Click_WhenDisabled_DoesNothing()
    {
        var clicks = 0;
        var cut = RenderButton(parameters => parameters
            .Add(button => button.Disabled, true)
            .Add(button => button.OnClick, () => clicks++));

        cut.Find("button").Click();

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void Render_ExtraAttributes_ArePassedThrough()
    {
        var button = RenderButton(parameters => parameters
            .AddUnmatched("data-testid", "start")
            .AddUnmatched("title", "Starts the selected suite")).Find("button");

        Assert.Equal("start", button.GetAttribute("data-testid"));
        Assert.Equal("Starts the selected suite", button.GetAttribute("title"));
    }
}
