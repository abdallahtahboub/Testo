using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testo.Contracts.Identity;
using Testo.Persistence;
using Testo.Persistence.Interceptors;

namespace Testo.Application.Tests.Support;

/// <summary>
/// A PostgreSQL database of its own for one test class, created from the migrations and dropped
/// afterwards.
/// </summary>
/// <remarks>
/// <para>
/// These tests run against the real database on purpose. Two of the three layers of tenant
/// isolation are the database's — the indexes and the foreign keys — and an in-memory provider
/// would quietly pass tests that PostgreSQL would fail.
/// </para>
/// <para>
/// Where the server is: <c>TESTO_TEST_DB</c> if it is set, otherwise a local PostgreSQL with the
/// usual development credentials. If neither answers, the failure says so and says how to get one,
/// rather than reporting a hundred unrelated errors.
/// </para>
/// </remarks>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _databaseName = $"testo_test_{Guid.CreateVersion7():N}";
    private readonly string _serverConnectionString;

    private TestDatabase(string serverConnectionString) => _serverConnectionString = serverConnectionString;

    /// <summary>Where this test's database is.</summary>
    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ToString();

    /// <summary>Who the test is currently pretending to be.</summary>
    public TestPrincipal Principal { get; } = new();

    /// <summary>Creates the database and brings it up to the current migration.</summary>
    public static async Task<TestDatabase> CreateAsync()
    {
        var server = Environment.GetEnvironmentVariable("TESTO_TEST_DB")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

        var database = new TestDatabase(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ToString());

        try
        {
            await using var connection = new NpgsqlConnection(database._serverConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database._databaseName}\"", connection);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "These tests need a PostgreSQL server. Start one and point TESTO_TEST_DB at it, for example "
                + "TESTO_TEST_DB=\"Host=localhost;Username=postgres;Password=postgres\". "
                + "On a developer machine: docker run -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16, "
                + "or install PostgreSQL locally. See README.md.",
                exception);
        }

        await using (var migrator = database.NewContext())
        {
            await migrator.Database.MigrateAsync().ConfigureAwait(false);
        }

        return database;
    }

    /// <summary>
    /// A fresh context, which reads the organization from <see cref="Principal"/> as it is now.
    /// </summary>
    /// <remarks>
    /// The two save guards are added here as well as in <see cref="NewServices"/>. Without them a
    /// test would be exercising a context the application never uses, and would pass while the
    /// product refused the same operation — or worse, the other way round.
    /// </remarks>
    /// <summary>
    /// The permission to write outside the current organization, for contexts built by
    /// <see cref="NewContext"/>. The container in <see cref="NewServices"/> has its own, scoped
    /// one per request, as the application does.
    /// </summary>
    public TenantWriteScope WriteScope { get; } = new();

    public TestoDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TestoDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(
                new TenantGuardInterceptor(Principal, WriteScope),
                new StampInterceptor(TimeProvider.System, Principal))
            .Options;

        return new TestoDbContext(options, Principal);
    }

    /// <summary>A container with the application registered against this database.</summary>
    /// <param name="configure">
    /// Runs before the application is registered, so that a test replacement wins over the
    /// defaults the application adds with <c>TryAdd</c>.
    /// </param>
    public ServiceProvider NewServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ICurrentUser>(Principal);
        services.AddSingleton<IOrganizationContext>(Principal);

        configure?.Invoke(services);

        services.AddTestoApplication(ConnectionString);

        return services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            connection);

        await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
