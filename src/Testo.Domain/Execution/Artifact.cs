using Testo.Domain.Common;

namespace Testo.Domain.Execution;

/// <summary>
/// A file a run produced. The row is here; the bytes are in the artifact store under a key that
/// begins with the organization, and are reached only through the application (ADR-0018).
/// </summary>
/// <remarks>
/// A screenshot of a customer's staging system is customer data. It is kept 90 days
/// (<c>docs/domain-model.md</c> §7), and nothing about it is reachable without the ordinary
/// permission check.
/// </remarks>
public sealed class Artifact : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The run that produced it.</summary>
    public Guid TestRunId { get; set; }

    /// <summary>What kind of file it is.</summary>
    public ArtifactKind Kind { get; set; }

    /// <summary>Where the bytes are, in the store. Begins with the organization.</summary>
    public required string StorageKey { get; set; }

    /// <summary>The media type to serve it as.</summary>
    public required string ContentType { get; set; }

    /// <summary>How big it is.</summary>
    public long SizeBytes { get; set; }

    /// <summary>When it was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; }
}

/// <summary>What kind of file an artifact is.</summary>
public enum ArtifactKind
{
    /// <summary>A screenshot of the page at the end of a step.</summary>
    Screenshot = 1,

    /// <summary>A Playwright trace. Off unless asked for: it carries request bodies.</summary>
    Trace = 2,

    /// <summary>A file the scenario downloaded on purpose.</summary>
    Download = 3,
}
