using System.Net;
using Testo.E2E.Support;
using Testo.Platform;

namespace Testo.E2E.Http;

/// <summary>
/// The gate Milestone 3 forgot to fit: D-1 in <c>docs/security-register.md</c>.
/// </summary>
/// <remarks>
/// Sign-in existed and nothing required it — <c>/app</c> answered 200 with no session. These
/// tests fail if that ever comes back, and they also pin the two things that must *not* change
/// as a result: the public website stays open, and an unknown address stays a 404 rather than
/// becoming a redirect to sign-in.
/// </remarks>
public class AuthorizationTests(AppFixture app) : HttpTest(app)
{
    [Fact]
    public async Task AppPage_WithNoSession_RedirectsToSignIn()
    {
        using var response = await GetAsync(Routes.App);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains(Routes.SignIn, location, StringComparison.Ordinal);

        // And it remembers where they were going, so signing in finishes the journey.
        Assert.Contains("next=", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppPage_WithASession_Answers()
    {
        var cookies = await SignInAsync();

        using var response = await GetAsync(Routes.App, ("Cookie", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/en")]
    [InlineData("/de")]
    [InlineData(Routes.SignIn)]
    [InlineData(Routes.ForgotPassword)]
    [InlineData(Routes.ResetPassword)]
    [InlineData(Routes.TwoFactor)]
    [InlineData(Routes.AcceptInvitation)]
    public async Task PublicPage_WithNoSession_StaysOpen(string path)
    {
        using var response = await GetAsync(path);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{path} answered {(int)response.StatusCode}; it must be reachable without an account.");
    }

    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/en/no-such-page")]
    [InlineData("/de/no-such-page")]
    public async Task UnknownAddress_WithNoSession_IsStillANotFound(string path)
    {
        // A fallback authorization policy would turn these into redirects to sign-in, because the
        // endpoint that serves unknown URLs would be challenged along with everything else. That
        // is why pages carry the attribute themselves (PageAuthorizationTests).
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ThemeSwitch_WithNoSession_StillWorks()
    {
        // A visitor reading the public website changes theme before they have an account.
        using var response = await PostFormAsync(
            Routes.ThemePreference,
            new Dictionary<string, string> { ["theme"] = "dark", ["returnUrl"] = "/en" },
            ("Sec-Fetch-Site", "same-origin"));

        // See Other, as the endpoint has always answered: the choice was stored, now go and look.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.DoesNotContain(Routes.SignIn, response.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
    }
}
