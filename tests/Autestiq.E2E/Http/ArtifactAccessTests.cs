using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Execution;
using Autestiq.Contracts.Execution;
using Autestiq.Domain.Execution;
using Autestiq.E2E.Support;
using Autestiq.Persistence;
using Autestiq.Platform;

namespace Autestiq.E2E.Http;

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
    public async Task Artifact_OfThisOrganization_WithASessionAndAValidSignature_IsServed()
    {
        // The case every screenshot on a run page depends on. It answered "not found" for everyone until the session
        // was read for every request, not only while a page rendered (SessionFromClaims).
        var url = await StoredScreenshotAsync();
        var cookies = await SignInAsync();

        using var response = await GetAsync(url, ("Cookie", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(Cancellation));
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A screenshot of a run of the seeded organization, stored as a run would store it, and its signed link.</summary>
    private async Task<string> StoredScreenshotAsync()
    {
        using var scope = App.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();

        // Across the filter: no one is signed in to this scope; the seeded project says whose it is.
        var project = await database.Projects.IgnoreQueryFilters().FirstAsync(candidate => candidate.KeyPrefix == "SHOP", Cancellation);

        var run = new TestRun
        {
            OrganizationId = project.OrganizationId,
            ProjectId = project.Id,
            Number = Random.Shared.Next(10_000, 1_000_000),
            ScenarioId = Scenarios.DemoShopBasket,
            TargetOrigin = "http://127.0.0.1:5090",
            Status = RunStatus.Passed,
            QueuedAt = DateTimeOffset.UtcNow,
        };

        using var content = new MemoryStream(Png);
        var key = await scope.ServiceProvider.GetRequiredService<IArtifactStore>().SaveAsync(project.OrganizationId, run.Id, "step-1.png", content, Cancellation);

        var artifact = new Artifact
        {
            OrganizationId = project.OrganizationId,
            TestRunId = run.Id,
            Kind = ArtifactKind.Screenshot,
            StorageKey = key,
            ContentType = "image/png",
            SizeBytes = Png.Length,
            CapturedAt = DateTimeOffset.UtcNow,
        };

        database.TestRuns.Add(run);
        database.Artifacts.Add(artifact);

        using (scope.ServiceProvider.GetRequiredService<TenantWriteScope>().AllowWritingTo(project.OrganizationId))
        {
            await database.SaveChangesAsync(Cancellation);
        }

        return $"/app/artifacts/{artifact.Id}?token={scope.ServiceProvider.GetRequiredService<ArtifactLinks>().Sign(artifact.Id)}";
    }

    [Fact]
    public async Task TestDownload_WithNoSession_RedirectsToSignInAndServesNothing()
    {
        using var response = await GetAsync(Routes.TestDownload(SomeArtifact));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(Routes.SignIn, response.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestDownload_OfATestThisOrganizationDoesNotHave_IsNotFound()
    {
        var cookies = await SignInAsync();

        using var response = await GetAsync(Routes.TestDownload(SomeArtifact), ("Cookie", cookies));

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
