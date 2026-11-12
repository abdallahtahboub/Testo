namespace Testo.UI.Shared.Components;

/// <summary>How a <see cref="TChoiceForm"/> tells assistive technology which option is in effect.</summary>
public enum ChoiceState
{
    /// <summary>Options are toggle buttons (<c>aria-pressed</c>), as in the theme switch.</summary>
    Pressed,

    /// <summary>The option in effect is the current item of a set (<c>aria-current</c>), as in the language switch.</summary>
    Current,
}
