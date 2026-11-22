using Autestiq.Contracts.Planning;

namespace Autestiq.Modules.Execution.ViewModels;

/// <summary>One expected result while it is being typed (ADR-0034): the text, and where it appears.</summary>
public sealed class ExpectedResultRow
{
    /// <summary>The text, exactly as the application shows it.</summary>
    public string? Text { get; set; }

    /// <summary>The page path it appears on, when the tester says so.</summary>
    public string? Path { get; set; }

    /// <summary>The rows as the service takes them. Rows left wholly empty are dropped; the service checks the rest.</summary>
    public static IReadOnlyList<ExpectedResult> ToResults(IEnumerable<ExpectedResultRow> rows) =>
    [
        .. rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Text) || !string.IsNullOrWhiteSpace(row.Path))
            .Select(row => new ExpectedResult(row.Text ?? "", string.IsNullOrWhiteSpace(row.Path) ? null : row.Path)),
    ];

    /// <summary>Rows to edit, from what is kept.</summary>
    public static List<ExpectedResultRow> FromResults(IEnumerable<ExpectedResult> results) =>
        [.. results.Select(result => new ExpectedResultRow { Text = result.Text, Path = result.Path })];
}
