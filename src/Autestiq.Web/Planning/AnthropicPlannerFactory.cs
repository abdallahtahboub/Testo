using Microsoft.Extensions.Options;
using Autestiq.Ai;
using Autestiq.Application.Testing;
using Autestiq.Contracts.Planning;

namespace Autestiq.Web.Planning;

/// <summary>Where the provider's key comes from: server configuration only, as <c>Planner:ApiKey</c> (<c>PLANNER__APIKEY</c>).</summary>
internal sealed class PlannerKeyOptions
{
    public const string Section = "Planner";

    /// <summary>The provider's key. Never stored in the database, never logged, never shown.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// The product's planner: Anthropic's API at the price book's prices for the configured model (ADR-0030). No key, no
/// planner, and planning then fails saying so instead of calling anything.
/// </summary>
internal sealed class AnthropicPlannerFactory(IHttpClientFactory clients, IOptionsMonitor<PlannerKeyOptions> key, TimeProvider timeProvider) : IPlannerFactory
{
    public IScenarioPlanner? Create(string model, decimal inputPerMillion, decimal outputPerMillion)
    {
        if (string.IsNullOrWhiteSpace(key.CurrentValue.ApiKey))
        {
            return null;
        }

        var options = new PlannerOptions
        {
            ApiKey = key.CurrentValue.ApiKey,
            Model = model,
            InputCostPerMillion = inputPerMillion,
            OutputCostPerMillion = outputPerMillion,
        };

        var client = clients.CreateClient(nameof(AnthropicPlannerFactory));
        client.Timeout = options.Timeout;

        return new AnthropicPlanner(client, options, timeProvider);
    }
}
