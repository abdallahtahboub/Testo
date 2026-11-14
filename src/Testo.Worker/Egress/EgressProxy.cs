using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Testo.Worker.Egress;

/// <summary>
/// The worker's only route out. Everything the browser does passes through here, and what is not
/// allowed does not happen.
/// </summary>
/// <remarks>
/// <para>
/// The browser is started with this as its proxy, so navigation, <c>fetch</c>, WebSockets,
/// prefetch and service workers all arrive at the same place. That is the point of a proxy rather
/// than a hook on navigation: there is one door, not eight (ADR-0018).
/// </para>
/// <para>
/// A name is resolved once, judged, and then connected to <em>by the address that was judged</em>.
/// Nothing re-resolves between the check and the connection, which is what closes DNS rebinding.
/// </para>
/// </remarks>
public sealed class EgressProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly EgressPolicy _policy;
    private readonly Action<string> _onRefused;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _accepting;
    private long _bytesDownloaded;

    private EgressProxy(TcpListener listener, EgressPolicy policy, Action<string> onRefused)
    {
        _listener = listener;
        _policy = policy;
        _onRefused = onRefused;
        _accepting = AcceptAsync();
    }

    /// <summary>Where the browser should send everything.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>How much the browser has fetched, for the run's budget and its record.</summary>
    public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);

    /// <summary>Starts listening on a free loopback port.</summary>
    /// <param name="policy">What may be reached.</param>
    /// <param name="onRefused">Told about every refusal, so the run can say what was blocked.</param>
    public static EgressProxy Start(EgressPolicy policy, Action<string> onRefused)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return new EgressProxy(listener, policy, onRefused);
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            // Each connection on its own, so one slow or hostile peer cannot hold up the rest.
            _ = HandleAsync(client);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadRequestHeadAsync(stream).ConfigureAwait(false);

                if (request is null)
                {
                    return;
                }

                if (request.Value.Method.Equals("CONNECT", StringComparison.Ordinal))
                {
                    await TunnelAsync(stream, request.Value).ConfigureAwait(false);
                }
                else
                {
                    await ForwardAsync(stream, request.Value).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // The browser went away mid-request. Not interesting.
            }
            catch (SocketException)
            {
                // Neither is a connection that could not be made.
            }
        }
    }

    /// <summary>A CONNECT tunnel: the browser's own TLS, through a socket we opened for it.</summary>
    private async Task TunnelAsync(NetworkStream browser, Request request)
    {
        var target = await ApproveAsync(request.Host, request.Port, "https").ConfigureAwait(false);

        if (target is null)
        {
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        using var upstream = new TcpClient();
        await upstream.ConnectAsync(target, request.Port).ConfigureAwait(false);

        await browser.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"))
            .ConfigureAwait(false);

        var up = CopyAsync(browser, upstream.GetStream(), counting: false);
        var down = CopyAsync(upstream.GetStream(), browser, counting: true);

        await Task.WhenAny(up, down).ConfigureAwait(false);
    }

    /// <summary>A plain HTTP request in absolute form, which is how a browser talks to a proxy.</summary>
    private async Task ForwardAsync(NetworkStream browser, Request request)
    {
        if (!Uri.TryCreate(request.Target, UriKind.Absolute, out var uri))
        {
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        var target = await ApproveAsync(uri.Host, uri.Port, uri.Scheme).ConfigureAwait(false);

        if (target is null)
        {
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        using var upstream = new TcpClient();
        await upstream.ConnectAsync(target, uri.Port).ConfigureAwait(false);

        var upstreamStream = upstream.GetStream();

        // Re-written in origin form, with the host the browser asked for, so the far end sees the
        // request it would have received directly.
        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{request.Method} {uri.PathAndQuery} HTTP/1.1\r\n")
            .Append(CultureInfo.InvariantCulture, $"Host: {uri.Authority}\r\n");

        foreach (var header in request.Headers)
        {
            if (!header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
                && !header.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase))
            {
                head.Append(CultureInfo.InvariantCulture, $"{header}\r\n");
            }
        }

        head.Append("\r\n");

        await upstreamStream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);

        var up = CopyAsync(browser, upstreamStream, counting: false);
        var down = CopyAsync(upstreamStream, browser, counting: true);

        await Task.WhenAny(up, down).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves, asks the policy about every address, and returns the one address to connect to.
    /// Nothing resolves again afterwards.
    /// </summary>
    private async Task<IPAddress?> ApproveAsync(string host, int port, string scheme)
    {
        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, _stopping.Token).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            addresses = [];
        }

        var decision = _policy.Check(host, port, scheme, addresses);

        if (!decision.Allowed)
        {
            _onRefused($"{scheme}://{host}:{port} — {decision.Reason}");
            return null;
        }

        return addresses[0];
    }

    private static async Task RefuseAsync(NetworkStream browser) =>
        await browser.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"))
            .ConfigureAwait(false);

    private async Task CopyAsync(Stream from, Stream to, bool counting)
    {
        var buffer = new byte[16 * 1024];

        try
        {
            int read;

            while ((read = await from.ReadAsync(buffer, _stopping.Token).ConfigureAwait(false)) > 0)
            {
                if (counting)
                {
                    Interlocked.Add(ref _bytesDownloaded, read);
                }

                await to.WriteAsync(buffer.AsMemory(0, read), _stopping.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or SocketException)
        {
            // One side closed. The other will be closed with it.
        }
    }

    /// <summary>Reads the request line and headers, and nothing of the body.</summary>
    private static async Task<Request?> ReadRequestHeadAsync(NetworkStream stream)
    {
        var head = new StringBuilder();
        var one = new byte[1];

        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(one).ConfigureAwait(false) == 0)
            {
                return null;
            }

            head.Append((char)one[0]);

            // A request head this long is not a browser.
            if (head.Length > 64 * 1024)
            {
                return null;
            }
        }

        var lines = head.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var parts = lines[0].Split(' ');

        if (parts.Length < 2)
        {
            return null;
        }

        var (host, port) = SplitAuthority(parts[1]);

        return new Request(parts[0], parts[1], host, port, [.. lines.Skip(1)]);
    }

    /// <summary>Splits the <c>host:port</c> of a CONNECT target.</summary>
    private static (string Host, int Port) SplitAuthority(string target)
    {
        var colon = target.LastIndexOf(':');

        if (colon > 0 && int.TryParse(target[(colon + 1)..], CultureInfo.InvariantCulture, out var port))
        {
            return (target[..colon].Trim('[', ']'), port);
        }

        return (target, 443);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        _stopping.Dispose();
    }

    private readonly record struct Request(
        string Method,
        string Target,
        string Host,
        int Port,
        IReadOnlyList<string> Headers);
}
