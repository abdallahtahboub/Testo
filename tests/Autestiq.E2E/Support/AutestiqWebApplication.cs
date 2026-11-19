using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Identity;
using Autestiq.Web.Components;
using Autestiq.Web.Identity;
using Autestiq.Web.Security;

namespace Autestiq.E2E.Support;

/// <summary>
/// The real application, started inside the test process on a real network port.
/// </summary>
/// <remarks>
/// It runs like a deployed site (environment "Production"), so the tests see the friendly
/// error page instead of the developer exception page. The only additions are the static
/// files of the class libraries (a published site has them in its web root) and one URL
/// that fails on purpose.
/// </remarks>
internal class AutestiqWebApplication(string connectionString) : WebApplicationFactory<App>
{
    /// <summary>A URL that makes the server throw, so the error page can be tested.</summary>
    public const string FailingPath = "/__e2e/fail";

    /// <summary>The address this application listens on, once it has been started.</summary>
    public string ServerAddress()
    {
        var addresses = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server does not report its addresses.");
        return addresses.Addresses.First().TrimEnd('/') + "/";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseStaticWebAssets();

        // The real application needs a real database, and these tests give it one of its own.
        builder.UseSetting("ConnectionStrings:Autestiq", connectionString);

        // There is no certificate on a loopback test port, so a Secure cookie would never be
        // stored and every sign-in test would fail for a reason the product does not have.
        builder.UseSetting(AutestiqIdentityServiceCollectionExtensions.AllowInsecureCookiesKey, "true");

        // The migration and seeding statements are tens of lines of SQL each and bury the
        // failure a test is actually reporting.
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Warning");

        // Every test shares this application and they all post to the account pages, from one
        // address, in parallel. Left at the real limit they would throttle each other. The limit
        // itself is tested against an application of its own, with a strict one (D-4).
        builder.UseSetting(AccountRateLimiting.PermitsPerMinuteKey, "100000");

        // A deployed site refuses to start without a key ring for sign-in values (ADR-0033), so this one gets its own.
        builder.UseSetting("DataProtection:KeyDirectory", Directory.CreateTempSubdirectory("autestiq-e2e-keys-").FullName);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, FailingPathFilter>();

            // This application runs as a deployed site, and a deployed site refuses to start if
            // invitation links would go to its log (D-3). So it gets a sender.
            services.AddScoped<IInvitationNotifier, SilentNotifier>();
        });
    }

    /// <summary>Adds a last step to the request pipeline that throws for <see cref="FailingPath"/>.</summary>
    private sealed class FailingPathFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, nextStep) => context.Request.Path == FailingPath
                ? throw new InvalidOperationException("Failure requested by an E2E test.")
                : nextStep(context));
        };
    }
}
