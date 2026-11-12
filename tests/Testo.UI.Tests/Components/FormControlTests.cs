using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

/// <summary>
/// The rules of ux/patterns.md §2: a visible label, a hint before anything goes wrong, an
/// error that says what to do, and a control that is connected to both.
/// </summary>
public class TFieldTests : DesignSystemTestContext
{
    /// <summary>A bare input that does exactly what the field asks of it, and nothing else.</summary>
    private static readonly RenderFragment<TFieldContext> Control = context => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "id", context.ControlId);
        builder.AddAttribute(2, "aria-describedby", context.DescribedBy);
        builder.AddAttribute(3, "aria-invalid", context.IsInvalid ? "true" : null);
        builder.CloseElement();
    };

    private IRenderedComponent<TField> RenderField(Action<ComponentParameterCollectionBuilder<TField>>? configure = null) =>
        Render<TField>(parameters =>
        {
            parameters.Add(field => field.Label, "Environment");
            parameters.Add(field => field.ChildContent, Control);
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Default_HasAVisibleLabelPointingAtTheControl()
    {
        var cut = RenderField();

        var label = cut.Find("label");
        var input = cut.Find("input");
        Assert.Equal("Environment", label.TextContent.Trim());
        Assert.Equal(input.Id, label.GetAttribute("for"));
        Assert.False(string.IsNullOrEmpty(input.Id));
    }

    [Fact]
    public void Render_Hint_IsShownBeforeTheControlAndDescribesIt()
    {
        var cut = RenderField(parameters => parameters.Add(field => field.Hint, "Where the suite runs"));

        var hint = cut.Find(".t-field__hint");
        Assert.Equal("Where the suite runs", hint.TextContent);
        Assert.Equal(hint.Id, cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Render_Error_IsAnnouncedAndMarksTheControlInvalid()
    {
        var cut = RenderField(parameters => parameters.Add(field => field.Error, "Choose an environment"));

        var error = cut.Find(".t-field__error");
        Assert.Contains("Choose an environment", error.TextContent, StringComparison.Ordinal);
        Assert.Equal("polite", error.GetAttribute("aria-live"));
        Assert.Equal("true", cut.Find("input").GetAttribute("aria-invalid"));
        Assert.Contains("t-field--invalid", cut.Find(".t-field").ClassList);
    }

    [Fact]
    public void Render_HintAndError_AreBothDescribedToAScreenReader()
    {
        var cut = RenderField(parameters => parameters
            .Add(field => field.Hint, "Where the suite runs")
            .Add(field => field.Error, "Choose an environment"));

        var describedBy = cut.Find("input").GetAttribute("aria-describedby")?.Split(' ') ?? [];

        Assert.Equal(2, describedBy.Length);
        Assert.Contains(cut.Find(".t-field__hint").Id, describedBy);
        Assert.Contains(cut.Find(".t-field__error").Id, describedBy);
    }

    [Fact]
    public void Render_Required_IsMarkedForBothSightedAndScreenReaderUsers()
    {
        var cut = RenderField(parameters => parameters.Add(field => field.Required, true));

        // The asterisk is decorative; the word is what gets read out.
        Assert.Equal("true", cut.Find(".t-field__required").GetAttribute("aria-hidden"));
        Assert.Equal("required", cut.Find("label .t-visually-hidden").TextContent);
    }

    [Fact]
    public void Render_RequiredInGerman_SaysSo()
    {
        using var german = new CultureScope("de");

        Assert.Equal("Pflichtfeld", RenderField(parameters => parameters.Add(field => field.Required, true))
            .Find("label .t-visually-hidden").TextContent);
    }

    [Fact]
    public void Render_TwoFields_DoNotShareIds()
    {
        Assert.NotEqual(RenderField().Find("input").Id, RenderField().Find("input").Id);
    }
}

public class TInputTests : DesignSystemTestContext
{
    private static TFieldContext Context => new("field-1", "field-1-hint", IsInvalid: false);

    [Fact]
    public void Render_Default_IsATextBoxWiredToItsField()
    {
        var input = Render<TInput>(parameters => parameters
            .Add(component => component.Context, Context)
            .Add(component => component.Value, "Checkout")).Find("input");

        Assert.Equal("text", input.GetAttribute("type"));
        Assert.Equal("field-1", input.Id);
        Assert.Equal("field-1-hint", input.GetAttribute("aria-describedby"));
        Assert.Equal("Checkout", input.GetAttribute("value"));
    }

    [Fact]
    public void Render_Invalid_SaysSoToAssistiveTechnologyAndVisually()
    {
        var input = Render<TInput>(parameters => parameters
            .Add(component => component.Context, new TFieldContext("f", null, IsInvalid: true))).Find("input");

        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Contains("t-input--invalid", input.ClassList);
    }

    [Fact]
    public void Type_Input_RaisesValueChangedAsThePersonTypes()
    {
        string? latest = null;
        var cut = Render<TInput>(parameters => parameters
            .Add(component => component.Context, Context)
            .Add(component => component.ValueChanged, value => latest = value));

        cut.Find("input").Input("Chec");

        Assert.Equal("Chec", latest);
    }

    [Fact]
    public void Leave_Field_RaisesOnCommittedSoValidationRunsOnce()
    {
        // patterns.md §2: validate on leaving the field, not on every keystroke.
        string? committed = null;
        var cut = Render<TInput>(parameters => parameters
            .Add(component => component.Context, Context)
            .Add(component => component.OnCommitted, value => committed = value));

        cut.Find("input").Change("Checkout");

        Assert.Equal("Checkout", committed);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("search")]
    public void Render_Type_IsPassedThrough(string type)
    {
        Assert.Equal(type, Render<TInput>(parameters => parameters
            .Add(component => component.Context, Context)
            .Add(component => component.Type, type)).Find("input").GetAttribute("type"));
    }
}

public class TSelectTests : DesignSystemTestContext
{
    private IRenderedComponent<TSelect> RenderSelect(Action<ComponentParameterCollectionBuilder<TSelect>>? configure = null) =>
        Render<TSelect>(parameters =>
        {
            parameters.Add(select => select.Context, new TFieldContext("env", null, IsInvalid: false));
            parameters.AddChildContent("<option value=\"staging\">Staging</option><option value=\"prod\">Production</option>");
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Default_IsANativeSelectWithItsOptions()
    {
        var cut = RenderSelect();

        Assert.Equal("SELECT", cut.Find("select").TagName);
        Assert.Equal(2, cut.FindAll("option").Count);
    }

    [Fact]
    public void Render_Placeholder_IsShownButCannotBeChosen()
    {
        var first = RenderSelect(parameters => parameters.Add(select => select.Placeholder, "Choose an environment")).Find("option");

        Assert.Equal("Choose an environment", first.TextContent);
        Assert.True(first.HasAttribute("disabled"));
    }

    [Fact]
    public void Render_Default_HidesOurChevronFromScreenReaders()
    {
        Assert.Equal("true", RenderSelect().Find("svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Choose_Option_RaisesValueChanged()
    {
        string? chosen = null;
        var cut = RenderSelect(parameters => parameters.Add(select => select.ValueChanged, value => chosen = value));

        cut.Find("select").Change("prod");

        Assert.Equal("prod", chosen);
    }
}

public class TCheckboxTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_HasALabelConnectedToTheBox()
    {
        var cut = Render<TCheckbox>(parameters => parameters.Add(box => box.Label, "Include skipped tests"));

        var input = cut.Find("input");
        Assert.Equal("checkbox", input.GetAttribute("type"));
        Assert.Equal(input.Id, cut.Find("label").GetAttribute("for"));
        Assert.Contains("Include skipped tests", cut.Find("label").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Hint_DescribesTheBox()
    {
        var cut = Render<TCheckbox>(parameters => parameters
            .Add(box => box.Label, "Include skipped tests")
            .Add(box => box.Hint, "Skipped tests count as neither passed nor failed"));

        Assert.Equal(cut.Find(".t-checkbox__hint").Id, cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Toggle_Always_RaisesCheckedChanged()
    {
        bool? latest = null;
        var cut = Render<TCheckbox>(parameters => parameters
            .Add(box => box.Label, "Include skipped tests")
            .Add(box => box.CheckedChanged, value => latest = value));

        cut.Find("input").Change(true);

        Assert.True(latest);
    }
}

public class TSwitchTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsASwitchForAssistiveTechnology()
    {
        // role="switch" so it is announced as on or off, not as checked.
        var input = Render<TSwitch>(parameters => parameters.Add(component => component.Label, "Require a second factor")).Find("input");

        Assert.Equal("switch", input.GetAttribute("role"));
        Assert.Equal("checkbox", input.GetAttribute("type"));
    }

    [Fact]
    public void Toggle_Always_RaisesCheckedChanged()
    {
        bool? latest = null;
        var cut = Render<TSwitch>(parameters => parameters
            .Add(component => component.Label, "Require a second factor")
            .Add(component => component.CheckedChanged, value => latest = value));

        cut.Find("input").Change(true);

        Assert.True(latest);
    }
}

public class TRadioGroupTests : DesignSystemTestContext
{
    private static readonly IReadOnlyList<TRadioOption<string>> Techniques =
    [
        new("boundary", "Boundary values", "The edges of each input range"),
        new("equivalence", "Equivalence classes"),
        new("pairwise", "Pairwise", Disabled: true),
    ];

    private IRenderedComponent<TRadioGroup<string>> RenderGroup(Action<ComponentParameterCollectionBuilder<TRadioGroup<string>>>? configure = null) =>
        Render<TRadioGroup<string>>(parameters =>
        {
            parameters.Add(group => group.Label, "Technique");
            parameters.Add(group => group.Options, Techniques);
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Render_Default_IsAGroupWithALegendAndOneRadioPerOption()
    {
        var cut = RenderGroup();

        Assert.Equal("FIELDSET", cut.Find("fieldset").TagName);
        Assert.Equal("Technique", cut.Find("legend").TextContent.Trim());
        Assert.Equal(3, cut.FindAll("input[type=radio]").Count);
        Assert.NotNull(cut.Find("[role=radiogroup]"));
    }

    [Fact]
    public void Render_Options_ShareAGroupNameSoOnlyOneCanBeChosen()
    {
        var names = RenderGroup().FindAll("input").Select(input => input.GetAttribute("name")).Distinct();

        Assert.Single(names);
    }

    [Fact]
    public void Render_Value_ChecksThatOptionOnly()
    {
        var checkedBoxes = RenderGroup(parameters => parameters.Add(group => group.Value, "equivalence"))
            .FindAll("input").Where(input => input.HasAttribute("checked")).ToList();

        var only = Assert.Single(checkedBoxes);
        Assert.Equal("Equivalence classes", only.NextElementSibling?.TextContent.Trim());
    }

    [Fact]
    public void Render_DisabledOption_CannotBeChosenWhileTheOthersCan()
    {
        var inputs = RenderGroup().FindAll("input");

        Assert.False(inputs[0].HasAttribute("disabled"));
        Assert.True(inputs[2].HasAttribute("disabled"));
    }

    [Fact]
    public void Choose_Option_RaisesValueChanged()
    {
        string? chosen = null;
        var cut = RenderGroup(parameters => parameters.Add(group => group.ValueChanged, value => chosen = value));

        cut.FindAll("input")[1].Change(true);

        Assert.Equal("equivalence", chosen);
    }

    [Fact]
    public void Render_Error_IsAnnouncedAndMarksTheGroupInvalid()
    {
        var cut = RenderGroup(parameters => parameters.Add(group => group.Error, "Choose a technique"));

        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-invalid"));
        Assert.Equal("polite", cut.Find(".t-radio-group__error").GetAttribute("aria-live"));
    }
}
