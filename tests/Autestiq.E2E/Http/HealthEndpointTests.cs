using System.Net;
using Autestiq.E2E.Support;

namespace Autestiq.E2E.Http;

/// <summary>Liveness and readiness (Milestone 5.1 R2), answered without signing in and never cached.</summary>
public class HealthEndpointTests(AppFixture app) : HttpTest(app)
{
    [Fact]
    public async Task Live_AnswersWithoutSigningIn()
    {
        using var response = await GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alive", await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Ready_WithTheDatabaseMigratedAndAKeyRing_IsReady_WithoutSigningIn()
    {
        using var response = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
