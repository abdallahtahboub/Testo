using System.Globalization;
using System.Text;
using Autestiq.Contracts.Export;
using Autestiq.Contracts.Planning;

namespace Autestiq.Export;

/// <summary>
/// Writes a plan as a pytest test using Playwright for Python (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// One fixed translation per action. Every value that came from a plan — a path, a name, a text, a description —
/// was written by a model or read from a customer's page, so it appears in the code only inside a string literal
/// this class escapes, or in a comment it strips of anything that could end the line. Nothing from a plan is ever
/// written where Python would read it as code.
/// </para>
/// <para>
/// The test locates elements exactly as our own runs do (<c>Targets</c> in the worker): a test id, a role with its
/// exact accessible name, or an exact text, optionally narrowed to the innermost container that holds both a text
/// and the element; and for a step, a looser name only when nothing has the exact one within two seconds. The
/// helper that does it is part of every exported file, so the file needs nothing of ours to run.
/// </para>
/// </remarks>
public sealed class PythonExporter : ITestExporter
{
    /// <summary>The environment variable a secret is read from: <c>AUTESTIQ_SECRET_</c> and its name.</summary>
    public const string SecretPrefix = "AUTESTIQ_SECRET_";

    /// <inheritdoc />
    public ExportLanguage Language => ExportLanguage.Python;

    /// <inheritdoc />
    public ExportedTest Export(TestPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var name = Identifier(plan.ScenarioName);
        var secrets = new SortedSet<string>(StringComparer.Ordinal);
        var code = new StringBuilder();

        code.Append(Header);
        code.Append("# Scenario: ").Append(Comment(plan.ScenarioName)).Append('\n');

        if (plan.Coverage == PlanCoverage.Partial)
        {
            code.Append("# This plan covers part of its scenario only; a pass is not a pass of the whole of it.\n");
        }

        code.Append(Helper);
        code.Append("\n\ndef test_").Append(name).Append("(page: Page) -> None:\n");

        foreach (var action in plan.Actions)
        {
            code.Append("    # ").Append(Comment(action.Description)).Append('\n');
            code.Append("    ").Append(Statement(action, secrets)).Append('\n');
        }

        return new ExportedTest($"test_{name}.py", code.ToString(), [.. secrets]);
    }

