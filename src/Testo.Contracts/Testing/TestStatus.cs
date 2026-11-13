namespace Testo.Contracts.Testing;

/// <summary>
/// The outcome of a test, a step or a run. The wording follows ISTQB and is translated in
/// <c>docs/ux/content.md</c>.
/// </summary>
public enum TestStatus
{
    /// <summary>Not executed yet.</summary>
    NotRun,

    /// <summary>Executing now.</summary>
    Running,

    /// <summary>The actual result matched the expected result.</summary>
    Passed,

    /// <summary>The actual result did not match the expected result.</summary>
    Failed,

    /// <summary>Could not be executed, for example because the environment was unavailable.</summary>
    Blocked,

    /// <summary>Deliberately not executed in this run.</summary>
    Skipped,

    /// <summary>Passes and fails without the product changing. Counted separately because it tells us about the test, not the product.</summary>
    Flaky,
}
