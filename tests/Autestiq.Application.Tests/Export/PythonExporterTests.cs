using Autestiq.Contracts.Planning;
using Autestiq.Export;

namespace Autestiq.Application.Tests.Export;

/// <summary>
/// Writing a plan as a Python test (ADR-0023). Every string in a plan came from a model or a customer's page, so
/// most of what is tested here is that none of it can become code.
/// </summary>
public class PythonExporterTests
{
    private static readonly PythonExporter Exporter = new();

    private static TestPlan Plan(params PlannedAction[] actions) => new("Basket keeps its items", actions, []);

    private static PlannedAction Visible(ActionTarget target) => new(ActionKind.AssertVisible, "It is there", target);

    [Theory]
    [MemberData(nameof(EveryActionKind))]
    public void Export_EveryAction_HasATranslation(ActionKind kind)
    {
        // A new action without a translation fails here, before a customer's export does.
        var action = new PlannedAction(kind, "A step", ActionTarget.ByTestId("thing"), Path: "/", Text: "Enter");

        var code = Exporter.Export(Plan(action)).Code;

        Assert.Contains("# A step\n", code, StringComparison.Ordinal);
    }

    public static TheoryData<ActionKind> EveryActionKind()
    {
        var data = new TheoryData<ActionKind>();
        foreach (var kind in Enum.GetValues<ActionKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    [Fact]
    public void Export_AnOrdinaryPlan_IsOneTestFunctionWithAStatementPerStep()
    {
        var exported = Exporter.Export(Plan(
            new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new PlannedAction(ActionKind.Click, "Add the teapot", ActionTarget.ByRole("button", "Add to basket").InsideOf("Cast iron teapot")),
            new PlannedAction(ActionKind.AssertText, "The total is right", ActionTarget.ByTestId("basket-total"), Text: "£53.25"),
            new PlannedAction(ActionKind.AssertUrl, "On the basket", Path: "/basket")));

        Assert.Equal("test_basket_keeps_its_items.py", exported.FileName);
        Assert.Contains("def test_basket_keeps_its_items(page: Page) -> None:\n", exported.Code, StringComparison.Ordinal);
        Assert.Contains("    page.goto(\"/\")\n", exported.Code, StringComparison.Ordinal);
        Assert.Contains("    target(page, \"role\", \"Add to basket\", role=\"button\", within=\"Cast iron teapot\").click()\n", exported.Code, StringComparison.Ordinal);
        Assert.Contains("    expect(target(page, \"testId\", \"basket-total\")).to_have_text(\"\\u00a353.25\")\n", exported.Code, StringComparison.Ordinal);
        Assert.Contains("re.escape(\"/basket\")", exported.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_IsTheSameEveryTime()
    {
        var plan = Plan(new PlannedAction(ActionKind.Goto, "Open", Path: "/"), Visible(ActionTarget.ByText("£42.00")));

        Assert.Equal(Exporter.Export(plan).Code, Exporter.Export(plan).Code);
    }

    [Theory]
    [InlineData("\"); import os; os.system(\"curl evil\") #")]
    [InlineData("\\\"); __import__('os').system('x') #")]
    [InlineData("a\nimport os\nos.system('x')")]
    [InlineData("\"\"\"")]
    [InlineData("{0}{{}}%s")]
    public void Literal_HostileText_StaysInsideTheString(string hostile)
    {
        var literal = PythonExporter.Literal(hostile);

        Assert.StartsWith("\"", literal, StringComparison.Ordinal);
        Assert.EndsWith("\"", literal, StringComparison.Ordinal);

        // Inside, no quote is unescaped and no line ends: the string cannot be closed early or spill onto a new line.
        var inside = literal[1..^1];
        Assert.DoesNotContain('\n', inside);
        Assert.DoesNotContain('\r', inside);

        for (var index = 0; index < inside.Length; index++)
        {
            if (inside[index] == '\\')
            {
                index++;
                continue;
            }

            Assert.NotEqual('"', inside[index]);
        }
    }

    [Fact]
    public void Literal_AnythingButPrintableAscii_IsAnEscape()
    {
        Assert.Equal("\"\\u00a342 \\U0001f600 \\u2028\"", PythonExporter.Literal("£42 😀 \u2028"));
    }

    [Theory]
    [InlineData("Add it\nimport os", "Add it import os")]
    [InlineData("Line\rbreak", "Line break")]
    [InlineData("Separator\u2028here", "Separator here")]
    [InlineData("Para\u2029graph\u0085", "Para graph")]
    public void Comment_AnythingThatEndsALine_BecomesASpace(string description, string expected)
    {
        Assert.Equal(expected, PythonExporter.Comment(description));
    }

    [Fact]
    public void Export_AHostileDescription_CannotEscapeItsComment()
    {
        var code = Exporter.Export(Plan(
            new PlannedAction(ActionKind.Goto, "Open\nimport os\nos.system('x')", Path: "/"),
            Visible(ActionTarget.ByText("x")))).Code;

        Assert.DoesNotContain("\nimport os\nos.system", code, StringComparison.Ordinal);
        Assert.Contains("    # Open import os os.system('x')\n", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ASecret_IsReadByNameFromTheEnvironment_AndItsValueIsNowhere()
    {
        var exported = Exporter.Export(Plan(
            new PlannedAction(ActionKind.Type, "Type the password", ActionTarget.ByTestId("password"), SecretName: "password"),
            Visible(ActionTarget.ByTestId("signed-in-as"))));

        Assert.Equal(["AUTESTIQ_SECRET_PASSWORD"], exported.SecretVariables);
        Assert.Contains("target(page, \"testId\", \"password\").fill(os.environ[\"AUTESTIQ_SECRET_PASSWORD\"])", exported.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Basket keeps its items", "basket_keeps_its_items")]
    [InlineData("  ¡Olé! 3 cups — please ", "ol_3_cups_please")]
    [InlineData("!!!", "plan")]
    [InlineData("import os; os.system('x')", "import_os_os_system_x")]
    public void Identifier_IsLettersDigitsAndUnderscoresOnly(string name, string expected)
    {
        Assert.Equal(expected, PythonExporter.Identifier(name));
    }

    [Fact]
    public void Export_APartialPlan_SaysSoAtTheTop()
    {
        var plan = Plan(Visible(ActionTarget.ByText("x"))) with { Coverage = PlanCoverage.Partial };

        Assert.Contains("covers part of its scenario only", Exporter.Export(plan).Code, StringComparison.Ordinal);
    }
}
