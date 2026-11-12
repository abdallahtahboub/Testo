using Microsoft.AspNetCore.Components;

namespace Testo.UI.Shared.Components;

/// <summary>One option of a <see cref="TChoiceForm"/>.</summary>
/// <param name="Value">The value posted when the option is chosen.</param>
/// <param name="Label">The full, translated name. Read by screen readers when the visible content is an icon or an abbreviation.</param>
/// <param name="IsCurrent">Whether this is the option currently in effect.</param>
/// <param name="ShortText">Optional short visible text, for example a language code.</param>
/// <param name="Icon">Optional icon shown instead of text.</param>
/// <param name="Lang">Optional language of <paramref name="Label"/>, so screen readers pronounce it correctly.</param>
public sealed record ChoiceOption(
    string Value,
    string Label,
    bool IsCurrent,
    string? ShortText = null,
    RenderFragment? Icon = null,
    string? Lang = null);
