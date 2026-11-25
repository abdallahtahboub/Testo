using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>
/// Nine scenarios for OWASP Juice Shop, a shop written as an Angular single-page application, which nobody here wrote
/// (Milestone 5.1 R9). It is the first client-side application measured: its pages are rendered in the browser, its
/// addresses are fragments (<c>/#/about</c>), and much of its navigation is buttons, not links.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Written from the source before anything ran</strong> (2026-11-25). Written from version 17.1.1 of
/// <c>juice-shop/juice-shop</c>, commit <c>39b46860612990ae18d499a20472172839fe8c77</c>, which was read and not run
/// here. The application is the Docker image <c>bkimminich/juice-shop:v17.1.1</c>, which has the MIT licence.
/// </para>
/// <para>
/// <strong>This is the tuning application of R9.</strong> Gaps the explorer shows here may be fixed in code, and each
/// fix is re-measured on the lab sets. Its figures are therefore never a clean measurement, unlike PetClinic's.
/// A scenario's words do not change. A baseline may be corrected only where it fails the application as it is, found by
/// a run with no model, and the correction is reported.
/// </para>
/// <para>
/// Juice Shop has no defect switches, so every scenario runs against the sound application. Its database is rebuilt
/// each time the container starts, so a run starts from a fresh container (<c>tools/r9/juice-shop-up.sh</c>).
/// </para>
/// <para>
/// A welcome dialog covers the shop on a first visit until it is dismissed. A baseline that presses anything
/// dismisses it first, as a person would.
/// </para>
/// <para>
/// No scenario signs in or registers. The password fields have no ARIA role, so nothing in the vocabulary can type into
/// them: a gap in the vocabulary, recorded rather than worked around.
/// </para>
/// <para>
/// <strong>Baselines corrected after the first run with no model</strong> (2026-11-25), each because it failed the
/// application as it is; no scenario's words changed:
/// <list type="bullet">
/// <item>About Us, the contact page and the login page: the welcome dialog marks the rest of the page hidden to
/// assistive technology, so a heading is not found by its role until the dialog is dismissed. They dismiss it first.</item>
/// <item>German: the radio's own input sits under its label, which takes the click. The label is pressed, by its text.</item>
/// <item>"The account menu leads to the login" was removed from the set: its menu entry has the role menuitem, which the
/// vocabulary does not have, so no baseline can be written for it. Its words were "Open the account menu and choose to
/// log in, and check that the login page opens."</item>
/// </list>
/// </para>
/// </remarks>
public static class JuiceShopScenarios
{
    /// <summary>Every Juice Shop scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        new(
            "all-products-lists-apple-juice",
            "Open the shop and check that its list of all products includes Apple Juice (1000ml).",
            [
                Goto("Open the shop", "/#/search"),
                SeeText("It lists all products", "All Products"),
                SeeText("Apple Juice is listed", "Apple Juice (1000ml)"),
            ]),

        new(
            "searching-for-apple-finds-only-apple-products",
            "Search the shop for apple and check that Apple Juice (1000ml) and Apple Pomace are found and Banana Juice (1000ml) is not.",
            [
                Goto("Search for apple", "/#/search?q=apple"),
                SeeText("Apple Juice is found", "Apple Juice (1000ml)"),
                SeeText("Apple Pomace is found", "Apple Pomace"),
                NotText("Banana Juice is not", "Banana Juice (1000ml)"),
            ]),

        new(
            "a-search-with-no-match-says-so",
            "Search the shop for kiwano-dragonfruit and check that it says no results were found.",
            [
                Goto("Search for something the shop does not sell", "/#/search?q=kiwano-dragonfruit"),
                SeeText("It says so", "No results found"),
            ]),

        new(
            "a-products-details-describe-it",
            "Open the details of Apple Juice (1000ml) from the list of all products and check that they describe it as the all-time classic.",
            [
                Goto("Open the shop", "/#/search"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                Click("Open the apple juice", "button", "Apple Juice (1000ml)"),
                SeeText("It is described", "The all-time classic."),
            ]),

        new(
            "the-about-page-tells-the-corporate-history",
            "Open the About Us page and check that it has a section on the corporate history and policy.",
            [
                Goto("Open About Us", "/#/about"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                See("It is About Us", "heading", "About Us"),
                SeeText("It has the history", "Corporate History & Policy"),
            ]),

        new(
            "the-contact-page-asks-for-feedback",
            "Open the contact page and check that it asks for customer feedback.",
            [
                Goto("Open the contact page", "/#/contact"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                See("It asks for feedback", "heading", "Customer Feedback"),
            ]),

        new(
            "the-shop-can-be-read-in-german",
            "Switch the shop's language to Deutsch and check that the list of all products is then titled Alle Produkte.",
            [
                Goto("Open the shop", "/#/search"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                Click("Open the language menu", "button", "Language selection menu"),
                ClickText("Choose German", "Deutsch"),
                SeeText("The title is German", "Alle Produkte"),
            ]),

        new(
            "the-side-menu-leads-to-about-us",
            "Open the side menu, follow it to About Us, and check that the About Us page opens.",
            [
                Goto("Open the shop", "/#/search"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                Click("Open the side menu", "button", "Open Sidenav"),
                Click("Follow it to About Us", "link", "Go to about us page"),
                See("About Us opened", "heading", "About Us"),
            ]),

        new(
            "the-login-page-offers-a-forgotten-password",
            "Open the login page and check that it offers help for a forgotten password.",
            [
                Goto("Open the login page", "/#/login"),
                Click("Dismiss the welcome", "button", "Close Welcome Banner"),
                See("It is the login page", "heading", "Login"),
                See("It offers help", "link", "Forgot your password?"),
            ]),
    ];

    private static PlannedAction Goto(string description, string path) =>
        new(ActionKind.Goto, description, Path: path);

    private static PlannedAction Click(string description, string role, string name) =>
        new(ActionKind.Click, description, ActionTarget.ByRole(role, name));

    private static PlannedAction ClickText(string description, string text) =>
        new(ActionKind.Click, description, ActionTarget.ByText(text));

    private static PlannedAction See(string description, string role, string name) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByRole(role, name));

    private static PlannedAction SeeText(string description, string text) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByText(text));

    private static PlannedAction NotText(string description, string text) =>
        new(ActionKind.AssertNotVisible, description, ActionTarget.ByText(text));
}
