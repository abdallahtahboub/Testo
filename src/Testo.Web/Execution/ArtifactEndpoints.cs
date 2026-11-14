using Microsoft.EntityFrameworkCore;
using Testo.Application.Execution;
using Testo.Persistence;

namespace Testo.Web.Execution;

/// <summary>Serves the bytes of an artifact, to somebody who may see them.</summary>
public static class ArtifactEndpoints
{
    /// <summary>Where an artifact is fetched from.</summary>
    public const string Pattern = "/app/artifacts/{artifactId:guid}";

    /// <summary>Adds the endpoint.</summary>
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Pattern, ServeAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Three things have to hold, and the order matters: a session, a signature that has not
    /// expired, and an artifact that exists <em>for this organization</em>.
    /// </summary>
    /// <remarks>
    /// The last is the real control. The context reads through the tenant filter, so another
    /// organization's artifact is not refused — it is not found, which is the same answer as for
    /// an artifact that never existed. Nothing about it leaks, not even that it is there.
    /// </remarks>
    private static async Task<IResult> ServeAsync(
        Guid artifactId,
        string? token,
        TestoDbContext database,
        ArtifactLinks links,
        IArtifactStore store,
        CancellationToken cancellationToken)
    {
        if (!links.IsValid(artifactId, token))
        {
            return Results.NotFound();
        }

        var artifact = await database.Artifacts
            .FirstOrDefaultAsync(candidate => candidate.Id == artifactId, cancellationToken)
            .ConfigureAwait(false);

        if (artifact is null)
        {
            return Results.NotFound();
        }

        var content = await store.OpenAsync(artifact.StorageKey, cancellationToken).ConfigureAwait(false);

        return content is null ? Results.NotFound() : Results.Stream(content, artifact.ContentType);
    }
}
