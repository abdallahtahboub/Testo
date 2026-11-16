using System.Diagnostics;
using Autestiq.Contracts.Planning;
using Autestiq.Export;

namespace Autestiq.Benchmark;

/// <summary>
/// The verification ADR-0023 §4 requires before a language is offered: every baseline, exported, run by the
/// language's own test runner against the application with each defect off and on, must give the verdict the plan
/// gives. A test that passes a broken application where the plan failed it is the false positive this whole
/// benchmark exists to count, arriving by another route.
/// </summary>
public static class ExportCheck
{
    /// <summary>Exports and runs every scenario's baseline. True when every exported test agreed with its plan.</summary>
    public static async Task<bool> RunAsync(
        string origin,
        IReadOnlyList<BenchmarkScenario> scenarios,
        Func<IReadOnlyList<string>, bool> setDefects,
        Func<Task>? startAfresh,
        string python,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(setDefects);
        ArgumentNullException.ThrowIfNull(output);

        var exporter = new PythonExporter();
        var directory = Directory.CreateTempSubdirectory("autestiq-export-").FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, "conftest.py"), Conftest).ConfigureAwait(false);

        var agreed = 0;
        var runs = 0;

        if (!await HostileExportStaysDataAsync(exporter, directory, python, output).ConfigureAwait(false))
        {
            return false;
        }

        foreach (var scenario in scenarios)
        {
            var exported = exporter.Export(new TestPlan(scenario.Id, scenario.Baseline, []));
            var file = Path.Combine(directory, exported.FileName);
            await File.WriteAllTextAsync(file, exported.Code).ConfigureAwait(false);

            foreach (var defectOn in scenario.Defect is null ? new[] { false } : [false, true])
            {
                if (!setDefects(defectOn && scenario.Defect is { } defect ? [defect] : []))
                {
                    throw new InvalidOperationException($"'{scenario.Defect}' is not a defect this application has.");
                }

                if (startAfresh is not null)
                {
                    await startAfresh().ConfigureAwait(false);
                }

                var (exitCode, said) = await PytestAsync(python, directory, file, origin, scenario).ConfigureAwait(false);

                // A baseline passes a sound application and fails a broken one: that is what made it a baseline.
                var expected = defectOn ? 1 : 0;
                var agrees = exitCode == expected;

                runs++;
                agreed += agrees ? 1 : 0;

                output.WriteLine(
                    $"  {scenario.Id,-38} defect={(defectOn ? "on " : "off")} exported test {(exitCode switch { 0 => "passed", 1 => "failed", _ => $"could not run ({exitCode})" }),-18} "
                    + (agrees ? "agrees with the plan" : "DISAGREES with the plan"));

                if (!agrees)
                {
                    output.WriteLine("      " + said.ReplaceLineEndings("\n      "));
                }
            }
        }

        setDefects([]);
        output.WriteLine($"Exported to Python and run with pytest: {agreed} of {runs} runs gave the plan's verdict. ({directory})");

        return agreed == runs;
    }

    /// <summary>
    /// A plan whose every string tries to become code, exported, and read back by Python's own parser: the test
    /// function must hold exactly one expression statement per action, and nothing else.
    /// </summary>
    private static async Task<bool> HostileExportStaysDataAsync(PythonExporter exporter, string directory, string python, TextWriter output)
    {
        const string Hostile = "\"); import os; os.system(\"echo pwned\") #\n__import__('os').system('x')\r\"\"\"\u2028x";

        PlannedAction[] actions =
        [
            new(ActionKind.Goto, Hostile, Path: "/" + Hostile),
            new(ActionKind.Type, Hostile, ActionTarget.ByRole("textbox", Hostile).InsideOf(Hostile), Text: Hostile),
            new(ActionKind.Select, Hostile, ActionTarget.ByText(Hostile), Text: Hostile),
            new(ActionKind.AssertText, Hostile, ActionTarget.ByTestId(Hostile), Text: Hostile),
        ];

        var exported = exporter.Export(new TestPlan(Hostile, actions, []));
        var file = Path.Combine(directory, "hostile_" + exported.FileName);
        await File.WriteAllTextAsync(file, exported.Code).ConfigureAwait(false);

        var (exitCode, said) = await RunAsync(
            python,
            directory,
            ["-I", "-c", ShapeCheck, file, actions.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            environment: null).ConfigureAwait(false);

        output.WriteLine(exitCode == 0
            ? "A hostile plan, exported and parsed by Python: every string stayed a string, one statement per action."
            : "A HOSTILE PLAN BECAME CODE when exported: " + said);

        return exitCode == 0;
    }

    private static Task<(int ExitCode, string Said)> PytestAsync(string python, string directory, string file, string origin, BenchmarkScenario scenario)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in scenario.Secrets ?? new Dictionary<string, string>())
        {
            environment[PythonExporter.SecretPrefix + PythonExporter.Identifier(name).ToUpperInvariant()] = value;
        }

        return RunAsync(python, directory, ["-m", "pytest", "-q", "-p", "no:cacheprovider", file, "--base-url", origin], environment);
    }

    /// <summary>Runs a process with an argument list, never through a shell, and keeps the end of what it said.</summary>
    private static async Task<(int ExitCode, string Said)> RunAsync(
        string program,
        string directory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        var start = new ProcessStartInfo(program)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{program} would not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);

        var said = (await standardOutput.ConfigureAwait(false)) + (await standardError.ConfigureAwait(false));
        return (process.ExitCode, said.Length > 2_000 ? said[^2_000..] : said);
    }

    /// <summary>Points pytest-playwright at the browser this machine keeps, when it keeps one somewhere else.</summary>
    private const string Conftest =
        """
        import os

        import pytest


        @pytest.fixture(scope="session")
        def browser_type_launch_args(browser_type_launch_args):
            path = os.environ.get("AUTESTIQ_BROWSER_PATH")
            return {**browser_type_launch_args, "executable_path": path} if path else browser_type_launch_args

        """;

    /// <summary>Python that parses an exported file and checks the test function is one expression per action.</summary>
    private const string ShapeCheck =
        """
        import ast, sys
        tree = ast.parse(open(sys.argv[1], encoding="utf-8").read())
        tests = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name.startswith("test_")]
        assert len(tests) == 1, "expected one test function, found %d" % len(tests)
        body = tests[0].body
        assert len(body) == int(sys.argv[2]), "expected %s statements, found %d" % (sys.argv[2], len(body))
        assert all(isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Call) for statement in body), "a statement is not a call"
        names = {node.id for statement in body for node in ast.walk(statement) if isinstance(node, ast.Name)}
        assert names <= {"page", "target", "expect", "re", "os"}, "unexpected names: %s" % (names - {"page", "target", "expect", "re", "os"})
        """;
}