    private static string Statement(PlannedAction action, SortedSet<string> secrets) => action.Kind switch
    {
        ActionKind.Goto => $"page.goto({Literal(Required(action.Path))})",
        ActionKind.Click => $"{Target(action)}.click()",
        ActionKind.Type => $"{Target(action)}.fill({TextToType(action, secrets)})",
        ActionKind.Press => $"{Target(action)}.press({Literal(Required(action.Text))})",
        ActionKind.WaitFor => $"{Target(action)}.wait_for(state=\"attached\")",
        ActionKind.Select => $"{Target(action)}.select_option(label={Literal(Required(action.Text))})",
        ActionKind.AssertText => $"expect({Target(action)}).to_have_text({Literal(Required(action.Text))})",
        ActionKind.AssertVisible => $"expect({Target(action)}).to_be_visible()",
        ActionKind.AssertNotVisible => $"expect({Target(action)}).to_be_hidden()",

        // A path, checked as "ends with", so that a query string does not fail an honest check: as our runs do.
        ActionKind.AssertUrl => $"expect(page).to_have_url(re.compile(re.escape({Literal(Required(action.Path))}) + r\"(\\?.*)?$\"))",

        _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "There is no translation for that action."),
    };

    private static string Target(PlannedAction action)
    {
        var target = action.Target ?? throw new ArgumentException($"The step '{action.Description}' names nothing to act on.", nameof(action));

        var arguments = new StringBuilder("target(page, ");
        arguments.Append(target.Kind switch
        {
            TargetKind.TestId => "\"testId\"",
            TargetKind.RoleAndName => "\"role\"",
            TargetKind.Text => "\"text\"",
            _ => throw new ArgumentOutOfRangeException(nameof(action), target.Kind, "There is no translation for that target."),
        });
        arguments.Append(", ").Append(Literal(target.Value));

        if (target.Kind == TargetKind.RoleAndName)
        {
            arguments.Append(", role=").Append(Literal(target.Role ?? ""));
        }

        if (target.Within is { Length: > 0 } within)
        {
            arguments.Append(", within=").Append(Literal(within));
        }

        return arguments.Append(')').ToString();
    }

    private static string TextToType(PlannedAction action, SortedSet<string> secrets)
    {
        if (action.SecretName is not { Length: > 0 } secret)
        {
            return Literal(Required(action.Text));
        }

        var variable = SecretPrefix + Identifier(secret).ToUpperInvariant();
        secrets.Add(variable);

        // Read from the customer's environment by name. The value was never in the plan, and is not in the file.
        return $"os.environ[{Literal(variable)}]";
    }

    private static string Required(string? value) =>
        value is { Length: > 0 } ? value : throw new ArgumentException("A step is missing something it cannot run without.");

    /// <summary>
    /// A Python string literal holding exactly this text, and nothing Python could read as anything else: printable
    /// ASCII as itself apart from the backslash and the quote, everything else as an escape.
    /// </summary>
    public static string Literal(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var text = new StringBuilder("\"");

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            switch (character)
            {
                case '\\':
                    text.Append("\\\\");
                    break;
                case '"':
                    text.Append("\\\"");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                default:
                    if (character is >= ' ' and <= '~')
                    {
                        text.Append(character);
                    }
                    else if (char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        text.Append("\\U").Append(char.ConvertToUtf32(character, value[index + 1]).ToString("x8", CultureInfo.InvariantCulture));
                        index++;
                    }
                    else
                    {
                        text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        return text.Append('"').ToString();
    }

    /// <summary>
    /// Text for a comment: on one line, whatever it held. Every character Python or an editor could take for the
    /// end of a line becomes a space, so nothing after it is read as code.
    /// </summary>
    public static string Comment(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var text = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            text.Append(char.IsControl(character) || character is ' ' or ' ' ? ' ' : character);
        }

        return text.ToString().Trim();
    }

    /// <summary>A Python identifier from a name: lower-case letters, digits and underscores, never empty.</summary>
    public static string Identifier(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var text = new StringBuilder();

        foreach (var character in value.ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                text.Append(character);
            }
            else if (text.Length > 0 && text[^1] != '_')
            {
                text.Append('_');
            }
        }

        var identifier = text.ToString().TrimEnd('_');

        if (identifier.Length > MaxIdentifierLength)
        {
            identifier = identifier[..MaxIdentifierLength].TrimEnd('_');
        }

        return identifier.Length > 0 ? identifier : "plan";
    }

    private const int MaxIdentifierLength = 60;

    private const string Header =
        """
        # Generated by Autestiq from a validated plan. Do not edit it by hand: change the plan and export it again.
        # Run it with pytest and Playwright for Python against your application:
        #   pip install pytest-playwright && playwright install chromium
        #   pytest --base-url https://your-application.example
        # Secrets are read from environment variables by name; the file never holds their values.

        """;

    private const string Helper =
        """

        import os
        import re

        from playwright.sync_api import Locator, Page, TimeoutError as PlaywrightTimeoutError, expect

        # Where "within" may look: the row, item, form or section an element sits in.
        CONTAINERS = "tr, li, article, section, form, fieldset, dl, p"


        def _find(root, kind: str, value: str, role: str | None, exact: bool) -> Locator:
            if kind == "testId":
                return root.get_by_test_id(value)
            if kind == "role":
                return root.get_by_role(role, name=value, exact=exact)
            return root.get_by_text(value, exact=True)


        def _locate(page: Page, kind: str, value: str, role: str | None, within: str | None, exact: bool) -> Locator:
            if within is None:
                return _find(page, kind, value, role, exact)
            # The innermost container that holds both the text and the element.
            holder = page.locator(CONTAINERS).filter(has_text=within, has=_find(page, kind, value, role, exact)).last
            return _find(holder, kind, value, role, exact)


        def target(page: Page, kind: str, value: str, role: str | None = None, within: str | None = None) -> Locator:
            # The exact name first; a looser one only when nothing has the exact name within two seconds.
            exact = _locate(page, kind, value, role, within, True)
            if kind != "role":
                return exact
            try:
                exact.first.wait_for(state="attached", timeout=2000)
                return exact
            except PlaywrightTimeoutError:
                return _locate(page, kind, value, role, within, False)
        """;
}
