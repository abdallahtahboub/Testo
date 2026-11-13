using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testo.E2E.Support;

namespace Testo.E2E.Http;

/// <summary>Feature flags come from configuration and reach the pages.</summary>
public partial class FeatureFlagTests(AppFixture app)
{
    [Fact]
    public async Task ModuleFlag_SwitchedOnInConfiguration_IsShownAsOn()
    {
        // A second copy of the application, in memory, with one flag switched on.
        await using var application = new TestoWebApplication(app.ConnectionString);
        using var client = application
            .WithWebHostBuilder(builder => builder.UseSetting("FeatureManagement:Module.Planning", "true"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        // /app needs a session (D-1), and this copy of the application has data-protection keys of
        // its own, so the run's shared cookie means nothing here.
        var cookies = await SignIn.AsOwnerAsync(client, TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/app").WithCookies(cookies);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var states = ModuleRow().Matches(html).ToDictionary(row => row.Groups["id"].Value, row => row.Groups["state"].Value);
        Assert.Equal(7, states.Count);
        Assert.Equal("On", states["planning"]);
        Assert.All(states.Where(state => state.Key != "planning"), state => Assert.Equal("Off", state.Value));
    }

    [GeneratedRegex(@"<th[^>]*><code[^>]*>(?<id>[a-z]+)</code></th>\s*<td[^>]*><code[^>]*>[^<]+</code></td>\s*<td[^>]*>(?<state>[^<]+)</td>")]
    private static partial Regex ModuleRow();
}
