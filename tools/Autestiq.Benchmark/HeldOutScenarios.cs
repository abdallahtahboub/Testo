using Autestiq.Contracts.Planning;
using Autestiq.DemoShop;
using static Autestiq.Benchmark.BenchmarkScenarios;

namespace Autestiq.Benchmark;

/// <summary>
/// Twenty scenarios the planner has not been measured on, and must not be tuned against.
/// </summary>
/// <remarks>
/// <para>
/// The twelve in <see cref="BenchmarkScenarios"/> were used to find the planner's failures, and the
/// crawl was then changed to fix them. Measuring again on those twelve shows that the fixes work on
/// the scenarios they were made for. It does not show that the planner works on a scenario nobody has
/// looked at, which is the question ADR-0020 asks. These twenty answer that question.
/// </para>
/// <para>
/// <strong>Freeze rule.</strong> Changing a scenario, its baseline or its defect after a planner has been
/// measured on it makes this set a second design set. Only the baseline check may change a scenario
/// here, and only before the first planner run: a baseline that fails a sound shop, or passes a broken
/// one, means the scenario is wrong, not the planner. The check is that the baseline passes every sound
/// shop and fails every broken one.
/// </para>
/// <para>
/// Fourteen only ever run against a sound shop. The six with a defect cover each of the shop's six
/// defects in words the twelve did not use, so a planner that only learned the old wording fails here.
/// </para>
/// </remarks>
public static class HeldOutScenarios
{
    /// <summary>Every held-out scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        // --- Sound shop only: passing is correct. ---

        new(
            "catalogue-lists-every-product",
            "Open the catalogue and check that all four products are listed: the tea, the teapot, the cup and the tray.",
            [
                Goto("Open the catalogue", "/"),
                AssertVisible("The tea is listed", "name-tea"),
                AssertVisible("The teapot is listed", "name-pot"),
                AssertVisible("The cup is listed", "name-cup"),
                AssertVisible("The tray is listed", "name-tray"),
            ]),

        new(
            "catalogue-shows-every-price",
            "Check the catalogue prices: the tea is £8.50, the cup is £11.25 and the tray is £23.00.",
            [
                Goto("Open the catalogue", "/"),
                Assert("The tea costs £8.50", "price-tea", "£8.50"),
                Assert("The cup costs £11.25", "price-cup", "£11.25"),
                Assert("The tray costs £23.00", "price-tray", "£23.00"),
            ]),

        new(
            "search-ignores-capitals",
            "Search the catalogue for 'TEAPOT' in capitals and check the teapot is still found.",
            [
                Goto("Open the catalogue", "/"),
                Type("Search for TEAPOT", "search", "TEAPOT"),
                Click("Search", "do-search"),
                AssertVisible("The teapot is found", "name-pot"),
            ]),

        new(
            "search-hides-what-does-not-match",
            "Search the catalogue for 'teapot' and check the tea, which does not match, is no longer shown.",
            [
                Goto("Open the catalogue", "/"),
                Type("Search for teapot", "search", "teapot"),
                Click("Search", "do-search"),
                AssertHidden("The tea is not shown", "name-tea"),
            ]),

        new(
            "empty-basket-says-so",
            "Open the basket with nothing in it and check that the shop says the basket is empty.",
            [
                Goto("Open the basket", "/basket"),
                AssertVisible("It says the basket is empty", "empty"),
            ]),

