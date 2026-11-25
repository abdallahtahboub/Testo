using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Autestiq.Worker.Egress;

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
    private readonly DnsEndPoint? _upstream;
    private long _bytesDownloaded;

    private EgressProxy(TcpListener listener, EgressPolicy policy, Action<string> onRefused, DnsEndPoint? upstream)
    {
        _listener = listener;
        _policy = policy;
        _onRefused = onRefused;
        _upstream = upstream;
        _accepting = AcceptAsync();
    }

    /// <summary>Where the browser should send everything.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// Whether only reads pass: a plain-http request other than GET, HEAD or OPTIONS is refused here as well as in the
    /// browser (ADR-0041). The second layer: the browser's guard let a page's POST through while the page navigated
    /// (R9, 2026-11-25). An https request is inside a tunnel this proxy cannot read, so there the browser's guard is
    /// the only one.
    /// </summary>
    public bool ReadsOnly { get; set; }

    /// <summary>How much the browser has fetched, for the run's budget and its record.</summary>
    public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);

    /// <summary>Starts listening on a free loopback port.</summary>
    /// <param name="policy">What may be reached.</param>
    /// <param name="onRefused">Told about every refusal, so the run can say what was blocked.</param>
    public static EgressProxy Start(EgressPolicy policy, Action<string> onRefused) =>
        Start(policy, onRefused, IPAddress.Loopback, port: 0, upstream: null);

    /// <summary>Starts listening where it is told, and optionally sends everything through another proxy.</summary>
    /// <param name="policy">What may be reached.</param>
    /// <param name="onRefused">Told about every refusal.</param>
    /// <param name="address">Where to listen: loopback for the worker's own proxy, all addresses for the outer one.</param>
    /// <param name="port">The port, or 0 for any free one.</param>
    /// <param name="upstream">
    /// The outer proxy (ADR-0026). When set, this proxy judges each request by its origin only and opens no
    /// connection of its own: everything goes through the outer proxy, which resolves names and judges addresses,
    /// and is the only thing a sealed run network lets the worker reach.
    /// </param>
    public static EgressProxy Start(EgressPolicy policy, Action<string> onRefused, IPAddress address, int port, DnsEndPoint? upstream)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(address);

        var listener = new TcpListener(address, port);
        listener.Start();

        return new EgressProxy(listener, policy, onRefused, upstream);
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
        if (_upstream is { } outer)
        {
            await TunnelThroughAsync(browser, request, outer).ConfigureAwait(false);
            return;
        }

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

        if (ReadsOnly && !OnlyReads(request.Method))
        {
            // Not written as an origin: a refused write is not a site that could not be reached (Unreachable).
            _onRefused($"a {request.Method} to {uri.AbsolutePath}, which a guarded run does not send");
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        if (_upstream is { } outer)
        {
            await ForwardThroughAsync(browser, request, uri, outer).ConfigureAwait(false);
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

        AppendHeaders(head, request.Headers);

        await upstreamStream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);

        var up = CopyAsync(browser, upstreamStream, counting: false);
        var down = CopyAsync(upstreamStream, browser, counting: true);

        await Task.WhenAny(up, down).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves, asks the policy about every address, and returns the addresses to connect to, in the order the
    /// name gave them. Nothing resolves again afterwards.
    /// </summary>
    /// <remarks>
    /// All of them, not the first: the policy allows a name only when every address it resolves to is allowed, so
    /// any of them is as approved as the others. "localhost" resolves to ::1 before 127.0.0.1 on many machines,
    /// and an application listening on IPv4 alone refused the first and was never tried on the second.
    /// </remarks>
    private async Task<IPAddress[]?> ApproveAsync(string host, int port, string scheme)
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

        return addresses;
    }

    /// <summary>A CONNECT tunnel opened through the outer proxy, which judges the addresses.</summary>
    private async Task TunnelThroughAsync(NetworkStream browser, Request request, DnsEndPoint outer)
    {
        if (!Approved(request.Host, request.Port, "https"))
        {
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        using var upstream = new TcpClient();
        await upstream.ConnectAsync(outer.Host, outer.Port).ConfigureAwait(false);
        var stream = upstream.GetStream();

        var authority = $"{request.Host}:{request.Port.ToString(CultureInfo.InvariantCulture)}";
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"CONNECT {authority} HTTP/1.1\r\nHost: {authority}\r\n\r\n"))
            .ConfigureAwait(false);

        // The outer proxy's answer, read to the end of its head and no further, so nothing of the tunnel is lost.
        var answer = await ReadRequestHeadAsync(stream).ConfigureAwait(false);

        if (answer is not { Target: "200" })
        {
            _onRefused($"https://{authority} — refused by the outer proxy");
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        await browser.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n")).ConfigureAwait(false);

        var up = CopyAsync(browser, stream, counting: false);
        var down = CopyAsync(stream, browser, counting: true);

        await Task.WhenAny(up, down).ConfigureAwait(false);
    }

    /// <summary>A plain HTTP request passed on to the outer proxy as one, so that it judges it as http.</summary>
    private async Task ForwardThroughAsync(NetworkStream browser, Request request, Uri uri, DnsEndPoint outer)
    {
        if (!Approved(uri.Host, uri.Port, uri.Scheme))
        {
            await RefuseAsync(browser).ConfigureAwait(false);
            return;
        }

        using var upstream = new TcpClient();
        await upstream.ConnectAsync(outer.Host, outer.Port).ConfigureAwait(false);
        var stream = upstream.GetStream();

        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{request.Method} {uri.AbsoluteUri} HTTP/1.1\r\n")
            .Append(CultureInfo.InvariantCulture, $"Host: {uri.Authority}\r\n");

        AppendHeaders(head, request.Headers);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);

        var up = CopyAsync(browser, stream, counting: false);
        var down = CopyAsync(stream, browser, counting: true);

        await Task.WhenAny(up, down).ConfigureAwait(false);
    }

    /// <summary>
    /// The browser's headers, less the ones that are the proxy's business, ending the head. The far end is asked to
    /// close the connection after its answer, unless the request upgrades it (a WebSocket).
    /// </summary>
    /// <remarks>
    /// Only the first request on a connection passes through here; the rest are copied as the browser sent them. Kept
    /// open, a connection would carry the browser's next request to the far end in proxy form, with the whole address
    /// on its first line and never checked by the policy. Kestrel and Tomcat read such a request by its path; Node's
    /// server does not, and OWASP Juice Shop answered its own scripts with its HTML page, so nothing rendered (R9,
    /// 2026-11-25). Closed, every request opens a connection of its own, and each is rewritten and judged.
    /// </remarks>
    private static void AppendHeaders(StringBuilder head, IEnumerable<string> headers)
    {
        var upgrading = headers.Any(header => header.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase));

        foreach (var header in headers)
        {
            var connection = header.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
                || header.StartsWith("Keep-Alive:", StringComparison.OrdinalIgnoreCase);

            if (!header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
                && !header.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)
                && (upgrading || !connection))
            {
                head.Append(CultureInfo.InvariantCulture, $"{header}\r\n");
            }
        }

        if (!upgrading)
        {
            head.Append("Connection: close\r\n");
        }

        head.Append("\r\n");
    }

    /// <summary>The origin check this proxy can make without resolving anything.</summary>
    private bool Approved(string host, int port, string scheme)
    {
        var decision = _policy.CheckOrigin(host, port, scheme);

        if (!decision.Allowed)
        {
            _onRefused($"{scheme}://{host}:{port} — {decision.Reason}");
        }

        return decision.Allowed;
    }

    /// <summary>Whether a request with this method only reads, as <c>ReadOnlyGuard</c> judges it in the browser.</summary>
    private static bool OnlyReads(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
        || method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);

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
