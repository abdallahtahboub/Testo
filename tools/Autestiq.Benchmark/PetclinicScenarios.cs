using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>
/// Eleven scenarios for Spring PetClinic, Spring's sample application: owners, pets, visits and veterinarians, rendered
/// on the server by a Java stack nothing measured here before resembles. The clean measurement ADR-0020 asks for.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Clean-measurement rule</strong> (2026-11-23). Written from the application's source at commit
/// <c>500158f732419217507c7656904b8e6aa1bcc0d6</c> of <c>spring-projects/spring-petclinic</c>, read and never run here,
/// before the application had ever been explored or planned against. The explorer, the prompt and the validator are
/// frozen at the commit that adds this file; nothing in them may change in answer to PetClinic's results before the
/// first planner run is published. A baseline may be corrected only if it fails the application as it is, found by a
/// run with no model, and the correction is reported. A scenario's words may not change at all.
/// </para>
/// <para>
/// PetClinic has no defect switches, so, as for eShopOnWeb, every scenario runs against the sound application and the
/// set measures one mistake: failing an application with nothing wrong with it.
/// </para>
/// <para>
/// Its in-memory database keeps what runs add until the application restarts, so no scenario creates anything another
/// checks, and none creates anything twice that it then looks for once: a new owner is looked for on its own page,
/// an edited address is the same each time, and no pet or visit is added (a pet's name must be unique for its owner,
/// and a repeated visit would show twice). The owner search box has no label, so nothing in the vocabulary can name it;
/// the search is a form sent with GET, so a baseline opens its address instead, as a person reading the address bar
/// could. A visit dated in the past is not tried: the date field's own <c>min</c> stops the browser sending it, so the
/// server's message is never shown.
/// </para>
/// </remarks>
public static class PetclinicScenarios
{
    /// <summary>Every PetClinic scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        new(
            "a-vet-lists-their-specialty",
            "Open the list of veterinarians and check that Helen Leary is listed as a radiology specialist.",
            [
                Goto("Open the veterinarians", "/vets.html"),
                SeeText("Helen Leary is listed", "Helen Leary"),
                SeeText("Her specialty is radiology", "radiology", within: "Helen Leary"),
            ]),

        new(
            "a-vet-without-specialties-says-none",
            "Open the list of veterinarians and check that James Carter has no specialty: the list says none for him.",
            [
                Goto("Open the veterinarians", "/vets.html"),
                SeeText("James Carter has none", "none", within: "James Carter"),
            ]),

        new(
            "the-vets-list-has-a-second-page",
            "Open the second page of the veterinarians list and check that it shows Sharon Jenkins and not Helen Leary.",
            [
                Goto("Open the second page", "/vets.html?page=2"),
                SeeText("Sharon Jenkins is listed", "Sharon Jenkins"),
                NotText("Helen Leary is not", "Helen Leary"),
            ]),

        new(
            "finding-one-owner-opens-their-page",
            "Find the owner whose last name is Franklin and check that his page shows George Franklin at 110 W. Liberty St.",
            [
                Goto("Search for Franklin", "/owners?lastName=Franklin"),
                See("The owner's page opened", "heading", "Owner Information"),
                SeeText("It is George Franklin", "George Franklin"),
                SeeText("At his address", "110 W. Liberty St."),
            ]),

        new(
            "a-shared-last-name-lists-both-owners",
            "Search for owners with the last name Davis and check that both Betty Davis and Harold Davis are listed.",
            [
                Goto("Search for Davis", "/owners?lastName=Davis"),
                See("Betty Davis is listed", "link", "Betty Davis"),
                See("Harold Davis is listed", "link", "Harold Davis"),
            ]),

        new(
            "an-unknown-last-name-is-not-found",
            "Search for an owner with the last name Zygmunt and check that the clinic says no such owner has been found.",
            [
                Goto("Search for Zygmunt", "/owners?lastName=Zygmunt"),
                See("Still on the search", "heading", "Find Owners"),
                SeeText("It says so", "has not been found"),
            ]),

        new(
            "an-owners-page-shows-a-pets-visit",
            "Find the owner Jean Coleman and check that her pet Samantha has a visit recorded for being spayed.",
            [
                Goto("Search for Coleman", "/owners?lastName=Coleman"),
                SeeText("Samantha is her pet", "Samantha"),
                SeeText("A visit for being spayed", "spayed"),
            ]),

        new(
            "a-new-owner-is-created",
            "Add a new owner, Ada Lovelace of 12 Analytical Street, London, telephone 0123456789, and check that her page then shows her details.",
            [
                Goto("Open the find-owners page", "/owners/find"),
                Click("Choose to add an owner", "link", "Add Owner"),
                Type("Type the first name", "textbox", "First Name", "Ada"),
                Type("Type the last name", "textbox", "Last Name", "Lovelace"),
                Type("Type the address", "textbox", "Address", "12 Analytical Street"),
                Type("Type the city", "textbox", "City", "London"),
                Type("Type the telephone", "textbox", "Telephone", "0123456789"),
                Click("Add the owner", "button", "Add Owner"),
                See("Her page opened", "heading", "Owner Information"),
                SeeText("It is Ada Lovelace", "Ada Lovelace"),
                SeeText("At her address", "12 Analytical Street"),
            ]),

        new(
            "a-short-telephone-number-is-refused",
            "Try to add an owner, Alan Turing of 1 Bletchley Road, Milton Keynes, with the telephone number 12345, and check that the form says the telephone must be a 10-digit number.",
            [
                Goto("Open the new-owner form", "/owners/new"),
                Type("Type the first name", "textbox", "First Name", "Alan"),
                Type("Type the last name", "textbox", "Last Name", "Turing"),
                Type("Type the address", "textbox", "Address", "1 Bletchley Road"),
                Type("Type the city", "textbox", "City", "Milton Keynes"),
                Type("Type a short telephone", "textbox", "Telephone", "12345"),
                Click("Try to add the owner", "button", "Add Owner"),
                SeeText("The telephone is refused", "Telephone must be a 10-digit number"),
                NotText("No owner page opened", "Owner Information"),
            ]),

        new(
            "an-edited-address-is-kept",
            "Find the owner Jeff Black, change his address to 99 Orchard Lane, save it, and check that his page shows the new address.",
            [
                Goto("Search for Black", "/owners?lastName=Black"),
                Click("Edit the owner", "link", "Edit Owner"),
                Type("Type the new address", "textbox", "Address", "99 Orchard Lane"),
                Click("Save the owner", "button", "Update Owner"),
                See("His page opened", "heading", "Owner Information"),
                SeeText("It shows the new address", "99 Orchard Lane"),
            ]),

        new(
            "the-error-link-shows-the-error-page",
            "Follow the Error link in the menu and check that the clinic shows its error page saying something happened.",
            [
                Goto("Open the home page", "/"),
                Click("Follow the Error link", "link", "Error"),
                See("The error page says so", "heading", "Something happened..."),
            ]),
    ];

    private static PlannedAction Goto(string description, string path) =>
        new(ActionKind.Goto, description, Path: path);

    private static PlannedAction Click(string description, string role, string name) =>
        new(ActionKind.Click, description, ActionTarget.ByRole(role, name));

    private static PlannedAction Type(string description, string role, string name, string text) =>
        new(ActionKind.Type, description, ActionTarget.ByRole(role, name), Text: text);

    private static PlannedAction See(string description, string role, string name) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByRole(role, name));

    private static PlannedAction SeeText(string description, string text, string? within = null) =>
        new(ActionKind.AssertVisible, description, within is null ? ActionTarget.ByText(text) : ActionTarget.ByText(text).InsideOf(within));

    private static PlannedAction NotText(string description, string text) =>
        new(ActionKind.AssertNotVisible, description, ActionTarget.ByText(text));
}
