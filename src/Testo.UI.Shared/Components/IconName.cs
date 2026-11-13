namespace Testo.UI.Shared.Components;

/// <summary>
/// The icons the product has. An icon is added when a screen needs one (ADR-0015), so a
/// name that is not here is a compile error rather than a blank square.
/// </summary>
public enum IconName
{
    // Outcome
    /// <summary>A plain check mark.</summary>
    Check,

    /// <summary>Passed.</summary>
    CircleCheck,

    /// <summary>Failed.</summary>
    CircleCross,

    /// <summary>Skipped or not run.</summary>
    CircleMinus,

    /// <summary>Blocked.</summary>
    Block,

    /// <summary>Flaky: passes and fails without the product changing.</summary>
    Wave,

    /// <summary>Running.</summary>
    Spinner,

    // Meaning
    /// <summary>Warning.</summary>
    Warning,

    /// <summary>Information.</summary>
    Info,

    // Movement
    /// <summary>Chevron pointing up.</summary>
    ChevronUp,

    /// <summary>Chevron pointing down.</summary>
    ChevronDown,

    /// <summary>Chevron pointing left.</summary>
    ChevronLeft,

    /// <summary>Chevron pointing right.</summary>
    ChevronRight,

    /// <summary>A trend going up.</summary>
    ArrowUp,

    /// <summary>A trend going down.</summary>
    ArrowDown,

    /// <summary>Opens somewhere outside the product.</summary>
    ExternalLink,

    // Actions
    /// <summary>Add something.</summary>
    Add,

    /// <summary>Edit.</summary>
    Edit,

    /// <summary>Delete.</summary>
    Delete,

    /// <summary>Search.</summary>
    Search,

    /// <summary>Filter a list.</summary>
    Filter,

    /// <summary>More actions.</summary>
    More,

    /// <summary>Close.</summary>
    Close,

    /// <summary>A menu, on small screens.</summary>
    Menu,

    /// <summary>Start a run.</summary>
    Play,

    /// <summary>Stop a run.</summary>
    Stop,

    /// <summary>Run again, or reload.</summary>
    Refresh,

    // Things
    /// <summary>A person.</summary>
    Person,

    /// <summary>Notifications.</summary>
    Bell,

    /// <summary>A document, a test case, a report.</summary>
    Document,

    /// <summary>A folder or a suite.</summary>
    Folder,

    /// <summary>A date.</summary>
    Calendar,

    /// <summary>Elapsed time or duration.</summary>
    Clock,

    /// <summary>Settings.</summary>
    Settings,
}
