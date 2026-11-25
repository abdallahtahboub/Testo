using Npgsql;
using Autestiq.Application.Execution;
using Autestiq.Application.Operations;
using Autestiq.Application.Projects;
using Autestiq.Application.Tests.Support;
using Autestiq.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Autestiq.Application.Tests.Operations;

/// <summary>
/// A host that can be deployed (Milestone 5.1 R2): the schema is migrated by an operator, forward and back, and the
/// readiness checks say no for each thing the application cannot serve without.
/// </summary>
public sealed class OperationsTests : IAsyncLifetime
{
    private TestDatabase _empty = null!;

    public async ValueTask InitializeAsync() => _empty = await TestDatabase.CreateAsync(migrate: false);

    public async ValueTask DisposeAsync() => await _empty.DisposeAsync();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_AnEmptyDatabase_AppliesEveryMigration_RollsBackToEmpty_AndForwardAgain()
    {
        await using var database = _empty.NewContext();
        var known = DatabaseMigrations.Known(database);

        Assert.Equal(known, (await DatabaseMigrations.StateAsync(database, Cancellation)).Pending);

        await DatabaseMigrations.MigrateAsync(database, null, Cancellation);
        var (applied, pending) = await DatabaseMigrations.StateAsync(database, Cancellation);
        Assert.Equal(known, applied);
        Assert.Empty(pending);

        // One step back: the latest migration's tables are gone, the earlier ones are there.
        await DatabaseMigrations.MigrateAsync(database, known[^2], Cancellation);
        Assert.Equal([known[^1]], (await DatabaseMigrations.StateAsync(database, Cancellation)).Pending);

        await DatabaseMigrations.MigrateAsync(database, DatabaseMigrations.Empty, Cancellation);
        Assert.Empty((await DatabaseMigrations.StateAsync(database, Cancellation)).Applied);

        await DatabaseMigrations.MigrateAsync(database, null, Cancellation);
        Assert.Empty((await DatabaseMigrations.StateAsync(database, Cancellation)).Pending);
    }

    [Fact]
    public async Task Migrate_ToAMigrationThatDoesNotExist_IsRefused_AndChangesNothing()
    {
        await using var database = _empty.NewContext();

        await Assert.ThrowsAsync<ArgumentException>(() => DatabaseMigrations.MigrateAsync(database, "20990101000000_Nothing", Cancellation));
        Assert.Empty((await DatabaseMigrations.StateAsync(database, Cancellation)).Applied);
    }

    [Fact]
    public async Task Ready_Database_WhenEveryMigrationIsApplied()
    {
        await using var migrated = await TestDatabase.CreateAsync();
        await using var database = migrated.NewContext();

        Assert.True((await Readiness.DatabaseAsync(database, Cancellation)).Ready);
    }

