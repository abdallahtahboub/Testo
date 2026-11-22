using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>
/// Eleven scenarios for eShopOnWeb, Microsoft's sample shop: the first application measured here that
/// nobody on this project wrote, and the first with no test ids at all.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Freeze rule</strong> (as for <see cref="HeldOutScenarios"/>): written on 2026-11-15 from what the
/// pages say, before any planner was run against the application. A scenario or its baseline may change only
/// if its baseline fails the application as it is, and only before the first planner run.
/// </para>
/// <para>
/// eShopOnWeb has no defect switches and its code is not ours to change, so every scenario runs against the
/// sound application only. The set measures the opposite mistake to a missed defect: a test that fails an
/// application with nothing wrong with it. How many defects are caught is measured on the other three sets.
/// </para>
/// <para>
/// The baselines name elements as a person would without test ids: by role and accessible name, or by exact
/// text, narrowed to the product they belong to where the same words repeat. Three things a person would test
/// here are left out because the action vocabulary cannot do them, and that is a finding, not an oversight:
/// choosing a brand or a type from a list (there is no select action), changing a quantity (the field has no
/// label, so nothing names it), and the basket's total (the same amount is shown twice, as the line's cost
/// and as the total, with no container that says which is which). A fourth was written and dropped before
/// any planner run, because its baseline failed the application as it is: a refused registration shows its
/// message twice, in the form's summary and beside the field, and nothing can say which one is meant.
/// </para>
/// <para>
/// The application keeps its orders and a signed-in user's basket in memory between runs. The scenarios
/// that sign in either place the order, which empties the basket, or assert nothing about it.
/// </para>
/// </remarks>
public static class EshopScenarios
{
    /// <summary>The demo account eShopOnWeb publishes on its own sign-in page.</summary>
    private const string DemoUser = "demouser@microsoft.com";

    private static readonly Dictionary<string, string> DemoPassword =
        new(StringComparer.Ordinal) { ["password"] = "Pass@word1" };

    /// <summary>
    /// The scenarios with the whole published demo account, user name as well as password, so that the explorer can
    /// sign in as an environment's stored test account lets it (ADR-0033). Only the scenarios that already carry the
    /// password get it: the others are about what a visitor who is not signed in sees.
    /// </summary>
    public static IReadOnlyList<BenchmarkScenario> SignedIn =>
    [
        .. All.Select(scenario => scenario.Secrets is { } secrets && secrets.ContainsKey("password")
            ? scenario with { Secrets = new Dictionary<string, string>(secrets, StringComparer.Ordinal) { ["username"] = DemoUser } }
            : scenario),
    ];

