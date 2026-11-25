using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Autestiq.Application.Execution;

/// <summary>How a worker is run.</summary>
public enum WorkerIsolation
{
    /// <summary>
    /// A process on this machine, its proxy inside it. For development, the test suite and the benchmark, which run
    /// our own applications on loopback. Never for a customer's target (ADR-0026).
    /// </summary>
    Process = 1,

    /// <summary>A sealed container per run, with the proxy in a container of its own outside it (ADR-0026).</summary>
    Container = 2,
}

/// <summary>
/// The Docker commands of a sealed run (ADR-0026), in one place, so that the dispatcher and the tests that check the
/// gate start containers exactly the same way.
/// </summary>
/// <remarks>
/// Every value here is ours: run ids, names derived from them, the image from configuration, and origins the run
/// already holds. Each is a separate argument, never joined into a command line, so nothing is read by a shell.
/// </remarks>
public static class SealedRun
{
    /// <summary>The label on every container and network a run makes, so a lost one can be found and removed.</summary>
    public const string Label = "autestiq.run";

    /// <summary>Where the run's workspace is inside the worker's container: the one place it may write that lasts.</summary>
    public const string Workspace = "/run/workspace";

    /// <summary>The port the outer proxy listens on inside its container.</summary>
    public const int ProxyPort = 3128;

    /// <summary>What the outer proxy writes once it is listening.</summary>
    public const string ProxyReady = "ready";

    /// <summary>The setting that tells the worker where its outer proxy is.</summary>
    public const string UpstreamVariable = "AUTESTIQ_UPSTREAM_PROXY";

    public static string NetworkName(Guid runId) => $"autestiq-run-{runId:N}";

    public static string ProxyName(Guid runId) => $"autestiq-proxy-{runId:N}";

    public static string WorkerName(Guid runId) => $"autestiq-worker-{runId:N}";

    /// <summary>
    /// A network for this run alone: internal, so it has no route out, and with no address for the Docker host on
    /// it, so the host's own services cannot be reached either. Measured in ADR-0026: without <c>inhibit_ipv4</c>
    /// an internal network still reaches the host.
    /// </summary>
    public static IReadOnlyList<string> CreateNetwork(Guid runId) =>
    [
        "network", "create", "--internal",
        "--opt", "com.docker.network.bridge.inhibit_ipv4=true",
        "--label", $"{Label}={runId}",
        NetworkName(runId),
    ];

    /// <summary>
    /// The outer proxy, started on the default bridge so that it can reach the internet, then joined to the run's
    /// network (<see cref="ConnectProxy"/>). It is told the origins it allows, and nothing else of the run.
    /// </summary>
    public static IReadOnlyList<string> RunProxy(Guid runId, string image, IReadOnlyList<string> allowedOrigins) =>
    [
        "run", "--detach", "--name", ProxyName(runId), "--label", $"{Label}={runId}",
        "--network", "bridge",
        .. Hardening(memory: "256m", cpus: "0.5", processes: 128),
        image, "--proxy", .. allowedOrigins,
    ];

    public static IReadOnlyList<string> ConnectProxy(Guid runId) => ["network", "connect", NetworkName(runId), ProxyName(runId)];

    /// <summary>The outer proxy's address on the run's network.</summary>
    public static IReadOnlyList<string> ProxyAddress(Guid runId) =>
        ["inspect", "--format", $"{{{{(index .NetworkSettings.Networks \"{NetworkName(runId)}\").IPAddress}}}}", ProxyName(runId)];

    public static IReadOnlyList<string> ProxyLogs(Guid runId) => ["logs", ProxyName(runId)];

    /// <summary>
    /// The worker, on the run's network only, locked down: not root, no capabilities, no privilege escalation, a
    /// read-only root, small scratch space, limits on memory, CPU and processes, no credentials, and one writable
    /// folder for its screenshots. The job arrives on standard input; the result leaves on standard output.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="image">The worker image.</param>
    /// <param name="workspace">The run's workspace on this machine, mounted at <see cref="Workspace"/>.</param>
    /// <param name="proxyAddress">The outer proxy, as <c>address:port</c>.</param>
    /// <param name="entrypoint">A different program to run, for the gate tests only.</param>
    /// <param name="command">Its arguments, for the gate tests only.</param>
    public static IReadOnlyList<string> RunWorker(
        Guid runId,
        string image,
        string workspace,
        string proxyAddress,
        string? entrypoint = null,
        IReadOnlyList<string>? command = null) =>
    [
        "run", "--interactive", "--rm", "--name", WorkerName(runId), "--label", $"{Label}={runId}",
        "--network", NetworkName(runId),
        .. Hardening(memory: "1g", cpus: "1.0", processes: 512),
        "--tmpfs", "/tmp:rw,size=512m,mode=1777",
        "--volume", $"{workspace}:{Workspace}",
        "--env", $"{UpstreamVariable}={proxyAddress}",
        .. entrypoint is null ? Array.Empty<string>() : ["--entrypoint", entrypoint],
        image,
        .. command ?? [],
    ];

    public static IReadOnlyList<string> KillWorker(Guid runId) => ["kill", WorkerName(runId)];

    public static IReadOnlyList<string> RemoveContainers(Guid runId) => ["rm", "--force", WorkerName(runId), ProxyName(runId)];

    public static IReadOnlyList<string> RemoveNetwork(Guid runId) => ["network", "rm", NetworkName(runId)];

    /// <summary>Every container and network any run left, by label: for the clean-up after a lost dispatcher.</summary>
    public static IReadOnlyList<string> ListContainers() => ["ps", "--all", "--filter", $"label={Label}", "--format", $"{{{{.Label \"{Label}\"}}}}"];

    public static IReadOnlyList<string> ListNetworks() => ["network", "ls", "--filter", $"label={Label}", "--format", $"{{{{.Label \"{Label}\"}}}}"];

    private static string[] Hardening(string memory, string cpus, int processes) =>
    [
        "--read-only",
        "--cap-drop", "ALL",
        "--security-opt", "no-new-privileges",
        "--user", "pwuser",
        "--memory", memory,
        "--cpus", cpus,
        "--pids-limit", processes.ToString(CultureInfo.InvariantCulture),
    ];
}

/// <summary>Runs the Docker command line with an argument list, never through a shell.</summary>
public sealed class DockerCommand(string docker)
{
    /// <summary>Runs a command to its end and returns what it said.</summary>
    public async Task<(int ExitCode, string Output, string Errors)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(Start(arguments, input: false))
            ?? throw new InvalidOperationException($"{docker} would not start.");

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, (await output.ConfigureAwait(false)).Trim(), (await errors.ConfigureAwait(false)).Trim());
    }

    /// <summary>How to start a command that is fed on standard input and waited on.</summary>
    public ProcessStartInfo Start(IReadOnlyList<string> arguments, bool input)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var start = new ProcessStartInfo(docker)
        {
            RedirectStandardInput = input,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }
}
