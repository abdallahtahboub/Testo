namespace Testo.Application.Execution;

/// <summary>
/// Where the bytes of screenshots, traces and downloads live.
/// </summary>
/// <remarks>
/// A screenshot of a customer's staging system is customer data (ADR-0018), so every key begins
/// with the organization and nothing is addressable without going through the application. This
/// is a file system today and object storage when there is more than one machine; the interface is
/// the same either way, which is the point of having one.
/// </remarks>
public interface IArtifactStore
{
    /// <summary>Stores a file and returns its key.</summary>
    Task<string> SaveAsync(
        Guid organizationId,
        Guid runId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file, or <c>null</c> when there is nothing at that key.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Removes everything belonging to a run, when its evidence has aged out.</summary>
    Task DeleteRunAsync(Guid organizationId, Guid runId, CancellationToken cancellationToken = default);
}
