using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// The gate ADR-0026 and the threat model set before any customer target: from inside a worker container, started
/// exactly as the dispatcher starts one, nothing but the outer proxy can be reached.
/// </summary>
/// <remarks>
/// These need Docker and the worker image (<c>AUTESTIQ_WORKER_IMAGE</c>, <c>autestiq-worker:dev</c> by default),
/// which CI builds before the tests and <c>CLAUDE.md</c> says how to build. They fail, rather than pass quietly,
/// when either is missing: a gate that is skipped is not a gate.
/// </remarks>
[Collection(SealedContainers.Name)]
public sealed class SealedContainerTests : IAsyncLifetime
{
    private static readonly string Image = Environment.GetEnvironmentVariable("AUTESTIQ_WORKER_IMAGE") is { Length: > 0 } image ? image : "autestiq-worker:dev";

    private readonly DockerCommand _docker = new("docker");
    private readonly Guid _runId = Guid.CreateVersion7();
    private TcpListener? _hostService;
    private string _proxy = "";
    private string _hostAddress = "";
    private int _hostPort;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await PrepareAsync();
        }
        catch
        {
            // xUnit does not dispose a fixture whose initialization failed; what was made is removed here instead.
            await DisposeAsync();
            throw;
        }
    }

    private async Task PrepareAsync()
    {
        var (exitCode, _, errors) = await _docker.RunAsync(["image", "inspect", Image], TestContext.Current.CancellationToken);
        Assert.True(exitCode == 0, $"Docker and the worker image {Image} are needed (see CLAUDE.md, Commands): {errors}");

        // A service on the Docker host, listening on every address, as a database or an admin page would.
        _hostService = new TcpListener(IPAddress.Any, 0);
        _hostService.Start();
        _hostPort = ((IPEndPoint)_hostService.LocalEndpoint).Port;
        _hostAddress = (await _docker.RunAsync(["network", "inspect", "bridge", "--format", "{{(index .IPAM.Config 0).Gateway}}"], TestContext.Current.CancellationToken)).Output;

        foreach (var command in new[] { SealedRun.CreateNetwork(_runId), SealedRun.RunProxy(_runId, Image, ["https://shop.example:443"]), SealedRun.ConnectProxy(_runId) })
        {
            var (code, _, why) = await _docker.RunAsync(command, TestContext.Current.CancellationToken);
            Assert.True(code == 0, $"{string.Join(' ', command.Take(2))} failed: {why}");
        }

        var address = (await _docker.RunAsync(SealedRun.ProxyAddress(_runId), TestContext.Current.CancellationToken)).Output;
        _proxy = $"{address}:{SealedRun.ProxyPort}";

        for (var attempt = 0; attempt < 50; attempt++)
        {
            if ((await _docker.RunAsync(SealedRun.ProxyLogs(_runId), TestContext.Current.CancellationToken)).Output.Contains(SealedRun.ProxyReady, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        Assert.Fail("The outer proxy never said it was ready.");
    }

    public async ValueTask DisposeAsync()
    {
        await _docker.RunAsync(SealedRun.RemoveContainers(_runId), CancellationToken.None);
        await _docker.RunAsync(SealedRun.RemoveNetwork(_runId), CancellationToken.None);
        _hostService?.Stop();
    }

    /// <summary>Runs a shell script in a worker container started exactly as the dispatcher starts one.</summary>
    private async Task<string> InWorkerAsync(string script)
    {
        var workspace = Directory.CreateTempSubdirectory("autestiq-sealed-").FullName;

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(workspace, (UnixFileMode)0b111_111_111);
        }

        var (_, output, errors) = await _docker.RunAsync(
            SealedRun.RunWorker(_runId, Image, workspace, _proxy, entrypoint: "bash", command: ["-c", script]),
            TestContext.Current.CancellationToken);

        return output + "\n" + errors;
    }

    private static string Probe(string name, string host, int port) =>
        $"if timeout 4 bash -c 'exec 3<>/dev/tcp/{host}/{port}' 2>/dev/null; then echo '{name}=reachable'; else echo '{name}=blocked'; fi;";

    [Fact]
    public async Task FromTheWorker_OnlyTheOuterProxyCanBeReached()
    {
        var proxyAddress = _proxy.Split(':')[0];

        var said = await InWorkerAsync(
            Probe("proxy", proxyAddress, SealedRun.ProxyPort)
            + Probe("host-service", _hostAddress, _hostPort)
            + Probe("host-database", _hostAddress, 5432)
            + Probe("metadata", "169.254.169.254", 80)
            + Probe("internet", "1.1.1.1", 443));

        // The positive control first: if the proxy were unreachable too, every "blocked" below would prove nothing.
        Assert.Contains("proxy=reachable", said, StringComparison.Ordinal);
        Assert.Contains("host-service=blocked", said, StringComparison.Ordinal);
        Assert.Contains("host-database=blocked", said, StringComparison.Ordinal);
        Assert.Contains("metadata=blocked", said, StringComparison.Ordinal);
        Assert.Contains("internet=blocked", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThroughTheOuterProxy_TheDockerHostIsRefused()
    {
        // The other way round: asking the proxy to fetch the host. It resolves the address and refuses a private one,
        // even though the proxy itself sits on the bridge where the host is one hop away.
        var proxyAddress = _proxy.Split(':')[0];

        var said = await InWorkerAsync(
            $"exec 3<>/dev/tcp/{proxyAddress}/{SealedRun.ProxyPort}; "
            + $"printf 'GET http://{_hostAddress}:{_hostPort}/ HTTP/1.1\\r\\nHost: {_hostAddress}:{_hostPort}\\r\\n\\r\\n' >&3; "
            + "timeout 5 head -n 1 <&3");

        Assert.Contains("403", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWorker_IsNotRoot_CannotWriteItsRoot_AndHoldsNoCredential()
    {
        var said = await InWorkerAsync(
            "echo uid=$(id -u); "
            + "if touch /etc/autestiq 2>/dev/null; then echo root-writable; else echo root-read-only; fi; "
            + "if touch /run/workspace/screenshot 2>/dev/null; then echo workspace-writable; fi; "
            + "env | cut -d= -f1 | grep -iE 'key|secret|password|token|connection' || echo no-credentials");

        Assert.DoesNotContain("uid=0", said, StringComparison.Ordinal);
        Assert.Contains("root-read-only", said, StringComparison.Ordinal);
        Assert.Contains("workspace-writable", said, StringComparison.Ordinal);
        Assert.Contains("no-credentials", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRunsNetwork_GivesTheHostNoAddress()
    {
        // The option the whole design rests on. A Docker version that dropped it would reopen the host silently.
        var (_, output, _) = await _docker.RunAsync(
            ["network", "inspect", SealedRun.NetworkName(_runId), "--format", "{{.Internal}} {{index .Options \"com.docker.network.bridge.inhibit_ipv4\"}}"],
            TestContext.Current.CancellationToken);

        Assert.Equal("true true", output);
    }
}

/// <summary>A run carried out by a dispatcher in container mode, and what it leaves behind: nothing.</summary>
[Collection(SealedContainers.Name)]
public sealed class ContainerDispatcherTests : IAsyncLifetime
{
    private static readonly string Image = Environment.GetEnvironmentVariable("AUTESTIQ_WORKER_IMAGE") is { Length: > 0 } image ? image : "autestiq-worker:dev";

    private TestDatabase _database = null!;
    private Guid _organization;
    private Guid _project;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_organization, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        _project = await ExecutionSupport.ProjectAsync(_database, _organization, "SHOP");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task ARunInASealedContainer_EndsWithAVerdict_AndLeavesNoContainerOrNetwork()
    {
        // Our own demo address, which a sealed run cannot reach by design: the browser's request is refused, so the
        // run ends saying the site could not be reached, not with an infrastructure error, and everything it made is removed.
        var options = new ChangeableOptions(new ExecutionOptions
        {
            Isolation = WorkerIsolation.Container,
            WorkerImage = Image,
            MaxConcurrentRuns = 1,
            TargetOrigin = "http://127.0.0.1:5090",
        });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);
        var runId = await ExecutionSupport.RunAsync(_database, _organization, _project);

        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            var finished = await FinishedAsync(runId, TimeSpan.FromMinutes(3));

            // Not an infrastructure error, and not "Failed" either: the site was never reached, and the run says so
            // (ADR-0026 amendment, 2026-11-25).
            Assert.True(finished.Status == RunStatus.Inconclusive, $"{finished.Status}: {finished.FailureReason}");
            Assert.Contains("could not be reached", finished.FailureReason, StringComparison.Ordinal);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }

        var docker = new DockerCommand("docker");
        var containers = (await docker.RunAsync(["ps", "--all", "--filter", $"label={SealedRun.Label}={runId}", "--format", "{{.Names}}"], CancellationToken.None)).Output;
        var networks = (await docker.RunAsync(["network", "ls", "--filter", $"label={SealedRun.Label}={runId}", "--format", "{{.Name}}"], CancellationToken.None)).Output;

        Assert.Equal("", containers);
        Assert.Equal("", networks);
    }

    private async Task<Domain.Execution.TestRun> FinishedAsync(Guid runId, TimeSpan within)
    {
        var deadline = DateTime.UtcNow + within;

        while (DateTime.UtcNow < deadline)
        {
            var run = await ExecutionSupport.StoredAsync(_database, runId);

            if (run.Status.IsFinished())
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        return await ExecutionSupport.StoredAsync(_database, runId);
    }
}

/// <summary>One at a time: these start real containers and networks.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SealedContainers
{
    public const string Name = "sealed containers";
}
