using Npgsql;

namespace Autestiq.Persistence;

/// <summary>
/// A PostgreSQL session advisory lock, held on a connection of its own for as long as this object lives (Milestone
/// 5.1 R1). If the process dies, the connection closes and PostgreSQL releases the lock, so a stuck holder cannot keep
/// it. The key is scoped to the current database, so separate databases on one server do not share it.
/// </summary>
public sealed class AdvisoryLock : IAsyncDisposable, IDisposable
{
    private readonly NpgsqlConnection _connection;

    private AdvisoryLock(NpgsqlConnection connection) => _connection = connection;

    /// <summary>Takes the lock if nobody holds it; <c>null</c> when another session does.</summary>
    /// <param name="connectionString">The database's connection string.</param>
    /// <param name="key">What is locked, unique per purpose within the product.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    public static async Task<AdvisoryLock?> TryTakeAsync(string connectionString, int key, CancellationToken cancellationToken)
    {
        // Never pooled: a pooled connection goes back to the pool when disposed, its session and so the lock with it.
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtext(current_database()), @key)", connection);
            command.Parameters.AddWithValue("key", key);

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
            {
                return new AdvisoryLock(connection);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        return null;
    }

    /// <summary>Whether the lock's connection still answers. A lost connection means the lock is lost too.</summary>
    public async Task<bool> StillHeldAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT 1", _connection);
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // The connection is closed or broken.
            return false;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
