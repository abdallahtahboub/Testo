namespace Testo.Platform.Navigation;

/// <summary>The sections of the app navigation, in display order.</summary>
public enum NavGroup
{
    /// <summary>Dashboard and other overview pages.</summary>
    Overview,

    /// <summary>Planning and analysis.</summary>
    Plan,

    /// <summary>Test design and implementation.</summary>
    Build,

    /// <summary>Execution and defects.</summary>
    Run,

    /// <summary>Monitoring, reporting and release readiness.</summary>
    Report,

    /// <summary>Settings.</summary>
    Settings,
}
