using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TChoiceFormTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<ChoiceOption> TwoOptions =
    [
        new("a", "Option A", IsCurrent: true),
        new("b", "Option B", IsCurrent: false),
    ];

    private IRenderedComponent<TChoiceForm> RenderForm(
        IReadOnlyList<ChoiceOption>? options = null,
        ChoiceState state = ChoiceState.Pressed) =>
        Render<TChoiceForm>(parameters => parameters
            .Add(component => component.Action, "/preferences/sample")
            .Add(component => component.FieldName, "choice")
            .Add(component => component.Label, "Sample choice")
            .Add(component => component.Options, options ?? TwoOptions)
            .Add(component => component.State, state));

    [Fact]
    public void Render_Default_IsAPlainPostFormThatWorksWithoutJavaScript()
    {
        var form = RenderForm().Find("form");

        Assert.Equal("post", form.GetAttribute("method"));
        Assert.Equal("/preferences/sample", form.GetAttribute("action"));
        Assert.All(form.QuerySelectorAll("button"), button => Assert.Equal("submit", button.GetAttribute("type")));
    }

    [Fact]
    public void Render_Default_SendsTheCurrentPageAsReturnAddress()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/de/pricing?plan=team");

        var returnUrl = RenderForm().Find("input[name=returnUrl]");

        Assert.Equal("hidden", returnUrl.GetAttribute("type"));
        Assert.Equal("/de/pricing?plan=team", returnUrl.GetAttribute("value"));
    }

    [Fact]
    public void Navigate_ToAnotherPage_UpdatesTheReturnAddress()
    {
        var cut = RenderForm();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/app/settings");

        cut.WaitForAssertion(() => Assert.Equal("/app/settings", cut.Find("input[name=returnUrl]").GetAttribute("value")));
    }

    [Fact]
    public void Render_Options_BecomeNamedButtonsInAGroup()
    {
        var cut = RenderForm();

        var group = cut.Find("[role=group]");
        Assert.Equal("Sample choice", group.GetAttribute("aria-label"));

        var buttons = group.QuerySelectorAll("button");
        Assert.Equal(["a", "b"], buttons.Select(button => button.GetAttribute("value")));
        Assert.All(buttons, button => Assert.Equal("choice", button.GetAttribute("name")));
        Assert.Equal(["Option A", "Option B"], buttons.Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void Render_PressedState_MarksEveryButtonAsPressedOrNot()
    {
        var buttons = RenderForm(state: ChoiceState.Pressed).FindAll("button");

        Assert.Equal(["true", "false"], buttons.Select(button => button.GetAttribute("aria-pressed")));
        Assert.All(buttons, button => Assert.False(button.HasAttribute("aria-current")));
    }

    [Fact]
    public void Render_CurrentState_MarksOnlyTheOptionInEffect()
    {
        var buttons = RenderForm(state: ChoiceState.Current).FindAll("button");

        Assert.Equal("true", buttons[0].GetAttribute("aria-current"));
        Assert.False(buttons[1].HasAttribute("aria-current"));
        Assert.All(buttons, button => Assert.False(button.HasAttribute("aria-pressed")));
    }

    [Fact]
    public void Render_ShortText_KeepsTheFullNameForScreenReaders()
    {
        var button = RenderForm([new("de", "Deutsch", IsCurrent: false, ShortText: "DE", Lang: "de")]).Find("button");

        Assert.Equal("de", button.GetAttribute("lang"));
        Assert.Equal("DE", button.QuerySelector("[aria-hidden=true]")?.TextContent);
        Assert.Equal("Deutsch", button.QuerySelector(".t-visually-hidden")?.TextContent);
    }

    [Fact]
    public void Render_Icon_KeepsTheFullNameForScreenReaders()
    {
        RenderFragment icon = builder => builder.AddMarkupContent(0, "<svg aria-hidden=\"true\"></svg>");

        var button = RenderForm([new("dark", "Dark", IsCurrent: false, Icon: icon)]).Find("button");

        Assert.NotNull(button.QuerySelector("svg[aria-hidden=true]"));
        Assert.Equal("Dark", button.QuerySelector(".t-visually-hidden")?.TextContent);
    }

    [Fact]
    public void Render_ExtraAttributes_ArePassedToTheForm()
    {
        var form = Render<TChoiceForm>(parameters => parameters
            .Add(component => component.Action, "/x")
            .Add(component => component.FieldName, "choice")
            .Add(component => component.Label, "Sample choice")
            .Add(component => component.Options, TwoOptions)
            .AddUnmatched("data-sample", "yes")).Find("form");

        Assert.Equal("yes", form.GetAttribute("data-sample"));
    }
}
