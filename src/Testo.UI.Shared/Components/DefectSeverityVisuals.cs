using Testo.Contracts.Testing;

namespace Testo.UI.Shared.Components;

/// <summary>How a defect severity is shown.</summary>
public static class DefectSeverityVisuals
{
    /// <summary>The meaning the colour follows from.</summary>
    public static Tone ToneFor(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Critical => Tone.Danger,
        DefectSeverity.Major => Tone.Warning,
        DefectSeverity.Minor => Tone.Info,
        _ => Tone.Neutral,
    };

    /// <summary>The two that need acting on are drawn strongest; the rest stay quiet in a long list.</summary>
    public static BadgeAppearance AppearanceFor(DefectSeverity severity) =>
        severity is DefectSeverity.Critical or DefectSeverity.Major ? BadgeAppearance.Filled : BadgeAppearance.Outline;

    /// <summary>The resource key of the word, written out so a missing key is caught by a test.</summary>
    public static string ResourceKeyFor(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Trivial => "Severity_Trivial",
        DefectSeverity.Minor => "Severity_Minor",
        DefectSeverity.Major => "Severity_Major",
        DefectSeverity.Critical => "Severity_Critical",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "This severity has no word yet."),
    };
}
