using Microsoft.Extensions.Options;

namespace Testo.Application.Execution;

/// <summary>Keeps artifacts in a directory, one folder per organization and run.</summary>
/// <remarks>
/// Good enough for one machine, and deliberately shaped like the object store that replaces it:
/// keys begin with the organization, and nothing outside this class ever builds a path.
/// </remarks>
public sealed class FileSystemArtifactStore(IOptions<ArtifactStoreOptions> options) : IArtifactStore
{
    private readonly string _root = options.Value.Directory;

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        Guid organizationId,
        Guid runId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var key = Key(organizationId, runId, fileName);
        var path = PathFor(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return key;
    }

    /// <inheritdoc />
    public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PathFor(storageKey);

        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    /// <inheritdoc />
    public Task DeleteRunAsync(Guid organizationId, Guid runId, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_root, organizationId.ToString("N"), runId.ToString("N"));

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>The organization first, always. A key is never built anywhere else.</summary>
    private static string Key(Guid organizationId, Guid runId, string fileName) =>
        $"{organizationId:N}/{runId:N}/{Path.GetFileName(fileName)}";

    /// <summary>
    /// Turns a key into a path, refusing anything that tries to climb out of the root.
    /// </summary>
    private string PathFor(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        var root = Path.GetFullPath(_root);

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The artifact key '{storageKey}' points outside the store.");
        }

        return path;
    }
}

/// <summary>Where the artifact store keeps things.</summary>
public sealed class ArtifactStoreOptions
{
    /// <summary>The configuration section this is bound from.</summary>
    public const string SectionName = "Artifacts";

    /// <summary>The directory artifacts live under.</summary>
    public string Directory { get; set; } = Path.Combine(Path.GetTempPath(), "testo-artifacts");

    /// <summary>How long a link to an artifact stays usable.</summary>
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The key links are signed with. Generated per process unless configured, which means links
    /// stop working when the application restarts — correct for one machine, and a setting to fill
    /// in before there are two.
    /// </summary>
    public string? SigningKey { get; set; }
}
