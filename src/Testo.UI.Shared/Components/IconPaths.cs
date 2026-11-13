namespace Testo.UI.Shared.Components;

/// <summary>
/// The drawing for each icon (ADR-0015).
/// </summary>
/// <remarks>
/// All on one 20×20 grid, outline only, so they share an optical weight. Stroke width,
/// caps and joins come from <c>TIcon.razor.css</c>; an icon that needs to be solid says
/// so itself with <c>fill="currentColor" stroke="none"</c>.
/// </remarks>
internal static class IconPaths
{
    /// <summary>The inner markup of an icon. Written by us, never from input.</summary>
    public static string For(IconName name) => name switch
    {
        // ---------- Outcome ----------
        IconName.Check => """<path d="M4.75 10.5 8.25 14 15.25 6" />""",
        IconName.CircleCheck => """<circle cx="10" cy="10" r="7.25" /><path d="m6.75 10.25 2.25 2.25 4.25-4.75" />""",
        IconName.CircleCross => """<circle cx="10" cy="10" r="7.25" /><path d="m7.5 7.5 5 5m0-5-5 5" />""",
        IconName.CircleMinus => """<circle cx="10" cy="10" r="7.25" /><path d="M7 10h6" />""",
        IconName.Block => """<circle cx="10" cy="10" r="7.25" /><path d="M4.9 15.1 15.1 4.9" />""",
        IconName.Wave => """<path d="M2.75 11.5c1.4-3.5 3.1-3.5 4.5 0s3.1 3.5 4.5 0 3.1-3.5 4.5 0" />""",
        // The gap in the circle is what makes the spin visible; TIcon.razor.css turns it.
        IconName.Spinner => """<path d="M10 2.75a7.25 7.25 0 1 1-7.25 7.25" />""",

        // ---------- Meaning ----------
        IconName.Warning => """<path d="M10 3.4 17.4 16.3H2.6Z" /><path d="M10 8.25v3.25" /><path d="M10 13.9h.01" />""",
        IconName.Info => """<circle cx="10" cy="10" r="7.25" /><path d="M10 9.5v4" /><path d="M10 6.6h.01" />""",

        // ---------- Movement ----------
        IconName.ChevronUp => """<path d="m5.5 12 4.5-4.5 4.5 4.5" />""",
        IconName.ChevronDown => """<path d="m5.5 8 4.5 4.5L14.5 8" />""",
        IconName.ChevronLeft => """<path d="M12 5.5 7.5 10l4.5 4.5" />""",
        IconName.ChevronRight => """<path d="m8 5.5 4.5 4.5L8 14.5" />""",
        IconName.ArrowUp => """<path d="M10 16.25V4.25" /><path d="m5.5 8.75 4.5-4.5 4.5 4.5" />""",
        IconName.ArrowDown => """<path d="M10 3.75v12" /><path d="m5.5 11.25 4.5 4.5 4.5-4.5" />""",
        IconName.ExternalLink => """<path d="M11.25 3.75h5v5" /><path d="m16.25 3.75-6.5 6.5" /><path d="M15 11.25v3.5a1.5 1.5 0 0 1-1.5 1.5h-8a1.5 1.5 0 0 1-1.5-1.5v-8a1.5 1.5 0 0 1 1.5-1.5h3.5" />""",

        // ---------- Actions ----------
        IconName.Add => """<path d="M10 4.25v11.5" /><path d="M4.25 10h11.5" />""",
        IconName.Edit => """<path d="m13.25 3.5 3.25 3.25L7.25 16H4v-3.25Z" /><path d="m11.5 5.25 3.25 3.25" />""",
        IconName.Delete => """<path d="M4.25 5.75h11.5" /><path d="M8 5.75V4.5a.75.75 0 0 1 .75-.75h2.5a.75.75 0 0 1 .75.75v1.25" /><path d="m5.75 5.75.7 9.6a1 1 0 0 0 1 .9h5.1a1 1 0 0 0 1-.9l.7-9.6" />""",
        IconName.Search => """<circle cx="8.75" cy="8.75" r="5.25" /><path d="m12.6 12.6 3.9 3.9" />""",
        IconName.Filter => """<path d="M3.25 5.25h13.5" /><path d="M5.75 10h8.5" /><path d="M8.5 14.75h3" />""",
        IconName.More => """<circle cx="4.5" cy="10" r="1.3" fill="currentColor" stroke="none" /><circle cx="10" cy="10" r="1.3" fill="currentColor" stroke="none" /><circle cx="15.5" cy="10" r="1.3" fill="currentColor" stroke="none" />""",
        IconName.Close => """<path d="m5.25 5.25 9.5 9.5m0-9.5-9.5 9.5" />""",
        IconName.Menu => """<path d="M3.25 5.75h13.5" /><path d="M3.25 10h13.5" /><path d="M3.25 14.25h13.5" />""",
        IconName.Play => """<path d="M6.75 4.6a.4.4 0 0 1 .6-.35l8.1 5.4a.4.4 0 0 1 0 .7l-8.1 5.4a.4.4 0 0 1-.6-.35Z" fill="currentColor" stroke="none" />""",
        IconName.Stop => """<rect x="5.5" y="5.5" width="9" height="9" rx="1.5" fill="currentColor" stroke="none" />""",
        IconName.Refresh => """<path d="M16.25 10a6.25 6.25 0 1 1-1.9-4.5" /><path d="M16.5 3.25v3.5h-3.5" />""",

        // ---------- Things ----------
        IconName.Person => """<circle cx="10" cy="6.75" r="3.25" /><path d="M4.25 16.5c0-2.9 2.6-4.5 5.75-4.5s5.75 1.6 5.75 4.5" />""",
        IconName.Bell => """<path d="M10 3.25a4.75 4.75 0 0 0-4.75 4.75c0 3.9-1.5 4.9-1.5 4.9h12.5s-1.5-1-1.5-4.9A4.75 4.75 0 0 0 10 3.25Z" /><path d="M8.4 15.4a1.75 1.75 0 0 0 3.2 0" />""",
        IconName.Document => """<path d="M11.25 3.25H6A1.5 1.5 0 0 0 4.5 4.75v10.5A1.5 1.5 0 0 0 6 16.75h8a1.5 1.5 0 0 0 1.5-1.5V7.5Z" /><path d="M11.25 3.25V7.5h4.25" />""",
        IconName.Folder => """<path d="M3.5 6.25A1.5 1.5 0 0 1 5 4.75h2.6a1 1 0 0 1 .8.4l1.1 1.45H15a1.5 1.5 0 0 1 1.5 1.5v6.15a1.5 1.5 0 0 1-1.5 1.5H5a1.5 1.5 0 0 1-1.5-1.5Z" />""",
        IconName.Calendar => """<rect x="3.5" y="4.75" width="13" height="11.75" rx="1.5" /><path d="M3.5 8.5h13" /><path d="M7 3.25v3" /><path d="M13 3.25v3" />""",
        IconName.Clock => """<circle cx="10" cy="10" r="7.25" /><path d="M10 5.9V10l2.75 1.75" />""",
        IconName.Settings => """<path d="M3.25 6.75h4.5" /><path d="M12.25 6.75h4.5" /><path d="M3.25 13.25h2.5" /><path d="M10.25 13.25h6.5" /><circle cx="10" cy="6.75" r="2.25" /><circle cx="8" cy="13.25" r="2.25" />""",

        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "This icon has no drawing yet. Add one to IconPaths."),
    };
}
