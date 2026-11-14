using System.Net;
using System.Net.Sockets;
using System.Text;
using Testo.Worker.Egress;

namespace Testo.Worker.Tests.Egress;

/// <summary>
/// The policy decides and the proxy enforces. These drive it over a real socket, because a policy
/// nothing consults is a wish.
/// </summary>
public class EgressProxyTests
{
    [Fact]
    public async Task Connect_ToAnOriginTheRunDidNotName_IsRefusedWithoutConnecting()
    {
        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(new EgressPolicy([], []), refusals.Add);

        var answer = await SendAsync(proxy.Port, "CONNECT collect.example:443 HTTP/1.1\r\nHost: collect.example:443\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(refusals, refusal => refusal.Contains("collect.example", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Connect_ToAnAllowedNameThatResolvesToLoopback_IsRefused()
    {
        // The shape of a rebinding attack, done with a name that genuinely resolves locally:
        // the origin is allowed, the address is not, and the address is what decides.
        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(new EgressPolicy(["https://localhost:443"], []), refusals.Add);

        var answer = await SendAsync(proxy.Port, "CONNECT localhost:443 HTTP/1.1\r\nHost: localhost:443\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(refusals, refusal => refusal.Contains("this machine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_AnInternalAddressInAbsoluteForm_IsRefused()
    {
        // What a page's own fetch() looks like when it arrives at a proxy: not a navigation, and
        // caught all the same, because there is only one door.
        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(new EgressPolicy(["http://169.254.169.254:80"], []), refusals.Add);

        var answer = await SendAsync(
            proxy.Port,
            "GET http://169.254.169.254/latest/meta-data/ HTTP/1.1\r\nHost: 169.254.169.254\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(refusals, refusal => refusal.Contains("cloud metadata lives", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_AnAllowedLocalOrigin_ReachesItAndCountsWhatItFetched()
    {
        // The demo application's shape: ours, loopback, named as trusted by the job.
        using var target = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        var port = ((IPEndPoint)target.LocalEndpoint).Port;

        var serving = ServeOnceAsync(target, "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");

        await using var proxy = EgressProxy.Start(
            new EgressPolicy([], [$"http://127.0.0.1:{port}"]),
            _ => Assert.Fail("nothing should have been refused"));

        var answer = await SendAsync(
            proxy.Port,
            $"GET http://127.0.0.1:{port}/ HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");

        await serving;

        Assert.StartsWith("HTTP/1.1 200", answer, StringComparison.Ordinal);
        Assert.Contains("hello", answer, StringComparison.Ordinal);
        Assert.True(proxy.BytesDownloaded > 0, "what came back should be counted against the budget.");
    }

    [Fact]
    public async Task Get_AnAllowedOriginThatRedirectsSomewhereItMayNotGo_IsRefusedAtTheRedirect()
    {
        // The first hop is allowed and the second is not. A check on the address first asked for
        // would pass this; the proxy judges every connection, so the redirect is a new question
        // with its own answer (docs/execution-threat-model.md §4.1).
        using var target = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        var port = ((IPEndPoint)target.LocalEndpoint).Port;

        var serving = ServeOnceAsync(
            target,
            "HTTP/1.1 302 Found\r\nLocation: http://10.0.0.5/secrets\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(
            new EgressPolicy([], [$"http://127.0.0.1:{port}"]),
            refusals.Add);

        using var handler = new HttpClientHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{proxy.Port}"),
            UseProxy = true,
            AllowAutoRedirect = true,
        };

        using var client = new HttpClient(handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/"), timeout.Token);

        await serving;

        // The redirect was followed, and the place it led to was refused by name.
        //
        // Which gate caught it is worth knowing: the origin one, because 10.0.0.5 is not an origin
        // this run may reach at all. The address gate behind it would have refused it too. Both
        // are checked on the redirect, because the redirect is a new connection and therefore a
        // new question.
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(refusals, refusal => refusal.Contains("10.0.0.5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_ARedirectToAnAllowedNameThatResolvesLocally_IsRefusedByTheAddress()
    {
        // The other half of the redirect question: this time the place it leads to *is* an allowed
        // origin, and it is refused anyway, because the address it resolves to is this machine.
        // That is the case a hostname allowlist cannot catch.
        using var target = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        var port = ((IPEndPoint)target.LocalEndpoint).Port;

        var serving = ServeOnceAsync(
            target,
            "HTTP/1.1 302 Found\r\nLocation: http://localhost:80/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(
            new EgressPolicy(["http://localhost:80"], [$"http://127.0.0.1:{port}"]),
            refusals.Add);

        using var handler = new HttpClientHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{proxy.Port}"),
            UseProxy = true,
            AllowAutoRedirect = true,
        };

        using var client = new HttpClient(handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/"), timeout.Token);

        await serving;

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(refusals, refusal => refusal.Contains("this machine", StringComparison.Ordinal));
    }

    /// <summary>Sends one request and reads whatever comes back until the far end closes.</summary>
    private static async Task<string> SendAsync(int proxyPort, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort);

        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        return await reader.ReadToEndAsync(timeout.Token);
    }

    /// <summary>Answers one connection with a fixed response, then stops.</summary>
    private static async Task ServeOnceAsync(TcpListener listener, string response)
    {
        using var connection = await listener.AcceptTcpClientAsync();
        var stream = connection.GetStream();

        // Enough of the request to know it arrived; the response does not depend on it.
        var buffer = new byte[4096];
        _ = await stream.ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: false);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }
}
