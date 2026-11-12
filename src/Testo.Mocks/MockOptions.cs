namespace Testo.Mocks;

/// <summary>
/// Settings for the in-memory mock services. Bound from the <c>Mocks</c> configuration section.
/// </summary>
public sealed class MockOptions
{
    /// <summary>Name of the configuration section these options are bound from.</summary>
    public const string SectionName = "Mocks";

    /// <summary>
    /// Seed for all generated mock data. The same seed always produces the same data,
    /// which keeps tests and screenshots stable.
    /// </summary>
    public int Seed { get; set; } = 20260101;
}
