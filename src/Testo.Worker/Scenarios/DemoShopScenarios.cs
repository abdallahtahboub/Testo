using Microsoft.Playwright;
using Testo.Contracts.Execution;

namespace Testo.Worker.Scenarios;

/// <summary>
/// The two scenarios of the deterministic slice: one that passes and one that finds a real defect.
/// </summary>
/// <remarks>
/// Both run against the demo shop we ship. The failing one is not a contrived assertion: the shop
/// genuinely leaves everything after the first item out of its checkout total, and the scenario
/// notices the way a tester would — by comparing what the basket said with what the checkout says.
/// </remarks>
public static class DemoShopScenarios
{
    /// <summary>A basket that adds up.</summary>
    public const string HappyPath = Contracts.Execution.Scenarios.DemoShopBasket;

    /// <summary>A checkout that does not.</summary>
    public const string KnownFailure = Contracts.Execution.Scenarios.DemoShopCheckoutTotal;

    /// <summary>A page trying to read cloud metadata, which must not reach it.</summary>
    public const string EgressProbe = Contracts.Execution.Scenarios.DemoShopEgressProbe;

    /// <summary>Everything a run may be asked for.</summary>
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new(
            HappyPath,
            "Add two items to the basket",
            "Opens the catalogue, adds a teapot and a cup, and checks the basket lists both.",
            [
                new("Open the catalogue", async (page, _) =>
                {
                    await page.GotoAsync("/");
                    await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Catalogue");
                }),
                new("Add the teapot", async (page, _) =>
                {
                    await page.GetByTestId("add-pot").ClickAsync();
                    await Assertions.Expect(page.GetByTestId("line-pot")).ToBeVisibleAsync();
                }),
                new("Add the cup", async (page, _) =>
                {
                    await page.GotoAsync("/");
                    await page.GetByTestId("add-cup").ClickAsync();
                }),
                new("The basket lists both items", async (page, _) =>
                {
                    await page.GotoAsync("/basket");
                    await Assertions.Expect(page.GetByTestId("line-pot")).ToBeVisibleAsync();
                    await Assertions.Expect(page.GetByTestId("line-cup")).ToBeVisibleAsync();
                }),
                new("The basket total is the sum of both", async (page, _) =>
                {
                    await Assertions.Expect(page.GetByTestId("basket-total")).ToHaveTextAsync("£53.25");
                }),
            ]),

        new(
            KnownFailure,
            "The checkout charges for the whole basket",
            "Adds two items and checks that the checkout asks for the same money the basket showed. It does not.",
            [
                new("Add the teapot", async (page, _) =>
                {
                    await page.GotoAsync("/");
                    await page.GetByTestId("add-pot").ClickAsync();
                }),
                new("Add the cup", async (page, _) =>
                {
                    await page.GotoAsync("/");
                    await page.GetByTestId("add-cup").ClickAsync();
                }),
                new("The basket shows two items", async (page, _) =>
                {
                    await page.GotoAsync("/basket");
                    await Assertions.Expect(page.GetByTestId("basket-total")).ToHaveTextAsync("£53.25");
                }),
                new("The checkout asks for the same amount", async (page, _) =>
                {
                    await page.GotoAsync("/checkout");

                    // This is the defect: the checkout leaves out everything after the first item.
                    await Assertions.Expect(page.GetByTestId("checkout-total")).ToHaveTextAsync("£53.25");
                }),
            ]),
        new(
            EgressProbe,
            "A page cannot reach cloud metadata",
            "Opens a page whose own script fetches the metadata endpoint, and checks it was refused.",
            [
                new("Open a page that fetches the metadata endpoint", async (page, _) =>
                {
                    await page.GotoAsync("/probe");
                }),
                new("The fetch was refused", async (page, _) =>
                {
                    // Not a navigation, so nothing a navigation-level check would see. It is
                    // refused because the proxy is the only route out, not because anybody
                    // remembered to look (docs/execution-threat-model.md §4.1).
                    await Assertions.Expect(page.GetByTestId("probe-result")).ToHaveTextAsync("refused");
                }),
            ]),
    ];

    /// <summary>Finds a scenario by id, or <c>null</c> when there is no such scenario.</summary>
    public static Scenario? Find(string id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.Ordinal));
}
