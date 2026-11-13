using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testo.E2E.Support;
using Testo.Platform;
using Testo.Web.Security;

namespace Testo.E2E.Http;

/// <summary>
/// One address cannot work through a password list (D-4).
/// </summary>
/// <remarks>
/// Against an application of its own, with a strict limit. The shared one is deliberately
/// unlimited: every test posts to the account pages from the same loopback address, so a real
/// limit there would have them throttling each other rather than testing anything.
/// </remarks>
public class AccountRateLimitTests(AppFixture app)
{
    private const int Permitted = 3;

    [Fact]
    public async Task SignInPosts_BeyondTheLimit_AreRefusedWithARetryAfter()
    {
        using var client = StrictlyLimited();

        // The allowance itself, spent on attempts that fail for the ordinary reason.
        for (var attempt = 1; attempt <= Permitted; attempt++)
        {
            using var allowed = await PostAsync(client);

            Assert.True(
                allowed.StatusCode != HttpStatusCode.TooManyRequests,
                $"attempt {attempt} of {Permitted} was refused; the limit is biting too early.");
        }

        using var refused = await PostAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        // And it says when to come back, for the person sharing an office address with whoever
        // spent the allowance.
        Assert.True(
            refused.Headers.TryGetValues("Retry-After", out var retryAfter),
            "a refusal must say when to try again.");
        Assert.NotEmpty(retryAfter);
    }

    [Fact]
    public async Task ReadingTheSignInPage_IsNeverLimited()
    {
        // Arriving is not attempting. An office behind one address arrives all morning, and
        // limiting that would make the product unusable rather than safer.
        using var client = StrictlyLimited();

        for (var visit = 0; visit < Permitted * 4; visit++)
        {
            using var response = await client.GetAsync(Routes.SignIn, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PagesOutsideTheAccountArea_AreNotLimited()
    {
        using var client = StrictlyLimited();

        for (var visit = 0; visit < Permitted * 4; visit++)
        {
            using var response = await client.GetAsync("/en", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>A copy of the application that allows only a few attempts a minute.</summary>
    private HttpClient StrictlyLimited()
    {
        var application = new TestoWebApplication(app.ConnectionString)
            .WithWebHostBuilder(builder =>
                builder.UseSetting(AccountRateLimiting.PermitsPerMinuteKey, Permitted.ToString(CultureInfo.InvariantCulture)));

        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    /// <summary>
    /// A sign-in attempt with no antiforgery token. It is refused either way; what is under test
    /// is whether it is refused for being too frequent, before anything expensive happens.
    /// </summary>
    private static Task<HttpResponseMessage> PostAsync(HttpClient client) =>
        client.PostAsync(
            Routes.SignIn,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["_handler"] = "sign-in",
                ["Input.Email"] = "somebody@northwind.example",
                ["Input.Password"] = "a-guess",
            }),
            TestContext.Current.CancellationToken);
}
