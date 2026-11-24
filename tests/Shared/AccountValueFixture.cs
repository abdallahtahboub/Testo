using Autestiq.Contracts.Planning;

namespace Autestiq.Tests.Shared;

/// <summary>
/// The regression fixture for a pass that rests on the test account's own value (ADR-0033, Milestone 5.1 R4): sign in
/// with the stored account, then check the page shows the account's email. Linked into the worker tests, where a run
/// of it ends inconclusive, and into the application tests, where downloading it is refused, so the hosted run and
/// the download are held to the same plan.
/// </summary>
internal static class AccountValueFixture
{
    /// <summary>The stored test account, by name, as an environment hands it to a run.</summary>
    public static Dictionary<string, string> Account(string email, string password) => new(StringComparer.Ordinal)
    {
        ["username"] = email,
        ["password"] = password,
    };

    /// <summary>Signs in with the stored account, then makes one check.</summary>
    public static PlannedAction[] SignInThen(PlannedAction check, string? typeEmailPlainly = null) =>
    [
        new(ActionKind.Goto, "Open the sign-in page", Path: "/sign-in"),
        typeEmailPlainly is not null
            ? new(ActionKind.Type, "Type the email", ActionTarget.ByTestId("email"), Text: typeEmailPlainly)
            : new(ActionKind.Type, "Type the email", ActionTarget.ByTestId("email"), SecretName: "username"),
        new(ActionKind.Type, "Type the password", ActionTarget.ByTestId("password"), SecretName: "password"),
        new(ActionKind.Click, "Sign in", ActionTarget.ByTestId("do-sign-in")),
        check,
    ];

    /// <summary>The check that rests on the account's own value: the page says who is signed in.</summary>
    public static PlannedAction ShowsTheAccountsEmail(string email) =>
        new(ActionKind.AssertText, "It says who is signed in", ActionTarget.ByTestId("signed-in-as"), Text: $"Signed in as {email}");

    /// <summary>A check of something only the signed-in page shows, which the guard leaves alone.</summary>
    public static PlannedAction ShowsTheAccountPage =>
        new(ActionKind.AssertVisible, "The account page is shown", ActionTarget.ByTestId("signed-in-as"));
}
