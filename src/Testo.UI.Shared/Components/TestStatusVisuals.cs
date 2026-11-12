using Testo.Contracts.Testing;

namespace Testo.UI.Shared.Components;

/// <summary>
/// How a test outcome is shown: its colour, its icon and its word. One place, so a passed
/// test looks the same in a badge, a timeline, a table and a chart.
/// </summary>
public static class TestStatusVisuals
{
    /// <summary>The meaning the colour follows from.</summary>
    public static Tone ToneFor(TestStatus status) => status switch
    {
        TestStatus.Passed => Tone.Success,
        TestStatus.Failed => Tone.Danger,
        TestStatus.Running => Tone.Info,
        TestStatus.Blocked or TestStatus.Flaky => Tone.Warning,
        _ => Tone.Neutral,
    };

    /// <summary>The icon shown with the word, so the status survives greyscale.</summary>
    public static IconName IconFor(TestStatus status) => status switch
    {
        TestStatus.Passed => IconName.CircleCheck,
        TestStatus.Failed => IconName.CircleCross,
        TestStatus.Running => IconName.Spinner,
        TestStatus.Blocked => IconName.Block,
        TestStatus.Flaky => IconName.Wave,
        TestStatus.Skipped => IconName.CircleMinus,
        _ => IconName.CircleMinus,
    };

    /// <summary>
    /// The resource key of the word. Written out rather than composed from the enum name,
    /// so a key that does not exist is caught by <c>ResourceFileTests</c>.
    /// </summary>
    public static string ResourceKeyFor(TestStatus status) => status switch
    {
        TestStatus.NotRun => "Status_NotRun",
        TestStatus.Running => "Status_Running",
        TestStatus.Passed => "Status_Passed",
        TestStatus.Failed => "Status_Failed",
        TestStatus.Blocked => "Status_Blocked",
        TestStatus.Skipped => "Status_Skipped",
        TestStatus.Flaky => "Status_Flaky",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "This status has no word yet."),
    };
}
