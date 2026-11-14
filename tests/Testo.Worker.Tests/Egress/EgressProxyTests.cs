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
