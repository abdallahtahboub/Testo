using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Autestiq.Persistence;

namespace Autestiq.Application;

/// <summary>
/// Checks that the database is actually reachable before the application tries to use it.
/// </summary>
/// <remarks>
/// <para>
/// Without this, a server that is not running surfaces as Entity Framework's own connection
/// error: several frames of stack trace that say a connection failed, but not which server was
/// tried, nor what to do about it. The product cannot run without a database, so the one person
/// who needs to know that deserves a sentence rather than a log event number.
/// </para>
/// <para>
/// It tells three failures apart, because each has a different remedy: no server answered; the
/// server answered but the database does not exist; the server refused the credentials. Entity
/// Framework's <c>CanConnectAsync</c> answers <see langword="false"/> for the first two alike and
/// says nothing of why, so this opens a connection itself and reads PostgreSQL's error code.
/// </para>
/// </remarks>
public static class DatabaseStartup
{
    /// <summary>PostgreSQL's code for a database that does not exist (<c>invalid_catalog_name</c>).</summary>
    private const string DatabaseDoesNotExist = "3D000";

    /// <summary>PostgreSQL's code for a wrong password (<c>invalid_password</c>).</summary>
    private const string InvalidPassword = "28P01";

    /// <summary>
    /// PostgreSQL's code for a refused login that is not a wrong password, such as no
    /// <c>pg_hba.conf</c> entry for this user (<c>invalid_authorization_specification</c>).
    /// </summary>
    private const string InvalidAuthorization = "28000";

    /// <summary>
    /// Throws with an explanation if the database cannot be reached.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="allowMissingDatabase">
    /// <see langword="true"/> where the migrations run at start-up and will create the database
    /// themselves — in development, ahead of <see cref="DevelopmentData"/>. A server that answers
    /// without the database is then good enough. Everywhere else a missing database is refused,
    /// because creating one is a deployment's decision, not the application's.
    /// </param>
    /// <param name="cancellationToken">Stops the check if the host is shutting down.</param>
    /// <exception cref="InvalidOperationException">
    /// The database could not be used; the message says which of the three reasons it was.
    /// </exception>
    public static async Task EnsureReachableAsync(
        IServiceProvider services,
        bool allowMissingDatabase = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        string? connectionString;

        await using (var scope = services.CreateAsyncScope())
        {
            connectionString = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>()
                .Database.GetConnectionString();
        }

        try
        {
            // Not pooled: a failed attempt must not leave a broken connection for the
            // application, nor a pool that keeps the database open while the migrations run.
            var unpooled = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };

            await using var connection = new NpgsqlConnection(unpooled.ToString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException refusal) when (refusal.SqlState == DatabaseDoesNotExist)
        {
            if (allowMissingDatabase)
            {
                return;
            }

            throw Missing(connectionString, refusal);
        }
        catch (PostgresException refusal) when (refusal.SqlState is InvalidPassword or InvalidAuthorization)
        {
            throw CredentialsRefused(connectionString, refusal);
        }
        catch (Exception reason) when (reason is NpgsqlException or InvalidOperationException or ArgumentException)
        {
            throw Unreachable(connectionString, reason);
        }
    }

    /// <summary>
    /// The message somebody reads when the server is not there. It names the server that was
    /// tried and never the password, because this goes into logs.
    /// </summary>
    private static InvalidOperationException Unreachable(string? connectionString, Exception? inner) => new(
        $"""
        The database server did not answer. {Describe(connectionString)}

        This application keeps accounts, organizations and roles in PostgreSQL, so it cannot
        start without one.

        To get a server running:
            docker run --name autestiq-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16

        Or install one. Homebrew and apt create a superuser named after your account rather
        than 'postgres', so after installing:
            createuser -s postgres
            psql -d postgres -c "ALTER USER postgres WITH PASSWORD 'postgres';"

        To point at a different server instead:
            export ConnectionStrings__Autestiq="Host=...;Database=...;Username=...;Password=..."

        README.md has the longer version, including what the tests need.
        """,
        inner);

    /// <summary>The message for a server that answered without the database.</summary>
    private static InvalidOperationException Missing(string? connectionString, Exception inner) => new(
        $"""
        The database server answered, but the database '{DatabaseName(connectionString)}' does not exist on it. {Describe(connectionString)}

        In development the application creates it on the first run. Outside development it does
        not create databases: create this one, for example with
            createdb {DatabaseName(connectionString)}
        or point the connection string at one that exists:
            export ConnectionStrings__Autestiq="Host=...;Database=...;Username=...;Password=..."

        README.md has the longer version.
        """,
        inner);

    /// <summary>
    /// The message for a server that refused the login. The usual cause on a developer machine is
    /// a second PostgreSQL holding the port, with a password of its own.
    /// </summary>
    private static InvalidOperationException CredentialsRefused(string? connectionString, Exception inner) => new(
        $"""
        The database server refused the credentials. {Describe(connectionString)}

        Either the user or its password is wrong for this server, or this is not the server you
        meant: another PostgreSQL may already be listening on that port, with a password of its
        own. Check which one answers there, then correct the password or the port:
            export ConnectionStrings__Autestiq="Host=...;Port=...;Database=...;Username=...;Password=..."

        README.md has the longer version.
        """,
        inner);

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

    /// <summary>The database's name alone, for the commands in a message.</summary>
    private static string DatabaseName(string? connectionString)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
        }
        catch (ArgumentException)
        {
            return "";
        }
    }
}
