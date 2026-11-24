using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Autestiq.Application.Execution;
using Autestiq.Application.Projects;
using Autestiq.Persistence;

namespace Autestiq.Application.Operations;

/// <summary>One thing the application needs before it can serve, and whether it has it.</summary>
/// <param name="Name">What was checked, for operators.</param>
/// <param name="Ready">Whether it holds.</param>
/// <param name="Reason">Why not, for the log. Never shown to whoever asked: it can name servers and directories.</param>
public sealed record ReadinessCheck(string Name, bool Ready, string? Reason = null);

/// <summary>
/// What the readiness endpoint asks (Milestone 5.1 R2): the database answers and has every migration this build
/// expects; the key ring encrypts and decrypts; and, when runs are sealed in containers, the worker image is there.
/// Liveness asks nothing: a process that answers is alive.
/// </summary>
public sealed class Readiness(IServiceScopeFactory scopes, IOptionsMonitor<ExecutionOptions> execution)
{
    /// <summary>Every check, in a fixed order.</summary>
    public async Task<IReadOnlyList<ReadinessCheck>> CheckAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        return
        [
            await DatabaseAsync(scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(), cancellationToken).ConfigureAwait(false),
            KeyRing(scope.ServiceProvider.GetRequiredService<ISecretProtector>()),
            await WorkerImageAsync(execution.CurrentValue, cancellationToken).ConfigureAwait(false),
        ];
    }

    /// <summary>The database answers, and its schema is the one this build was written for.</summary>
    public static async Task<ReadinessCheck> DatabaseAsync(AutestiqDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        try
        {
            var (_, pending) = await DatabaseMigrations.StateAsync(database, cancellationToken).ConfigureAwait(false);

            return pending.Count == 0
                ? new ReadinessCheck("database", true)
                : new ReadinessCheck("database", false, $"{pending.Count} migration(s) not applied; run the operator's migrate command.");
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return new ReadinessCheck("database", false, exception.GetBaseException().Message);
        }
    }

    /// <summary>A value encrypts and reads back: the key ring is there and usable (ADR-0033).</summary>
    public static ReadinessCheck KeyRing(ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        const string Purpose = "Autestiq.Readiness.v1";
        const string Probe = "readiness";

        try
        {
            return protector.Unprotect(Purpose, protector.Protect(Purpose, Probe)) == Probe
                ? new ReadinessCheck("key-ring", true)
                : new ReadinessCheck("key-ring", false, "A value written with the key ring did not read back.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException)
        {
            return new ReadinessCheck("key-ring", false, exception.Message);
        }
    }

    /// <summary>In container mode, the worker image the dispatcher would start is present. Otherwise nothing to check.</summary>
    public static async Task<ReadinessCheck> WorkerImageAsync(ExecutionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Isolation != WorkerIsolation.Container)
        {
            return new ReadinessCheck("worker-image", true);
        }

        try
        {
            var (exitCode, _, errors) = await new DockerCommand(options.Docker)
                .RunAsync(["image", "inspect", "--format", "{{.Id}}", options.WorkerImage], cancellationToken)
                .ConfigureAwait(false);

            return exitCode == 0
                ? new ReadinessCheck("worker-image", true)
                : new ReadinessCheck("worker-image", false, $"{options.WorkerImage} is not present: {errors}");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return new ReadinessCheck("worker-image", false, $"{options.Docker} could not be started: {exception.Message}");
        }
    }
}
