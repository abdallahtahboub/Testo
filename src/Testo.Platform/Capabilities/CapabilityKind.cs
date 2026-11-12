namespace Testo.Platform.Capabilities;

/// <summary>The kind of testing a capability performs.</summary>
public enum CapabilityKind
{
    /// <summary>End-to-end tests of a web application in a browser.</summary>
    E2EWeb,

    /// <summary>Tests of an HTTP API.</summary>
    Api,

    /// <summary>Load and performance tests.</summary>
    Performance,

    /// <summary>Security tests.</summary>
    Security,

    /// <summary>Accessibility tests.</summary>
    Accessibility,
}
