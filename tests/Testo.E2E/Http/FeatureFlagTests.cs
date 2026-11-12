using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Testo.E2E.Support;

namespace Testo.E2E.Http;

/// <summary>Feature flags come from configuration and reach the pages.</summary>
public partial class FeatureFlagTests
{
    [Fact]
    public async Task ModuleFlag_SwitchedOnInConfiguration_IsShownAsOn()
    {
        // A second copy of the application, in memory, with one flag switched on.
        await using var application = new TestoWebApplication();
        using var client = application
            .WithWebHostBuilder(builder => builder.UseSetting("FeatureManagement:Module.Planning", "true"))
            .CreateClient();

        var html = await client.GetStringAsync("/app", TestContext.Current.CancellationToken);

        var states = ModuleRow().Matches(html).ToDictionary(row => row.Groups["id"].Value, row => row.Groups["state"].Value);
        Assert.Equal(7, states.Count);
        Assert.Equal("On", states["planning"]);
        Assert.All(states.Where(state => state.Key != "planning"), state => Assert.Equal("Off", state.Value));
    }

    [GeneratedRegex(@"<th[^>]*><code[^>]*>(?<id>[a-z]+)</code></th>\s*<td[^>]*><code[^>]*>[^<]+</code></td>\s*<td[^>]*>(?<state>[^<]+)</td>")]
    private static partial Regex ModuleRow();
}