    /// <summary>Every eShopOnWeb scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        new(
            "second-page-holds-the-last-two",
            "Open the second page of the catalogue and check it shows the Cup<T> Sheet and the Prism White TShirt, and not the Roslyn Red Sheet from the first page.",
            [
                Goto("Open the second page", "/?pageId=1"),
                SeeText("The Cup<T> Sheet is listed", "Cup<T> Sheet"),
                SeeText("The Prism White TShirt is listed", "Prism White TShirt"),
                NotText("The Roslyn Red Sheet is not", "Roslyn Red Sheet"),
            ]),

        new(
            "catalogue-shows-a-price",
            "Open the catalogue and check that the .NET Bot Black Sweatshirt costs 19.50.",
            [
                Goto("Open the catalogue", "/"),
                SeeText("Its price is 19.50", "19.50", within: ".NET Bot Black Sweatshirt"),
            ]),

        new(
            "adding-a-product-opens-the-basket",
            "Add the Roslyn Red Sheet to the basket and check that the basket opens with it in, and that the basket shows one item.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the sheet", "button", "[ ADD TO BASKET ]", within: "Roslyn Red Sheet"),
                AssertUrl("The basket opened", "/Basket"),
                SeeText("The sheet is in it", "Roslyn Red Sheet"),
                SeeText("One item is counted", "1"),
            ]),

        new(
            "two-products-are-both-in-the-basket",
            "Add the Roslyn Red Sheet, go back to the catalogue, add the .NET Blue Sweatshirt, and check that the basket holds both and counts two items.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the sheet", "button", "[ ADD TO BASKET ]", within: "Roslyn Red Sheet"),
                Goto("Back to the catalogue", "/"),
                Click("Add the sweatshirt", "button", "[ ADD TO BASKET ]", within: ".NET Blue Sweatshirt"),
                SeeText("The sheet is in the basket", "Roslyn Red Sheet"),
                SeeText("So is the sweatshirt", ".NET Blue Sweatshirt"),
                SeeText("Two items are counted", "2"),
            ]),

        new(
            "an-empty-basket-says-so",
            "Open the basket without adding anything and check that it says it is empty.",
            [
                Goto("Open the basket", "/Basket"),
                SeeText("It says it is empty", "Basket is empty."),
            ]),

        new(
            "checkout-asks-to-sign-in",
            "Add the Roslyn Red Sheet to the basket, go to the checkout without signing in, and check that you are asked to log in.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the sheet", "button", "[ ADD TO BASKET ]", within: "Roslyn Red Sheet"),
                Click("Go to the checkout", "link", "[ Checkout ]"),
                AssertUrl("The log-in page opened", "/Identity/Account/Login"),
                See("It asks to log in", "heading", "Log in"),
            ]),

        new(
            "wrong-password-is-refused",
            $"Try to log in as {DemoUser} with the password not-the-password and check that the attempt is refused.",
            [
                Goto("Open the log-in page", "/Identity/Account/Login"),
                Type("Type the email", "textbox", "Email", DemoUser),
                Type("Type a wrong password", "textbox", "Password", "not-the-password"),
                Click("Log in", "button", "Log in"),
                SeeText("The attempt is refused", "Invalid login attempt."),
            ]),

        new(
            "signing-in-shows-who-is-signed-in",
            $"Log in as {DemoUser} with the stored password and check that the page then shows who is signed in.",
            [
                Goto("Open the log-in page", "/Identity/Account/Login"),
                Type("Type the email", "textbox", "Email", DemoUser),
                Secret("Type the stored password", "textbox", "Password", "password"),
                Click("Log in", "button", "Log in"),
                SeeText("It says who is signed in", DemoUser),
            ],
            Secrets: DemoPassword),

        new(
            "an-order-can-be-placed",
            $"Add the Roslyn Red Sheet to the basket, check out, log in as {DemoUser} with the stored password when asked, pay, and check that the shop thanks you for the order.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the sheet", "button", "[ ADD TO BASKET ]", within: "Roslyn Red Sheet"),
                Click("Go to the checkout", "link", "[ Checkout ]"),
                Type("Type the email", "textbox", "Email", DemoUser),
                Secret("Type the stored password", "textbox", "Password", "password"),
                Click("Log in", "button", "Log in"),
                SeeText("The review lists the sheet", "Roslyn Red Sheet"),
                Click("Pay", "button", "[ Pay Now ]"),
                See("The shop thanks you", "heading", "Thanks for your Order!"),
                AssertUrl("On the success page", "/Basket/Success"),
            ],
            Secrets: DemoPassword,

            // Exploring never pays (ADR-0032), so the thank-you page is never seen. The tester says what it shows, as a
            // tester of a real shop could (ADR-0034); taken from the baseline above, which checks both. Added
            // 2026-11-22: eShopOnWeb figures before that date were measured without it.
            Expected: [new ExpectedResult("Thanks for your Order!", "/Basket/Success")]),

        new(
            "order-history-needs-sign-in",
            "Open the order history without logging in and check that you are sent to log in first.",
            [
                Goto("Open the order history", "/order/my-orders"),
                AssertUrl("The log-in page opened", "/Identity/Account/Login"),
            ]),

        new(
            "forgotten-password-is-confirmed",
            $"Ask for a password reset for {DemoUser} and check that the request is confirmed.",
            [
                Goto("Open the forgotten-password page", "/Identity/Account/ForgotPassword"),
                Type("Type the email", "textbox", "Email", DemoUser),
                Click("Ask for a reset", "button", "Reset Password"),
                AssertUrl("The confirmation opened", "/Identity/Account/ForgotPasswordConfirmation"),
            ]),
    ];

    private static PlannedAction Goto(string description, string path) =>
        new(ActionKind.Goto, description, Path: path);

    private static PlannedAction AssertUrl(string description, string path) =>
        new(ActionKind.AssertUrl, description, Path: path);

    private static PlannedAction Click(string description, string role, string name, string? within = null) =>
        new(ActionKind.Click, description, Scoped(ActionTarget.ByRole(role, name), within));

    private static PlannedAction Type(string description, string role, string name, string text) =>
        new(ActionKind.Type, description, ActionTarget.ByRole(role, name), Text: text);

    private static PlannedAction Secret(string description, string role, string name, string secretName) =>
        new(ActionKind.Type, description, ActionTarget.ByRole(role, name), SecretName: secretName);

    private static PlannedAction See(string description, string role, string name) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByRole(role, name));

    private static PlannedAction SeeText(string description, string text, string? within = null) =>
        new(ActionKind.AssertVisible, description, Scoped(ActionTarget.ByText(text), within));

    private static PlannedAction NotText(string description, string text) =>
        new(ActionKind.AssertNotVisible, description, ActionTarget.ByText(text));

    private static ActionTarget Scoped(ActionTarget target, string? within) =>
        within is null ? target : target.InsideOf(within);
}
