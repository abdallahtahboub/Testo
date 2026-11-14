namespace Testo.Contracts.Execution;

/// <summary>
/// Which scenarios exist. The names and ids are shared vocabulary: the application offers them,
/// a run stores one, and the worker knows how to carry it out.
/// </summary>
/// <remarks>
/// The <em>steps</em> are not here. They are Playwright code and they live in the worker, which is
/// the only thing that should know how to drive a browser. This is the list; that is the doing.
/// </remarks>
public static class Scenarios
{
    /// <summary>A basket that adds up.</summary>
    public const string DemoShopBasket = "demo-shop.basket";

    /// <summary>A checkout that does not.</summary>
    public const string DemoShopCheckoutTotal = "demo-shop.checkout-total";

    /// <summary>A page trying to read cloud metadata, which must not reach it.</summary>
    public const string DemoShopEgressProbe = "demo-shop.egress-probe";

    /// <summary>Everything a person may start.</summary>
    public static IReadOnlyList<ScenarioSummary> All { get; } =
    [
        new(
            DemoShopBasket,
            "Add two items to the basket",
            "Opens the catalogue, adds a teapot and a cup, and checks the basket lists both."),
        new(
            DemoShopCheckoutTotal,
            "The checkout charges for the whole basket",
            "Adds two items and checks the checkout asks for the same money the basket showed."),
        new(
            DemoShopEgressProbe,
            "A page cannot reach cloud metadata",
            "Opens a page whose own script fetches the metadata endpoint, and checks it was refused."),
    ];

    /// <summary>Whether a scenario with this id exists.</summary>
    public static bool Exists(string scenarioId) =>
        All.Any(scenario => string.Equals(scenario.ScenarioId, scenarioId, StringComparison.Ordinal));

    /// <summary>The name of a scenario, or the id itself when it is not one we know.</summary>
    public static string NameOf(string scenarioId) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.ScenarioId, scenarioId, StringComparison.Ordinal))
            ?.Name ?? scenarioId;
}
