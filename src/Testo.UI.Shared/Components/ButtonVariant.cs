namespace Testo.UI.Shared.Components;

/// <summary>The visual weight of a button, which says how important the action is.</summary>
public enum ButtonVariant
{
    /// <summary>The main action of a view. Use it once per view.</summary>
    Primary,

    /// <summary>Any other action that deserves a border.</summary>
    Secondary,

    /// <summary>A quiet action, inside a toolbar or a table row.</summary>
    Ghost,

    /// <summary>Deletes or removes something. Always confirmed or undoable.</summary>
    Danger,
}
