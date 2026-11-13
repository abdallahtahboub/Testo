using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testo.Persistence;

namespace Testo.Application;

/// <summary>
/// Checks that the database is actually reachable before the application tries to use it.
/// </summary>
/// <remarks>
/// Without this, a server that is not running surfaces as Entity Framework's own connection
/// error: several frames of stack trace that say a connection failed, but not which server was
/// tried, nor what to do about it. The product cannot run without a database, so the one person
/// who needs to know that deserves a sentence rather than a log event number.
/// </remarks>
public static class DatabaseStartup
{
    /// <summary>
    /// Throws with an explanation if the database cannot be reached.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="cancellationToken">Stops the check if the host is shutting down.</param>
    /// <exception cref="InvalidOperationException">The database did not answer.</exception>
    public static async Task EnsureReachableAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();

        try
        {
            if (await database.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
        catch (Exception reason) when (reason is NpgsqlException or InvalidOperationException)
        {
            throw Unreachable(database.Database.GetConnectionString(), reason);
        }

        throw Unreachable(database.Database.GetConnectionString(), inner: null);
    }

    /// <summary>
    /// The message somebody reads when the server is not there. It names the server that was
    /// tried and never the password, because this goes into logs.
    /// </summary>
    private static InvalidOperationException Unreachable(string? connectionString, Exception? inner)
    {
        var where = Describe(connectionString);

        return new InvalidOperationException(
            $"""
            The database did not answer. {where}

            This application keeps accounts, organizations and roles in PostgreSQL, so it cannot
            start without one.

            To get a server running:
                docker run --name testo-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16

            Or install one. Homebrew and apt create a superuser named after your account rather
            than 'postgres', so after installing:
                createuser -s postgres
                psql -d postgres -c "ALTER USER postgres WITH PASSWORD 'postgres';"

            To point at a different server instead:
                export ConnectionStrings__Testo="Host=...;Database=...;Username=...;Password=..."

            README.md has the longer version, including what the tests need.
            """,
            inner);
    }

    /// <summary>Which server was tried, without the password.</summary>
    private static string Describe(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "No connection string was configured.";
        }

        try
        {
            var parts = new NpgsqlConnectionStringBuilder(connectionString);

            return $"Tried {parts.Host}:{parts.Port}, database '{parts.Database}', as '{parts.Username}'.";
        }
        catch (ArgumentException)
        {
            // A malformed connection string is its own problem, and printing it would print the
            // password with it.
            return "The configured connection string could not be read.";
        }
    }
}
