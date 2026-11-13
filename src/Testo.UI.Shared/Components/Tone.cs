namespace Testo.UI.Shared.Components;

/// <summary>What a piece of the interface means. Colour follows from this, never the other way round.</summary>
public enum Tone
{
    /// <summary>No particular meaning.</summary>
    Neutral,

    /// <summary>Belongs to the brand: a count, a selected thing.</summary>
    Brand,

    /// <summary>Something went well.</summary>
    Success,

    /// <summary>Worth attention, not yet wrong.</summary>
    Warning,

    /// <summary>Something is wrong.</summary>
    Danger,

    /// <summary>Worth knowing.</summary>
    Info,
}
