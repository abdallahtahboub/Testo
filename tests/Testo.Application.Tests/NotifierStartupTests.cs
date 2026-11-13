using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Identity;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;

namespace Testo.Application.Tests;

/// <summary>
/// A deployed application that would write invitation tokens to its log does not start (D-3).
/// </summary>
public class NotifierStartupTests
{
    private const string AnyDatabase = "Host=127.0.0.1;Port=5432;Database=unused;Username=u;Password=p";

    [Fact]
    public void Deployed_WithNoMailProvider_RefusesToStart()
    {
        using var services = Build(notifier: null);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => NotifierStartup.EnsureLinksReachPeople(services, isDevelopment: false));

        Assert.Contains("written to the log", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("anyone who can read the log", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployed_WithAMailProvider_Starts()
    {
        using var services = Build(notifier: new RecordingNotifier());

        NotifierStartup.EnsureLinksReachPeople(services, isDevelopment: false);
    }

    [Fact]
    public void OnADevelopersMachine_WithNoMailProvider_Starts()
    {
        // The whole point of the logging notifier: a developer can invite somebody and follow the
        // link without running a mail server. It warns on every use.
        using var services = Build(notifier: null);

        NotifierStartup.EnsureLinksReachPeople(services, isDevelopment: true);
    }

    private static ServiceProvider Build(IInvitationNotifier? notifier)
    {
        var services = new ServiceCollection();
        var principal = new TestPrincipal();

        services.AddLogging();
        services.AddSingleton<ICurrentUser>(principal);
        services.AddSingleton<IOrganizationContext>(principal);

        if (notifier is not null)
        {
            services.AddSingleton(notifier);
        }

        // No connection is opened: this check only looks at what is registered.
        services.AddTestoApplication(AnyDatabase);

        return services.BuildServiceProvider();
    }
}
