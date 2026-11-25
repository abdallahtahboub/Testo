using Autestiq.Contracts.Planning;

namespace Autestiq.Benchmark;

/// <summary>
/// Nine scenarios for Gitea, a self-hosted Git service written in Go: server-rendered pages with client-side controls,
/// which nobody here wrote (Milestone 5.1 R9). It is R9's <strong>clean</strong> application, as PetClinic was.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Clean-measurement rule</strong> (2026-11-25). Written from the source of version 1.22.6 of
/// <c>go-gitea/gitea</c>, commit <c>8eefa1f6dedf2488db2c9e12c916e8e51f673160</c>, read and never run here, before the
/// application had ever been started, explored or planned against. The application is the image
/// <c>gitea/gitea:1.22.6</c> (MIT), pulled through Google's mirror of Docker Hub
/// (<c>mirror.gcr.io/gitea/gitea@sha256:538658de667c5d098a274f2f63aa6ec891d88f670cdd5282cf27221ba747dda4</c>), because Docker
/// Hub refused this environment with its rate limit. The explorer, the prompt and the validator are frozen at the
/// commit that adds this file. Nothing in them may change in answer to Gitea's results before the first planner run is
/// published. A baseline may be corrected only if it fails the application as it is, found by a run with no model,
/// and the correction is reported. A scenario's words may not change at all.
/// </para>
/// <para>
/// <strong>What the instance holds</strong>, set up the same way before every run (<c>tools/r9/gitea-up.sh</c>):
/// installation locked, SQLite, registration open. There is one user, <c>gardener</c>, with one public repository,
/// <c>apples</c>, described as "Crisp and sweet, picked in October." and created with a README. Nothing else is
/// there. Gitea has no defect switches, so every scenario runs against the sound application.
/// </para>
/// <para>
/// <strong>After the first run with no model</strong> (2026-11-25), nine baselines passed as written and one could not
/// be written at all, so it was removed from the set: "a-repository-page-shows-its-readme", whose words were "Open the
/// apples repository of gardener and check that its README shows its description." The repository page shows the
/// description twice, at the top and in the README, so the text alone names two elements; and the README is a plain
/// <c>div</c>, which is not among the containers a target may be scoped to (<c>Targets.Containers</c>). A gap in the
/// vocabulary, recorded, not fixed here: the explorer and vocabulary are frozen for this application.
/// </para>
/// <para>
/// No scenario signs in: a password field was thought to have no ARIA role (as on Juice Shop). It has the textbox role
/// (corrected 2026-11-25, ADR-0042); the scenarios are not changed for it, and Gitea's first planner run uses the commit
/// that froze them, `44ebe97` on `develop`.
/// No scenario registers, because a registration would change what the next run sees.
/// </para>
/// </remarks>
public static class GiteaScenarios
{
    /// <summary>Every Gitea scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        new(
            "explore-lists-the-apples-repository",
            "Open the list of repositories to explore and check that the apples repository is listed with its description.",
            [
                Goto("Explore the repositories", "/explore/repos"),
                See("The apples repository is listed", "link", "apples"),
                SeeText("With its description", "Crisp and sweet, picked in October."),
            ]),

        new(
            "a-repository-search-that-matches-nothing-says-so",
            "Search the repositories to explore for quinces and check that it says no matching results were found.",
            [
                Goto("Search for quinces", "/explore/repos?q=quinces"),
                SeeText("It says so", "No matching results found."),
            ]),

        new(
            "explore-lists-the-gardener",
            "Open the list of users to explore and check that gardener is listed.",
            [
                Goto("Explore the users", "/explore/users"),
                See("The gardener is listed", "link", "gardener"),
            ]),

        new(
            "explore-has-no-organizations",
            "Open the list of organizations to explore and check that there are none: it says no matching results were found.",
            [
                Goto("Explore the organizations", "/explore/organizations"),
                SeeText("It says so", "No matching results found."),
            ]),

        new(
            "the-explore-menu-leads-to-the-users",
            "From the list of repositories to explore, follow the menu to Users and check that gardener is listed there.",
            [
                Goto("Explore the repositories", "/explore/repos"),
                Click("Follow the menu to Users", "link", "Users"),
                See("The gardener is listed", "link", "gardener"),
            ]),

        new(
            "a-users-page-lists-their-repository",
            "Open gardener's profile and check that it lists the apples repository.",
            [
                Goto("Open the profile", "/gardener"),
                See("It lists apples", "link", "apples"),
            ]),

        new(
            "the-sign-in-page-asks-for-a-username-or-email",
            "Open the sign-in page and check that it asks for a username or email address and offers help for a forgotten password.",
            [
                Goto("Open the sign-in page", "/user/login"),
                See("It asks for a username or email", "textbox", "Username or Email Address"),
                See("It offers help", "link", "Forgot password?"),
            ]),

        new(
            "the-registration-page-asks-for-an-email",
            "Open the registration page and check that it asks for a username and an email address.",
            [
                Goto("Open the registration page", "/user/sign_up"),
                See("It asks for a username", "textbox", "Username"),
                See("It asks for an email address", "textbox", "Email Address"),
            ]),

        new(
            "an-unknown-repository-is-not-found",
            "Open a repository that does not exist, gardener's pears, and check that Gitea says the page does not exist.",
            [
                Goto("Open a repository that does not exist", "/gardener/pears"),
                SeeText("It says so", "does not exist"),
            ]),
    ];

    private static PlannedAction Goto(string description, string path) =>
        new(ActionKind.Goto, description, Path: path);

    private static PlannedAction Click(string description, string role, string name) =>
        new(ActionKind.Click, description, ActionTarget.ByRole(role, name));

    private static PlannedAction See(string description, string role, string name) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByRole(role, name));

    private static PlannedAction SeeText(string description, string text) =>
        new(ActionKind.AssertVisible, description, ActionTarget.ByText(text));
}
