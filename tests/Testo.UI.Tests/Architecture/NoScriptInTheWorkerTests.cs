using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// ADR-0019 §2: no model output is ever executed as code. The vocabulary cannot express code, and
/// this is the other half of it — the worker has no way of running a string as code at all, so
/// there is nowhere for such a string to arrive.
/// </summary>
/// <remarks>
/// Playwright offers several. <c>EvaluateAsync</c> and its relatives take a line of JavaScript and
/// run it in the page; <c>AddScriptTag</c> and <c>AddInitScript</c> inject one. They are ordinary
/// tools in ordinary automation, and in this product they are the one shape that must not exist,
/// because the thing being automated is chosen by a stranger and the steps are chosen by a model.
/// Keeping them out by test is cheaper than reviewing every future change for them.
/// </remarks>
public class NoScriptInTheWorkerTests
{
    /// <summary>The ways Playwright will run a string for you.</summary>
    private static readonly string[] WaysToRunAString =
    [
        "EvaluateAsync",
        "EvaluateAllAsync",
        "EvaluateHandleAsync",
        "AddScriptTagAsync",
        "AddInitScriptAsync",
        "ExposeFunctionAsync",
        "ExposeBindingAsync",
    ];

    public static TheoryData<string> WorkerFiles =>
    [
        .. RepositoryRoot.SourceFiles("*.cs")
            .Where(file => RepositoryRoot.Relative(file).StartsWith("src/Testo.Worker/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(RepositoryRoot.Relative),
    ];

    [Fact]
    public void TheWorker_HasSourceFilesToCheck()
    {
        // Without this, an empty list would make the theory below pass by saying nothing.
        Assert.NotEmpty(WorkerFiles);
    }

    [Theory]
    [MemberData(nameof(WorkerFiles))]
    public void WorkerFile_NeverAsksTheBrowserToRunAString(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));

        var found = WaysToRunAString
            .Where(way => text.Contains(way, StringComparison.Ordinal))
            .Select(way => $"{file} calls {way}")
            .ToList();

        Assert.Empty(found);
    }
}