    [Fact]
    public async Task NotReady_Database_WhenMigrationsArePending_SayingToMigrate()
    {
        await using var database = _empty.NewContext();

        var check = await Readiness.DatabaseAsync(database, Cancellation);

        Assert.False(check.Ready);
        Assert.Contains("migrate", check.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotReady_Database_WhenNoServerAnswers()
    {
        var nowhere = new NpgsqlConnectionStringBuilder(_empty.ConnectionString) { Host = "127.0.0.1", Port = 1, Timeout = 2 }.ToString();
        await using var database = new AutestiqDbContext(new DbContextOptionsBuilder<AutestiqDbContext>().UseNpgsql(nowhere).Options, _empty.Principal);

        Assert.False((await Readiness.DatabaseAsync(database, Cancellation)).Ready);
    }

    [Fact]
    public async Task Ready_KeyRing_OnAFreshDatabase_WritesTheCanary_AndReadsItBack()
    {
        await using var migrated = await TestDatabase.CreateAsync();
        var ring = new Ring("first");

        await using (var database = migrated.NewContext())
        {
            Assert.True((await Readiness.KeyRingAsync(database, ring, Cancellation)).Ready);
        }

        await using (var database = migrated.NewContext())
        {
            Assert.Equal(1, await database.KeyRingCanaries.CountAsync(Cancellation));
            Assert.True((await Readiness.KeyRingAsync(database, ring, Cancellation)).Ready);
        }
    }

    [Fact]
    public async Task NotReady_KeyRing_ThatDidNotWriteTheDatabase_AsAfterARestoreWithoutIt()
    {
        await using var migrated = await TestDatabase.CreateAsync();

        await using (var database = migrated.NewContext())
        {
            Assert.True((await Readiness.KeyRingAsync(database, new Ring("original"), Cancellation)).Ready);
        }

        await using (var database = migrated.NewContext())
        {
            var check = await Readiness.KeyRingAsync(database, new Ring("a new one"), Cancellation);

            Assert.False(check.Ready);
            Assert.Contains("not the key ring", check.Reason!, StringComparison.Ordinal);
            Assert.Equal(1, await database.KeyRingCanaries.CountAsync(Cancellation)); // never overwritten by the wrong ring
        }
    }

    [Fact]
    public async Task Ready_KeyRing_AfterAResetAcceptsTheLoss_WritesANewCanary()
    {
        await using var migrated = await TestDatabase.CreateAsync();

        await using (var database = migrated.NewContext())
        {
            await Readiness.KeyRingAsync(database, new Ring("lost"), Cancellation);

            // What the operator's key-ring-reset does.
            await database.KeyRingCanaries.ExecuteDeleteAsync(Cancellation);
        }

        await using (var database = migrated.NewContext())
        {
            Assert.True((await Readiness.KeyRingAsync(database, new Ring("replacement"), Cancellation)).Ready);
        }
    }

    [Fact]
    public async Task Ready_KeyRing_WhenTwoInstancesCheckAtOnce_KeepsOneCanary()
    {
        await using var migrated = await TestDatabase.CreateAsync();
        var ring = new Ring("shared");

        async Task<bool> CheckAsync()
        {
            await using var database = migrated.NewContext();
            return (await Readiness.KeyRingAsync(database, ring, Cancellation)).Ready;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CheckAsync()));

        Assert.All(results, Assert.True);
        await using var check = migrated.NewContext();
        Assert.Equal(1, await check.KeyRingCanaries.CountAsync(Cancellation));
    }

    [Fact]
    public async Task NotReady_KeyRing_WithoutAProtector()
    {
        // What runs when no host provides one: nothing can be stored.
        await using var migrated = await TestDatabase.CreateAsync();
        await using var database = migrated.NewContext();

        Assert.False((await Readiness.KeyRingAsync(database, new Refusing(), Cancellation)).Ready);
    }

    [Fact]
    public async Task Ready_WorkerImage_InProcessMode_WithoutAskingDocker()
    {
        var options = new ExecutionOptions { Isolation = WorkerIsolation.Process, Docker = "/nonexistent/docker" };

        Assert.True((await Readiness.WorkerImageAsync(options, Cancellation)).Ready);
    }

    [Fact]
    public async Task NotReady_WorkerImage_InContainerMode_WhenDockerCannotStart()
    {
        var options = new ExecutionOptions { Isolation = WorkerIsolation.Container, Docker = "/nonexistent/docker" };

        Assert.False((await Readiness.WorkerImageAsync(options, Cancellation)).Ready);
    }

    [Fact]
    public async Task NotReady_WorkerImage_InContainerMode_WhenTheImageIsMissing()
    {
        var options = new ExecutionOptions { Isolation = WorkerIsolation.Container, WorkerImage = "autestiq-worker:does-not-exist" };

        var check = await Readiness.WorkerImageAsync(options, Cancellation);

        Assert.False(check.Ready);
        Assert.Contains("does-not-exist", check.Reason!, StringComparison.Ordinal);
    }

    /// <summary>A key ring that reads only what it wrote itself, as Data Protection with its own keys does.</summary>
    private sealed class Ring(string name) : ISecretProtector
    {
        public string Protect(string purpose, string value) => $"{name}|{purpose}|{value}";

        public string? Unprotect(string purpose, string ciphertext) =>
            ciphertext.StartsWith($"{name}|{purpose}|", StringComparison.Ordinal) ? ciphertext[$"{name}|{purpose}|".Length..] : null;
    }

    private sealed class Refusing : ISecretProtector
    {
        public string Protect(string purpose, string value) => throw new InvalidOperationException("No secret protector is configured.");

        public string? Unprotect(string purpose, string ciphertext) => null;
    }
}
