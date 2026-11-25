using System.Net;
using System.Runtime.InteropServices;
using Autestiq.Worker.Egress;

namespace Autestiq.Worker;

/// <summary>
/// The worker image's second role: the outer proxy of a sealed run (ADR-0026). The only thing on the run's network
/// that the worker can reach, and the only thing that reaches anything beyond it.
/// </summary>
/// <remarks>
/// It takes the origins it allows as arguments, trusts nothing as local, listens on
/// <see cref="Port"/> on every address of its container, says "ready", and serves until it is stopped. Every name
/// is resolved here and every address judged, so a name that leads to a private, loopback or metadata address,
/// including the Docker host's own address on the bridge, is refused.
/// </remarks>
internal static class OuterProxy
{
    /// <summary>The port the outer proxy listens on inside its container.</summary>
    public const int Port = 3128;

    /// <summary>What the outer proxy writes on standard output once it is listening.</summary>
    public const string Ready = "ready";

    /// <summary>Serves as the outer proxy for the origins given after <c>--proxy</c>.</summary>
    /// <remarks>
    /// The origins come as arguments, not as the job: the outer proxy never sees a run's secrets or its plan, only
    /// where it may go. They are not secret, and the dispatcher writes them from the run's job.
    /// </remarks>
    public static async Task<int> RunAsync(IReadOnlyList<string> allowedOrigins, CancellationToken cancellationToken)
    {
        if (allowedOrigins.Count == 0)
        {
            await Console.Error.WriteLineAsync("The outer proxy expects the origins it allows after --proxy.").ConfigureAwait(false);
            return 2;
        }

        // Trusted-local is the worker's own business on a developer's machine. In a sealed run nothing is.
        await using var proxy = EgressProxy.Start(
            new EgressPolicy(allowedOrigins, trustedLocalOrigins: []),
            refusal => Console.Error.WriteLine($"refused: {refusal}"),
            IPAddress.Any,
            Port,
            upstream: null);

        await Console.Out.WriteLineAsync(Ready).ConfigureAwait(false);
        await Console.Out.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Serves until the container is stopped.
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => stopped.TrySetResult());
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => stopped.TrySetResult());
        using var cancelled = cancellationToken.Register(() => stopped.TrySetResult());

        await stopped.Task.ConfigureAwait(false);
        return 0;
    }
}
