namespace Testo.UI.Shared.Components;

/// <summary>How strongly a badge is drawn.</summary>
public enum BadgeAppearance
{
    /// <summary>A tinted background. The default: readable in a dense list without shouting.</summary>
    Subtle,

    /// <summary>A solid background. For the one thing on a screen that must be seen.</summary>
    Filled,

    /// <summary>An outline only. For a long list where filled badges would make stripes.</summary>
    Outline,
}
