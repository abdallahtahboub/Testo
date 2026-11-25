using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Autestiq.Application.Tests.Support;

/// <summary>
/// A minimal SMTP server on loopback that keeps what it is sent, for the email tests (ADR-0040): enough of RFC 5321 for
/// .NET's client without TLS (EHLO, MAIL, RCPT, DATA, QUIT). Not for anything but tests.
/// </summary>
internal sealed class SmtpSink : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;

    public SmtpSink()
    {
        _listener.Start();
        _serving = ServeAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public List<(string From, IReadOnlyList<string> To, string Data)> Messages { get; } = [];

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();

        try
        {
            await _serving;
        }
        catch (OperationCanceledException)
        {
            // Stopped, as asked.
        }

        _stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

            await writer.WriteLineAsync("220 sink ready");
            string from = "";
            var to = new List<string>();

            while (await reader.ReadLineAsync(_stop.Token) is { } line)
            {
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();

                switch (verb)
                {
                    case "EHLO" or "HELO":
                        await writer.WriteLineAsync("250 sink");
                        break;
                    case "MAIL":
                        from = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim(' ', '<', '>');
                        await writer.WriteLineAsync("250 ok");
                        break;
                    case "RCPT":
                        to.Add(line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim(' ', '<', '>'));
                        await writer.WriteLineAsync("250 ok");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 go on");
                        var data = new StringBuilder();

                        while (await reader.ReadLineAsync(_stop.Token) is { } body && body != ".")
                        {
                            data.Append(body.StartsWith("..", StringComparison.Ordinal) ? body[1..] : body).Append('\n');
                        }

                        Messages.Add((from, [.. to], data.ToString()));
                        to.Clear();
                        await writer.WriteLineAsync("250 kept");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 bye");
                        goto Closed;
                    default:
                        await writer.WriteLineAsync("250 ok");
                        break;
                }
            }

        Closed:
            ;
        }
    }
}
