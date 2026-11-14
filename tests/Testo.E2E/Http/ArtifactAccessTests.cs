using System.Net;
using Testo.E2E.Support;
using Testo.Platform;

namespace Testo.E2E.Http;

/// <summary>
/// The evidence a run leaves behind is the most sensitive thing it produces: a screenshot of
/// somebody's application, sometimes with their data on it. These are the refusals the threat
/// model asks of the endpoint that serves it (<c>docs/execution-threat-model.md</c> §7).
/// </summary>
public class ArtifactAccessTests(AppFixture app) : HttpTest(app)
{
    private static readonly Guid SomeArtifact = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task Artifact_WithNoSession_RedirectsToSignInAndServesNothing()
    {
        using var response = await GetAsync($"/app/artifacts/{SomeArtifact}?token=whatever");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(Routes.SignIn, response.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Artifact_WithASessionButNoSignature_IsNotFound()
    {
        var cookies = await SignInAsync();

        using var response = await GetAsync($"/app/artifacts/{SomeArtifact}", ("Cookie", cookies));

        // Not "forbidden": an artifact nobody may see is indistinguishable from one that is not there.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Artifact_WithAForgedSignature_IsNotFound()
    {
        var cookies = await SignInAsync();

        using var response = await GetAsync(
            $"/app/artifacts/{SomeArtifact}?token=9999999999.ZmFrZS1zaWduYXR1cmU",
            ("Cookie", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunPages_WithNoSession_RedirectToSignIn()
    {
        foreach (var path in new[] { Routes.Runs, Routes.Run(SomeArtifact) })
        {
            using var response = await GetAsync(path);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Contains(Routes.SignIn, response.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
        }
    }
}
