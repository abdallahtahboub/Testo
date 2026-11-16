using System.Net;
using System.Net.Sockets;
using System.Text;
using Autestiq.Worker.Egress;

namespace Autestiq.Worker.Tests.Egress;

/// <summary>
/// The worker's proxy in front of the outer one (ADR-0026): the inner judges origins and counts bytes, the outer
/// resolves and judges addresses, and the inner never connects anywhere but to the outer.
/// </summary>
public class ChainedProxyTests
{
    [Fact]
    public async Task Get_AnOriginBothAllow_GoesThroughTheChain_AndIsCountedByTheInnerProxy()
    {
        using var target = Listen(out var port);
        var serving = ServeOnceAsync(target, "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");

        // The outer trusts the local target only so that a test can have one; in a sealed run nothing is trusted.
        await using var outer = EgressProxy.Start(new EgressPolicy([], [$"http://127.0.0.1:{port}"]), _ => { });
        await using var inner = Inner($"http://127.0.0.1:{port}", outer.Port);

        var answer = await SendAsync(inner.Port, $"GET http://127.0.0.1:{port}/ HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
        await serving;

        Assert.StartsWith("HTTP/1.1 200", answer, StringComparison.Ordinal);
        Assert.Contains("hello", answer, StringComparison.Ordinal);
        Assert.True(inner.BytesDownloaded > 0, "the worker's own proxy still counts what came back, for the budget.");
    }

    [Fact]
    public async Task Get_WhatTheOuterProxyRefuses_IsRefused_ThoughTheInnerAllowedItsOrigin()
    {
        // The inner proxy cannot resolve names in a sealed network, so it lets the origin through; the address is
        // the outer proxy's to judge, and loopback is not allowed there.
        var outerRefusals = new List<string>();
        await using var outer = EgressProxy.Start(new EgressPolicy(["http://localhost:80"], []), outerRefusals.Add);
        await using var inner = Inner("http://localhost:80", outer.Port);

        var answer = await SendAsync(inner.Port, "GET http://localhost/ HTTP/1.1\r\nHost: localhost\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(outerRefusals, refusal => refusal.Contains("this machine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Connect_ThroughTheChain_IsRefusedByTheOuterProxyForAMetadataAddress()
    {
        var outerRefusals = new List<string>();
        await using var outer = EgressProxy.Start(new EgressPolicy(["https://169.254.169.254:443"], []), outerRefusals.Add);
        await using var inner = Inner("https://169.254.169.254:443", outer.Port);

        var answer = await SendAsync(inner.Port, "CONNECT 169.254.169.254:443 HTTP/1.1\r\nHost: 169.254.169.254:443\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(outerRefusals, refusal => refusal.Contains("cloud metadata", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Connect_ThroughTheChain_ToAnAllowedTarget_IsATunnel()
    {
        using var target = Listen(out var port);
        var echoing = EchoOnceAsync(target);

        await using var outer = EgressProxy.Start(new EgressPolicy([], [$"https://127.0.0.1:{port}"]), _ => { });
        await using var inner = Inner($"https://127.0.0.1:{port}", outer.Port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, inner.Port, TestContext.Current.CancellationToken);
        var stream = client.GetStream();

        await stream.WriteAsync(Encoding.ASCII.GetBytes($"CONNECT 127.0.0.1:{port} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n"), TestContext.Current.CancellationToken);
        var established = await ReadHeadAsync(stream);
        await stream.WriteAsync("ping"u8.ToArray(), TestContext.Current.CancellationToken);

        var echo = new byte[4];
        await stream.ReadExactlyAsync(echo, TestContext.Current.CancellationToken);
        await echoing;

        Assert.StartsWith("HTTP/1.1 200", established, StringComparison.Ordinal);
        Assert.Equal("ping", Encoding.ASCII.GetString(echo));
    }

    [Fact]
    public async Task Get_AnOriginTheRunDidNotName_IsRefusedByTheInnerProxy_BeforeAnythingLeaves()
    {
        var innerRefusals = new List<string>();
        await using var outer = EgressProxy.Start(new EgressPolicy(["http://collect.example:80"], []), _ => Assert.Fail("nothing should reach the outer proxy"));
        await using var inner = EgressProxy.Start(new EgressPolicy(["https://shop.example:443"], []), innerRefusals.Add, IPAddress.Loopback, 0, new DnsEndPoint("127.0.0.1", outer.Port));

        var answer = await SendAsync(inner.Port, "GET http://collect.example/ HTTP/1.1\r\nHost: collect.example\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 403", answer, StringComparison.Ordinal);
        Assert.Contains(innerRefusals, refusal => refusal.Contains("collect.example", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_WithTheOuterProxyGone_GetsNothing_BecauseTheInnerNeverConnectsOnItsOwn()
    {
        using var target = Listen(out var port);
        var deadOuter = Listen(out var deadPort);
        deadOuter.Stop();

        await using var inner = Inner($"http://127.0.0.1:{port}", deadPort);

        var answer = await SendAsync(inner.Port, $"GET http://127.0.0.1:{port}/ HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");

        Assert.DoesNotContain("200", answer, StringComparison.Ordinal);
        Assert.False(target.Pending(), "the target was contacted directly, around the outer proxy.");
    }

    private static EgressProxy Inner(string allowedOrigin, int outerPort) =>
        EgressProxy.Start(new EgressPolicy([allowedOrigin], []), _ => { }, IPAddress.Loopback, 0, new DnsEndPoint("127.0.0.1", outerPort));

    private static TcpListener Listen(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static async Task<string> SendAsync(int proxyPort, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort);

        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            return await reader.ReadToEndAsync(timeout.Token);
        }
        catch (IOException)
        {
            return "";
        }
    }

    private static async Task<string> ReadHeadAsync(NetworkStream stream)
    {
        var head = new StringBuilder();
        var one = new byte[1];

        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && await stream.ReadAsync(one) > 0)
        {
            head.Append((char)one[0]);
        }

        return head.ToString();
    }

    private static async Task ServeOnceAsync(TcpListener listener, string response)
    {
        using var connection = await listener.AcceptTcpClientAsync();
        var stream = connection.GetStream();
        var buffer = new byte[4096];
        _ = await stream.ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: false);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }

    private static async Task EchoOnceAsync(TcpListener listener)
    {
        using var connection = await listener.AcceptTcpClientAsync();
        var stream = connection.GetStream();
        var buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer);
        await stream.WriteAsync(buffer);
    }
}
