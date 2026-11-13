using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.Platform;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// Signing in, for real: the browser posts the form, the server issues the cookie, and the
/// person lands in the product. Nothing here is mocked.
/// </summary>
public class AccountTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task SignInPage_Always_LoadsWithOneHeadingAndNoProblems()
    {
        var page = await OpenAsync(Routes.SignIn);

        await Expect(page.Page).ToHaveTitleAsync(new Regex("Sign in"));
        await Expect(page.Page.Locator("h1")).ToHaveCountAsync(1);
        await Expect(page.Page.Locator("h1")).ToContainTextAsync("Sign in to");

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignInPage_Always_IsRenderedStaticallyWithNoCircuit()
    {
        // A page that issues a cookie cannot be interactive: an interactive session has no
        // response to put one on (ADR-0016).
        var page = await OpenAsync(Routes.SignIn);

        Assert.Empty(page.WebSockets);
        Assert.DoesNotContain(page.Requests, request => request.Contains("blazor.web.js", StringComparison.Ordinal));
        page.AssertNoProblems();
    }

    [Theory]
    [InlineData(Routes.ForgotPassword, "Choose a new password")]
    [InlineData(Routes.TwoFactor, "Enter your one-time code")]
    public async Task AccountPage_Always_LoadsAndPassesAnAccessibilityScan(string path, string heading)
    {
        var page = await OpenAsync(path);

        await Expect(page.Page.Locator("h1")).ToHaveCountAsync(1);
        await Expect(page.Page.Locator("h1")).ToContainTextAsync(heading);

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_WithTheWrongPassword_SaysSoWithoutSayingWhichPartWasWrong()
    {
        var page = await OpenAsync(Routes.SignIn);

        await SubmitAsync(page, AppFixture.OwnerEmail, "definitely-not-the-password");

        var message = page.Page.GetByText("do not match an account");
        await Expect(message).ToBeVisibleAsync();

        // Still on the sign-in page, and no session was issued.
        Assert.Equal(Routes.SignIn, page.Path);
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_WithAnAddressThatHasNoAccount_SaysExactlyTheSameThing()
    {
        var page = await OpenAsync(Routes.SignIn);

        await SubmitAsync(page, "nobody@northwind.example", "definitely-not-the-password");

        await Expect(page.Page.GetByText("do not match an account")).ToBeVisibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_WithTheRightPassword_LandsInTheProduct()
    {
        var page = await OpenAsync(Routes.SignIn);

        await SubmitAsync(page, AppFixture.OwnerEmail, AppFixture.SeededPassword);

        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_ThenSignOut_EndsTheSession()
    {
        var page = await OpenAsync(Routes.SignIn);
        await SubmitAsync(page, AppFixture.OwnerEmail, AppFixture.SeededPassword);
        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));

        await page.Page.GotoAsync(App.Url(Routes.SignOut));

        // Signing out is a form, not a link, so that another site cannot do it on a person's
        // behalf by including an image.
        await Expect(page.Page.Locator("form.account-form")).ToBeVisibleAsync();
        await Submit(page).ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.SignIn)));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_WithADeepLink_ReturnsToWhereThePersonWasHeading()
    {
        var page = await OpenAsync($"{Routes.SignIn}?next={Uri.EscapeDataString(Routes.App)}");

        await SubmitAsync(page, AppFixture.OwnerEmail, AppFixture.SeededPassword);

        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignIn_WithAnAddressSomewhereElse_IgnoresIt()
    {
        // A sign-in screen that will send a person anywhere is a convincing way to send them
        // somewhere they did not mean to go.
        var page = await OpenAsync($"{Routes.SignIn}?next={Uri.EscapeDataString("https://example.com/")}");

        await SubmitAsync(page, AppFixture.OwnerEmail, AppFixture.SeededPassword);

        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task AcceptInvitation_WithATokenThatIsNotOne_SaysTheLinkNoLongerWorks()
    {
        var page = await OpenAsync($"{Routes.AcceptInvitation}?token=not-a-real-token");

        await Expect(page.Page.GetByText("no longer works")).ToBeVisibleAsync();

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task ForgotPassword_ForAnyAddress_SaysTheSameThing()
    {
        var page = await OpenAsync(Routes.ForgotPassword);

        await page.Page.FillAsync("input[name='Input.Email']", "nobody@northwind.example");
        await Submit(page).ClickAsync();

        await Expect(page.Page.GetByText("a link is on its way")).ToBeVisibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SignInPage_InGerman_IsInGerman()
    {
        var page = await OpenAsync(Routes.SignIn, options => options.Locale = "de-DE");

        await Expect(page.Page.Locator("h1")).ToContainTextAsync("anmelden");
        await Expect(page.Page.GetByLabel("E-Mail-Adresse")).ToBeVisibleAsync();

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }

    /// <summary>
    /// The form's own submit button. The layout also carries the language and theme switches,
    /// which are forms too, so "the submit button" on these pages is four buttons too many.
    /// </summary>
    private static ILocator Submit(TestPage page) =>
        page.Page.Locator("form.account-form button[type=submit]");

    private static async Task SubmitAsync(TestPage page, string email, string password)
    {
        await page.Page.FillAsync("input[name='Input.Email']", email);
        await page.Page.FillAsync("input[name='Input.Password']", password);
        await Submit(page).ClickAsync();
    }
}
