using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Autestiq.Application.Execution;
using Autestiq.Application.Projects;
using Autestiq.Domain.Operations;
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
            await KeyRingAsync(scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(), scope.ServiceProvider.GetRequiredService<ISecretProtector>(), cancellationToken).ConfigureAwait(false),
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

    /// <summary>
    /// The key ring is the one this database's test-account values were written with (ADR-0033, R-20): the canary the
    /// database keeps reads back. On a database without one, the canary is written now, by the first instance to get
    /// there. A key ring that cannot read it, as after restoring the database without its key ring, is not ready.
    /// </summary>
    public static async Task<ReadinessCheck> KeyRingAsync(AutestiqDbContext database, ISecretProtector protector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(protector);

        try
        {
            var canary = await database.KeyRingCanaries.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == KeyRingCanary.OnlyId, cancellationToken)
                .ConfigureAwait(false);

            if (canary is null)
            {
                database.KeyRingCanaries.Add(new KeyRingCanary { Id = KeyRingCanary.OnlyId, Ciphertext = protector.Protect(KeyRingCanary.Purpose, KeyRingCanary.Plaintext) });

                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    // Another instance wrote it first: read that one.
                    database.ChangeTracker.Clear();
                    return await KeyRingAsync(database, protector, cancellationToken).ConfigureAwait(false);
                }

                return new ReadinessCheck("key-ring", true);
            }

            return protector.Unprotect(KeyRingCanary.Purpose, canary.Ciphertext) == KeyRingCanary.Plaintext
                ? new ReadinessCheck("key-ring", true)
                : new ReadinessCheck("key-ring", false,
                    "The key ring cannot read the database's canary: it is not the key ring the database's test-account values were written with. "
                    + "Restore the key ring that belongs to this database, or, if it is lost for good, run the operator's key-ring-reset.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.Cryptography.CryptographicException or IOException
            or UnauthorizedAccessException or NpgsqlException or TimeoutException)
        {
            return new ReadinessCheck("key-ring", false, exception.GetBaseException().Message);
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
