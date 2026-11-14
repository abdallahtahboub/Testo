using Testo.Contracts.Planning;
using Testo.DemoShop;

namespace Testo.Benchmark;

/// <summary>One controlled scenario: what to ask for, what a person would have written, and the truth.</summary>
/// <param name="Id">A short name, used in the report and as the key for a recorded answer.</param>
/// <param name="Ask">
/// The scenario in a tester's words. This, and nothing else, is what the planner is given.
/// </param>
/// <param name="Baseline">
/// The plan a person wrote for the same scenario. The thing the planner is measured against.
/// </param>
/// <param name="Defect">
/// The defect this scenario is supposed to catch, or <c>null</c> when it only checks that a sound
/// application passes.
/// </param>
/// <param name="Secrets">The values a run of this scenario is given, by name.</param>
public sealed record BenchmarkScenario(
    string Id,
    string Ask,
    IReadOnlyList<PlannedAction> Baseline,
    string? Defect = null,
    IReadOnlyDictionary<string, string>? Secrets = null);

/// <summary>
/// The twelve scenarios the planner is measured on.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The baseline is a hand-written plan, not hand-written C#.</strong> That is deliberate
/// and it is what makes the comparison mean anything: both sides go through the identical
/// executor, so the only thing that differs between the two columns is <em>who chose the
/// actions</em> — a person or a model. A baseline written as Playwright code would also differ in
/// how it waits, how it locates and how it fails, and the table would be comparing two things at
/// once.
/// </para>
/// <para>
/// Six of the twelve have a defect they are meant to catch, so they run twice: once against a
/// sound shop, where passing is correct, and once with the defect switched on, where passing is a
/// <strong>false positive</strong>. The other six only ever run against a sound shop, and measure
/// the opposite mistake: reporting a defect in an application that has none.
/// </para>
/// </remarks>
public static class BenchmarkScenarios
{
    /// <summary>Every scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        new(
            "checkout-matches-basket",
            "Add a teapot and a cup to the basket, go to the checkout, and check that the amount "
            + "to pay is the same as the total the basket showed.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Open the basket", "/basket"),
                Assert("The basket adds both items up", "basket-total", "£53.25"),
                Goto("Open the checkout", "/checkout"),
                Assert("The checkout asks for the same amount", "checkout-total", "£53.25"),
            ],
            Defects.CheckoutTotal),

        new(
            "quantity-counts",
            "Add three porcelain cups to the basket and check that the basket total is three times "
            + "the price of one cup.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add a cup", "add-cup"),
                Goto("Back to the catalogue", "/"),
                Click("Add another cup", "add-cup"),
                Goto("Back to the catalogue again", "/"),
                Click("Add a third cup", "add-cup"),
                Goto("Open the basket", "/basket"),
                Assert("Three of them are in the basket", "quantity-cup", "3"),
                Assert("The total is three times £11.25", "basket-total", "£33.75"),
            ],
            Defects.QuantityIgnored),

        new(
            "discount-applied-once",
            "Add a teapot to the basket. At the checkout, apply the discount code SAVE10 and check "
            + "that the amount to pay is exactly 10% less than the basket total: £37.80.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Open the checkout", "/checkout"),
                Type("Type the discount code", "code", "SAVE10"),
                Click("Apply it", "apply-code"),
                Assert("The code was accepted", "discount-state", "accepted"),
                Assert("A tenth has come off, once", "checkout-total", "£37.80"),
            ],
            Defects.DiscountTwice),

        new(
            "wrong-password-is-refused",
            "Try to sign in as ada@example.test with the password 'not-the-password' and check "
            + "that the shop refuses it rather than signing you in.",
            [
                Goto("Open the sign-in page", "/sign-in"),
                Type("Type the email", "email", "ada@example.test"),
                Type("Type a password that is not the right one", "password", "not-the-password"),
                Click("Try to sign in", "do-sign-in"),
                AssertVisible("The shop says no", "sign-in-error"),
            ],
            Defects.SignInAnyPassword),

        new(
            "remove-takes-out-the-right-line",
            "Add a teapot and a cup to the basket, then remove the cup. Check that the teapot is "
            + "still in the basket and the cup is not.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Open the basket", "/basket"),
                Click("Remove the cup", "remove-cup"),
                AssertVisible("The teapot is still there", "line-pot"),
                Assert("And the basket is the teapot's price", "basket-total", "£42.00"),
            ],
            Defects.RemoveWrongItem),

        new(
            "empty-basket-cannot-be-ordered",
            "With an empty basket, go to the checkout and check that the shop says there is "
            + "nothing to pay for, and offers no way to place an order.",
            [
                Goto("Open the checkout with nothing in the basket", "/checkout"),
                AssertVisible("It says there is nothing to pay for", "nothing-to-pay"),
            ],
            Defects.EmptyOrderAllowed),

        // The six below have no defect to find. They measure the opposite mistake: reporting a
        // problem in an application that has none, which costs a tester an afternoon each time.
        new(
            "search-finds-the-teapot",
            "Search the catalogue for 'teapot' and check that the cast iron teapot is listed.",
            [
                Goto("Open the catalogue", "/"),
                Type("Search for a teapot", "search", "teapot"),
                Click("Search", "do-search"),
                AssertVisible("The teapot is listed", "name-pot"),
            ]),

        new(
            "search-finds-nothing",
            "Search the catalogue for 'bicycle' and check that the shop says nothing matches.",
            [
                Goto("Open the catalogue", "/"),
                Type("Search for something the shop does not sell", "search", "bicycle"),
                Click("Search", "do-search"),
                AssertVisible("The shop says nothing matches", "no-results"),
            ]),

        new(
            "catalogue-shows-a-price",
            "Check that the catalogue shows the price of the cast iron teapot as £42.00.",
            [
                Goto("Open the catalogue", "/"),
                Assert("The teapot costs £42.00", "price-pot", "£42.00"),
            ]),

        new(
            "basket-link-counts-items",
            "Add a teapot and a cup, then check the link to the basket says there are 2 items in it.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Back to the catalogue once more", "/"),
                Assert("The basket link says two", "to-basket", "Basket (2)"),
            ]),

        new(
            "an-order-can-be-placed",
            "Add a teapot to the basket, go to the checkout, place the order, and check that the "
            + "shop confirms the order was placed.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Open the checkout", "/checkout"),
                Click("Place the order", "place-order"),
                AssertVisible("The shop confirms it", "order-state"),
                AssertUrl("And we are on the confirmation page", "/order/placed"),
            ]),

        new(
            "signing-in-works",
            "Sign in as ada@example.test using the stored password, and check that the account "
            + "page says who is signed in.",
            [
                Goto("Open the sign-in page", "/sign-in"),
                Type("Type the email", "email", "ada@example.test"),
                Secret("Type the stored password", "password", "password"),
                Click("Sign in", "do-sign-in"),
                AssertVisible("The account page says who is signed in", "signed-in-as"),
            ],
            Defect: null,

            // The one scenario that needs a value the plan may not contain (ADR-0019 §4).
            Secrets: new Dictionary<string, string>(StringComparer.Ordinal) { ["password"] = Shop.Password }),
    ];

    private static PlannedAction Goto(string description, string path) =>
        new(ActionKind.Goto, description, Path: path);

    private static PlannedAction Click(string description, string testId) =>
        new(ActionKind.Click, description, ActionTarget.ByTestId(testId));

    private static PlannedAction Type(string description, string testId, string text) =>
        new(ActionKind.Type, description, ActionTarget.ByTestId(testId), Text: text);

    private static PlannedAction Secret(string description, string testId, string secretName) =>
        new(ActionKind.Type, description, ActionTarget.ByTestId(testId), SecretName: secretName);

    private static PlannedAction Assert(string description, string testId, string text) =>
        new(ActionKind.AssertText, description, ActionTarget.ByTestId(testId), Text: text);

    private static PlannedAction AssertVisible(string description, string testId) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByTestId(testId));

    private static PlannedAction AssertUrl(string description, string path) =>
        new(ActionKind.AssertUrl, description, Path: path);
}
