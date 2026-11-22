using Bunit;
using Autestiq.Modules.Execution.Components;
using Autestiq.Modules.Execution.ViewModels;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Execution;

/// <summary>
/// The expected-results editor (ADR-0034) is used when a test is described and on the test's page, so it is tested:
/// rows are added and removed up to the limit, every field is labelled, and the warning about secrets is always there.
/// </summary>
public class ExpectedResultsEditorTests : DesignSystemTestContext
{
    private IRenderedComponent<ExpectedResultsEditor> Editor(List<ExpectedResultRow> rows, bool disabled = false) =>
        Render<ExpectedResultsEditor>(parameters => parameters
            .Add(component => component.Rows, rows)
            .Add(component => component.Disabled, disabled));

    [Fact]
    public void Render_WithNoRows_OffersToAddOne_AndAlwaysWarnsAboutSecrets()
    {
        var editor = Editor([]);

        Assert.Empty(editor.FindAll("[data-testid^='expected-text-']"));
        Assert.Contains("Never put passwords or personal data", editor.Find("[data-testid='expected-warning']").TextContent, StringComparison.Ordinal);
        Assert.NotNull(editor.Find("[data-testid='expected-add']"));
    }

    [Fact]
    public void Add_AddsARowWithALabelledTextAndPage()
    {
        var rows = new List<ExpectedResultRow>();
        var editor = Editor(rows);

        editor.Find("[data-testid='expected-add']").Click();

        Assert.Single(rows);
        var text = editor.Find("[data-testid='expected-text-1']");
        var path = editor.Find("[data-testid='expected-path-1']");

        // Each input has a label naming it, so a screen reader says which result and which part.
        Assert.Equal("Expected text 1", editor.Find($"label[for='{text.Id}']").TextContent.Trim());
        Assert.StartsWith("On page 1", editor.Find($"label[for='{path.Id}']").TextContent.Trim(), StringComparison.Ordinal);
        Assert.Equal("200", text.GetAttribute("maxlength"));
    }

    [Fact]
    public void Add_StopsAtFive_AndSaysWhy()
    {
        var rows = Enumerable.Range(0, 4).Select(_ => new ExpectedResultRow()).ToList();
        var editor = Editor(rows);

        editor.Find("[data-testid='expected-add']").Click();

        Assert.Equal(5, rows.Count);
        Assert.Empty(editor.FindAll("[data-testid='expected-add']"));
        Assert.Contains("at most 5", editor.Find("[data-testid='expected-full']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_RemovesThatRow_AndItsButtonNamesWhichOne()
    {
        var rows = new List<ExpectedResultRow> { new() { Text = "First" }, new() { Text = "Second" } };
        var editor = Editor(rows);

        var remove = editor.Find("[data-testid='expected-remove-1']");
        Assert.Equal("Remove expected result 1", remove.GetAttribute("aria-label"));
        remove.Click();

        Assert.Equal(["Second"], rows.Select(row => row.Text));
    }

    [Fact]
    public void Disabled_DisablesEveryField()
    {
        var editor = Editor([new ExpectedResultRow()], disabled: true);

        Assert.True(editor.Find("[data-testid='expected-text-1']").HasAttribute("disabled"));
        Assert.True(editor.Find("[data-testid='expected-path-1']").HasAttribute("disabled"));
    }

    [Fact]
    public void ToResults_DropsWhollyEmptyRows_AndAnEmptyPath()
    {
        var results = ExpectedResultRow.ToResults([new() { Text = "Thanks", Path = " " }, new(), new() { Text = " ", Path = "" }]);

        Assert.Equal([new Autestiq.Contracts.Planning.ExpectedResult("Thanks")], results);
    }

    [Fact]
    public void Render_InGerman_SaysItInGerman()
    {
        using var german = new CultureScope("de");

        var editor = Editor([new ExpectedResultRow()]);

        Assert.Equal("Erwartete Ergebnisse", editor.Find("legend").TextContent.Trim());
        Assert.Contains("niemals Passwörter", editor.Find("[data-testid='expected-warning']").TextContent, StringComparison.Ordinal);
    }
}
