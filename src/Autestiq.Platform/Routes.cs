namespace Autestiq.Platform;

/// <summary>
/// URLs that more than one project needs. Modules link to each other through these
/// constants, never by referencing each other's page types.
/// </summary>
public static class Routes
{
    /// <summary>Root of the product app. Everything below it is interactive.</summary>
    public const string App = "/app";

    /// <summary>
    /// Signing in, signing out, resetting a password, accepting an invitation.
    /// </summary>
    /// <remarks>
    /// Outside <see cref="App"/> because these pages are rendered statically and post plain
    /// forms: a session cookie can only be issued while an HTTP request is in progress, and an
    /// interactive session has none (ADR-0016).
    /// </remarks>
    public const string Account = "/account";

    /// <summary>Where somebody signs in.</summary>
    public const string SignIn = Account + "/sign-in";

    /// <summary>Where somebody signs out.</summary>
    public const string SignOut = Account + "/sign-out";

    /// <summary>Where somebody asks for a password-reset link.</summary>
    public const string ForgotPassword = Account + "/forgot-password";

    /// <summary>Where somebody chooses a new password, having followed the link.</summary>
    public const string ResetPassword = Account + "/reset-password";

    /// <summary>Where a one-time code is asked for.</summary>
    public const string TwoFactor = Account + "/two-factor";

    /// <summary>Where somebody takes up an invitation.</summary>
    public const string AcceptInvitation = Account + "/accept";

    /// <summary>Test runs: the list, and one run with its steps and evidence.</summary>
    /// <summary>The organization's projects (Milestone 5).</summary>
    public const string Projects = App + "/projects";

    /// <summary>A project's environments, by the project's key.</summary>
    public static string Environments(string projectKey) => $"{App}/p/{projectKey}/execution/environments";

    /// <summary>A project's described tests.</summary>
    public static string Tests(string projectKey) => $"{App}/p/{projectKey}/tests";

    /// <summary>A project's settings, such as how long it keeps evidence.</summary>
    public static string ProjectSettings(string projectKey) => $"{App}/p/{projectKey}/settings";

    /// <summary>One described test and its plan.</summary>
    public static string Test(string projectKey, Guid testCaseId) => $"{App}/p/{projectKey}/tests/{testCaseId}";

    /// <summary>Where an approved test is downloaded from (Milestone 5, part 4). Served by the host, not a page.</summary>
    public static string TestDownload(Guid testCaseId) => $"/app/tests/{testCaseId}/download";

    public const string Runs = App + "/runs";

    /// <summary>The organization's members and pending invitations (E1.1).</summary>
    public const string Members = App + "/settings/members";

    /// <summary>One run.</summary>
    public static string Run(Guid runId) => $"{Runs}/{runId}";

    /// <summary>
    /// Every component with its variants, for developers and designers. Behind
    /// <c>Feature.Showcase</c>, which is off outside development.
    /// </summary>
    public const string Showcase = "/showcase";

    /// <summary>The page shown for unknown URLs.</summary>
    public const string NotFound = "/not-found";

    /// <summary>The page shown after an unexpected server error.</summary>
    public const string Error = "/error";

    /// <summary>Form target that stores the visitor's theme choice.</summary>
    public const string ThemePreference = "/preferences/theme";

    /// <summary>Form target that stores the visitor's language choice.</summary>
    public const string CulturePreference = "/preferences/culture";
}