        new(
            "basket-keeps-its-items-between-pages",
            "Add a teapot, visit the sign-in page, then go back to the basket and check the teapot is still in it.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Visit the sign-in page", "/sign-in"),
                Goto("Go back to the basket", "/basket"),
                AssertVisible("The teapot is still in the basket", "line-pot"),
            ]),

        new(
            "checkout-reached-by-the-basket-link",
            "Add a tray, then use the shop's own links to go from the basket to the checkout and check the amount is £23.00.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Click("Follow the checkout link from the basket", "to-checkout"),
                Assert("The checkout asks for £23.00", "checkout-total", "£23.00"),
            ]),

        new(
            "unknown-discount-code-is-refused",
            "Add a teapot, go to the checkout, enter the discount code SAVE99 and check the shop does not accept it and the amount stays £42.00.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Open the checkout", "/checkout"),
                Type("Type the code SAVE99", "code", "SAVE99"),
                Click("Apply it", "apply-code"),
                Assert("The shop says it is not a code it knows", "discount-state", "not a code we know"),
                Assert("The amount is unchanged at £42.00", "checkout-total", "£42.00"),
            ]),

        new(
            "unknown-email-is-refused",
            "Try to sign in as nobody@example.test with any password and check the shop refuses it.",
            [
                Goto("Open the sign-in page", "/sign-in"),
                Type("Type the email", "email", "nobody@example.test"),
                Type("Type any password", "password", "anything-at-all"),
                Click("Try to sign in", "do-sign-in"),
                AssertVisible("The shop says no", "sign-in-error"),
            ]),

        new(
            "two-of-one-product-add-up",
            "Add two teas to the basket and check the basket shows two of them and £17.00 for the line.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add a tea", "add-tea"),
                Goto("Back to the catalogue", "/"),
                Click("Add another tea", "add-tea"),
                Goto("Open the basket", "/basket"),
                Assert("There are two teas", "quantity-tea", "2"),
                Assert("The tea line is £17.00", "line-total-tea", "£17.00"),
            ]),

        new(
            "two-products-add-up-at-checkout",
            "Add a teapot and a tea, go to the checkout and check the amount to pay is £50.50.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Open the checkout", "/checkout"),
                Assert("The amount to pay is £50.50", "checkout-total", "£50.50"),
            ]),

        new(
            "order-empties-the-basket",
            "Add a tea, place the order, then check the basket is empty again.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Open the checkout", "/checkout"),
                Click("Place the order", "place-order"),
                AssertUrl("The shop confirms the order", "/order/placed"),
                Goto("Open the basket", "/basket"),
                AssertVisible("The basket is empty", "empty"),
            ]),

        new(
            "removing-the-only-item-empties-the-basket",
            "Add a teapot, remove it from the basket, and check the basket says it is empty.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Open the basket", "/basket"),
                Click("Remove the teapot", "remove-pot"),
                AssertVisible("The basket says it is empty", "empty"),
            ]),

        new(
            "checkout-counts-the-items",
            "Add a tea and a tray, go to the checkout and check it says there are two items.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Goto("Open the checkout", "/checkout"),
                Assert("It says two items", "checkout-count", "2"),
            ]),

        // --- Each of the six defects, worded differently from the twelve. Passing is a false positive. ---

        new(
            "cup-and-tray-total-at-checkout",
            "Add a cup and a tray. Check the basket adds to £34.25, and that the checkout asks for the same amount.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Goto("Open the basket", "/basket"),
                Assert("The basket adds to £34.25", "basket-total", "£34.25"),
                Goto("Open the checkout", "/checkout"),
                Assert("The checkout asks for £34.25", "checkout-total", "£34.25"),
            ],
            Defects.CheckoutTotal),

        new(
            "tray-discount-taken-once",
            "Add a tray, go to the checkout, apply SAVE10 and check the amount to pay is £20.70, 10% off once.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Goto("Open the checkout", "/checkout"),
                Type("Type the code SAVE10", "code", "SAVE10"),
                Click("Apply it", "apply-code"),
                Assert("The code is accepted", "discount-state", "accepted"),
                Assert("The amount to pay is £20.70", "checkout-total", "£20.70"),
            ],
            Defects.DiscountTwice),

        new(
            "removing-tea-keeps-the-tray",
            "Add a tray and a tea, remove the tea, and check the tray is still in the basket and the basket adds to £23.00.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Open the basket", "/basket"),
                Click("Remove the tea", "remove-tea"),
                AssertVisible("The tray is still in the basket", "line-tray"),
                Assert("The basket adds to £23.00", "basket-total", "£23.00"),
            ],
            Defects.RemoveWrongItem),

        new(
            "two-teas-cost-twice-one",
            "Add the tea to the basket twice and check the basket adds to £17.00.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tea again", "add-tea"),
                Goto("Open the basket", "/basket"),
                Assert("The basket adds to £17.00", "basket-total", "£17.00"),
            ],
            Defects.QuantityIgnored),

        new(
            "password-must-match-for-ada",
            "Sign in as ada@example.test with the password 'guess-123' and check the shop refuses it.",
            [
                Goto("Open the sign-in page", "/sign-in"),
                Type("Type the email", "email", "ada@example.test"),
                Type("Type the password guess-123", "password", "guess-123"),
                Click("Try to sign in", "do-sign-in"),
                AssertVisible("The shop says no", "sign-in-error"),
            ],
            Defects.SignInAnyPassword),

        new(
            "no-order-button_for-an-empty-checkout",
            "With nothing in the basket, open the checkout and check there is no button to place an order.",
            [
                Goto("Open the checkout with nothing in the basket", "/checkout"),
                AssertHidden("There is no place-order button", "place-order"),
            ],
            Defects.EmptyOrderAllowed),
    ];
}
