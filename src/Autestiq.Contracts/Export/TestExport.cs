using Autestiq.Contracts.Planning;

namespace Autestiq.Contracts.Export;

/// <summary>
/// The languages a validated plan can be exported to as test code (ADR-0023). A project chooses its own; this is
/// the list of what has been built and verified, not a default.
/// </summary>
public enum ExportLanguage
{
    /// <summary>Python, with pytest and Playwright for Python.</summary>
    Python = 1,
}

/// <summary>A plan written out as a test in a customer's language.</summary>
/// <param name="FileName">The file name the code belongs in, such as <c>test_basket_keeps_its_items.py</c>.</param>
/// <param name="Code">The test, ready to save.</param>
/// <param name="SecretVariables">
/// The environment variables the test reads its secrets from. Names only: an exported test never contains a
/// secret's value.
/// </param>
public sealed record ExportedTest(string FileName, string Code, IReadOnlyList<string> SecretVariables);

/// <summary>
/// Writes a validated plan out as test code, by a fixed template and never by a model (ADR-0023). The plan stays
/// the source of truth: an exported test does exactly what the plan does and nothing else.
/// </summary>
public interface ITestExporter
{
    /// <summary>The language this exporter writes.</summary>
    ExportLanguage Language { get; }

    /// <summary>The plan as a test. Deterministic: the same plan gives the same code, byte for byte.</summary>
    ExportedTest Export(TestPlan plan);
}
