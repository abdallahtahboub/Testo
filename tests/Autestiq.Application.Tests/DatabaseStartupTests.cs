using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application.Tests;

/// <summary>
/// A database that is not there is the first thing a new developer meets, and twice now it has
/// met them as a stack trace. These tests pin the sentence that replaced it.
/// </summary>
public class DatabaseStartupTests
{
    /// <summary>A port nothing listens on, so the connection is refused rather than left hanging.</summary>
    private const string Unreachable =
        "Host=127.0.0.1;Port=5466;Database=autestiq_nowhere;Username=nobody;Password=secret-value";

    [Fact]
    public async Task EnsureReachable_WhenTheServerIsNotThere_SaysSoAndSaysWhatToDo()
    {
        await using var services = Services(Unreachable);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.EnsureReachableAsync(services, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("did not answer", refusal.Message, StringComparison.Ordinal);

        // Which server was tried, so the reader can tell a wrong port from a stopped server.
        Assert.Contains("127.0.0.1:5466", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("autestiq_nowhere", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("nobody", refusal.Message, StringComparison.Ordinal);

        // And what to do about it.
        Assert.Contains("docker run", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("README.md", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureReachable_WhenTheServerIsNotThere_NeverPrintsThePassword()
    {
        // This message goes into logs, which are read by more people than the connection string is.
        await using var services = Services(Unreachable);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.EnsureReachableAsync(services, cancellationToken: TestContext.Current.CancellationToken));

        Assert.DoesNotContain("secret-value", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", refusal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureReachable_WhenTheDatabaseIsThere_SaysNothingAndReturns()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var services = database.NewServices();

        // No exception: the whole assertion.
        await DatabaseStartup.EnsureReachableAsync(services, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureReachable_WhenTheDatabaseDoesNotExist_NamesItAsMissing()
    {
        var missing = MissingDatabaseName();
        await using var services = Services(OnTheTestServer(database: missing));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.EnsureReachableAsync(services, cancellationToken: TestContext.Current.CancellationToken));

        // The server is there, so telling the reader to start one would send them the wrong way.
        Assert.Contains($"'{missing}' does not exist", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("did not answer", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("docker run", refusal.Message, StringComparison.Ordinal);
        Assert.IsType<PostgresException>(refusal.InnerException);
    }

    [Fact]
    public async Task EnsureReachable_WhenTheCredentialsAreWrong_SaysTheyWereRefused()
    {
        // As when a second PostgreSQL, with a password of its own, already holds the port.
        await using var services = Services(OnTheTestServer(database: "postgres", password: "not-the-password"));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.EnsureReachableAsync(services, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("refused the credentials", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("another PostgreSQL", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("did not answer", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-the-password", refusal.ToString(), StringComparison.Ordinal);
        var cause = Assert.IsType<PostgresException>(refusal.InnerException);
        Assert.True(cause.SqlState is "28P01" or "28000", $"Expected a refused login, got {cause.SqlState}.");
    }

    [Fact]
    public async Task EnsureReachable_InDevelopment_LetsTheMigrationsCreateAMissingDatabase()
    {
        // The first run against a fresh server: the database named in appsettings is not there yet.
        var missing = MissingDatabaseName();
        await using var services = Services(OnTheTestServer(database: missing));

        try
        {
            await DatabaseStartup.EnsureReachableAsync(
                services, allowMissingDatabase: true, TestContext.Current.CancellationToken);
            await DevelopmentData.EnsureSeededAsync(services, TestContext.Current.CancellationToken);

            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();

            Assert.Empty(await database.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
            Assert.True(await database.Organizations.AnyAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();

            await using var connection = new NpgsqlConnection(OnTheTestServer(database: "postgres"));
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{missing}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static string MissingDatabaseName() => $"autestiq_missing_{Guid.CreateVersion7():N}";

    private static string OnTheTestServer(string database, string? password = null)
    {
        var parts = new NpgsqlConnectionStringBuilder(TestDatabase.Server) { Database = database };

        if (password is not null)
        {
            parts.Password = password;
        }

        return parts.ToString();
    }

    private static ServiceProvider Services(string connectionString)
    {
        var services = new ServiceCollection();
        var principal = new TestPrincipal();

        services.AddLogging();
        services.AddSingleton<ICurrentUser>(principal);
        services.AddSingleton<IOrganizationContext>(principal);
        services.AddAutestiqApplication(connectionString);

        return services.BuildServiceProvider();
    }
}
