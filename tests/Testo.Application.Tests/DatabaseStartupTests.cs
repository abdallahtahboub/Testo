using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;

namespace Testo.Application.Tests;

/// <summary>
/// A database that is not there is the first thing a new developer meets, and twice now it has
/// met them as a stack trace. These tests pin the sentence that replaced it.
/// </summary>
public class DatabaseStartupTests
{
    /// <summary>A port nothing listens on, so the connection is refused rather than left hanging.</summary>
    private const string Unreachable =
        "Host=127.0.0.1;Port=5466;Database=testo_nowhere;Username=nobody;Password=secret-value";

    [Fact]
    public async Task EnsureReachable_WhenTheServerIsNotThere_SaysSoAndSaysWhatToDo()
    {
        await using var services = Services(Unreachable);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.EnsureReachableAsync(services, TestContext.Current.CancellationToken));

        // Which server was tried, so the reader can tell a wrong port from a stopped server.
        Assert.Contains("127.0.0.1:5466", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("testo_nowhere", refusal.Message, StringComparison.Ordinal);
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
            () => DatabaseStartup.EnsureReachableAsync(services, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("secret-value", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", refusal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureReachable_WhenTheDatabaseIsThere_SaysNothingAndReturns()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var services = database.NewServices();

        // No exception: the whole assertion.
        await DatabaseStartup.EnsureReachableAsync(services, TestContext.Current.CancellationToken);
    }

    private static ServiceProvider Services(string connectionString)
    {
        var services = new ServiceCollection();
        var principal = new TestPrincipal();

        services.AddLogging();
        services.AddSingleton<ICurrentUser>(principal);
        services.AddSingleton<IOrganizationContext>(principal);
        services.AddTestoApplication(connectionString);

        return services.BuildServiceProvider();
    }
}
