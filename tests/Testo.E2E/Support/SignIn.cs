using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Testo.E2E.Support;

/// <summary>
/// Signs in over plain HTTP, the way the browser does, for tests that look at raw responses.
/// </summary>
/// <remarks>
/// The session cookie is encrypted with the data-protection keys of the application that issued
/// it, so a cookie from one in-process application is meaningless to another. Tests that start
/// their own copy therefore sign in against that copy, which is what this helper is for.
/// </remarks>
public static partial class SignIn
{
    /// <summary>
    /// Signs in as the owner of the seeded organization and returns the cookies to send with
    /// later requests.
    /// </summary>
    /// <param name="client">
    /// A client that neither follows redirects nor stores cookies, so the test sees what the
    /// server actually said.
    /// </param>
    /// <param name="cancellationToken">Stops the work if the test is cancelled.</param>
    public static async Task<string> AsOwnerAsync(HttpClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var form = await client.GetAsync("/account/sign-in", cancellationToken).ConfigureAwait(false);
        var html = await form.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var antiforgery = CookiesOf(form);
        var token = TokenField().Match(html).Groups[1].Value;

        if (token.Length == 0)
        {
            throw new InvalidOperationException("The sign-in page carried no antiforgery token.");
        }

        using var post = new HttpRequestMessage(HttpMethod.Post, "/account/sign-in")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["_handler"] = "sign-in",
                ["Input.Email"] = AppFixture.OwnerEmail,
                ["Input.Password"] = AppFixture.SeededPassword,
            }),
        };

        post.Headers.Add("Cookie", antiforgery);

        using var response = await client.SendAsync(post, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != System.Net.HttpStatusCode.Found)
        {
            throw new InvalidOperationException(
                $"Signing in answered {(int)response.StatusCode} rather than a redirect. "
                + "The seeded owner, the form field names or the antiforgery token have moved.");
        }

        return CookiesOf(response);
    }

    /// <summary>Every cookie the response set, in the form a request header wants them.</summary>
    private static string CookiesOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values.Select(value => value.Split(';')[0]))
            : string.Empty;

    /// <summary>Adds the cookies to a request.</summary>
    public static HttpRequestMessage WithCookies(this HttpRequestMessage request, string cookies)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Add("Cookie", cookies);
        return request;
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]*)""")]
    private static partial Regex TokenField();
}
