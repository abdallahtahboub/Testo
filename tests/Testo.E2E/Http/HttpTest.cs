using Testo.E2E.Support;

namespace Testo.E2E.Http;

/// <summary>Base class for tests that look at raw HTTP responses, without a browser.</summary>
public abstract class HttpTest(AppFixture app) : IDisposable
{
    private readonly HttpClient _client = app.CreateHttpClient();

    /// <summary>The token that ends a request when the test run is cancelled.</summary>
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected Task<HttpResponseMessage> GetAsync(string path, params (string Name, string Value)[] headers) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), headers);

    protected Task<HttpResponseMessage> PostFormAsync(string path, IReadOnlyDictionary<string, string> form, params (string Name, string Value)[] headers) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) }, headers);

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, (string Name, string Value)[] headers)
    {
        using (request)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }

            return await _client.SendAsync(request, Cancellation);
        }
    }

    /// <summary>All values of a response header, or an empty list.</summary>
    protected static IReadOnlyList<string> Header(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? [.. values]
            : [];
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
