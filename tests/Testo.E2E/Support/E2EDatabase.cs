using Npgsql;

namespace Testo.E2E.Support;

/// <summary>
/// A PostgreSQL database of its own for the whole browser test run, dropped afterwards.
/// </summary>
/// <remarks>
/// The browser tests drive the real application, and the real application needs a database:
/// signing in is not something a mock can stand in for once the point is to prove that a person
/// can actually get in. Where the server is comes from <c>TESTO_TEST_DB</c>, the same variable
/// the application tests use.
/// </remarks>
internal sealed class E2EDatabase : IAsyncDisposable
{
    private readonly string _databaseName = $"testo_e2e_{Guid.CreateVersion7():N}";
    private readonly string _serverConnectionString;

    private E2EDatabase(string serverConnectionString) => _serverConnectionString = serverConnectionString;

    /// <summary>What the application should be pointed at.</summary>
    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ToString();

    /// <summary>Creates the database. The application applies the migrations when it starts.</summary>
    public static async Task<E2EDatabase> CreateAsync()
    {
        var server = Environment.GetEnvironmentVariable("TESTO_TEST_DB")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

        var database = new E2EDatabase(
            new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ToString());

        try
        {
            await using var connection = new NpgsqlConnection(database._serverConnectionString);
            await connection.OpenAsync();

            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database._databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "The browser tests need a PostgreSQL server, because they drive the real application. "
                + "Start one and point TESTO_TEST_DB at it, for example "
                + "TESTO_TEST_DB=\"Host=localhost;Username=postgres;Password=postgres\". See README.md.",
                exception);
        }

        return database;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();

        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            connection);

        await drop.ExecuteNonQueryAsync();
    }
}
