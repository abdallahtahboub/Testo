using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Identity;
using Testo.Web.Components;
using Testo.Web.Identity;

namespace Testo.E2E.Support;

/// <summary>
/// The real application, started inside the test process on a real network port.
/// </summary>
/// <remarks>
/// It runs like a deployed site (environment "Production"), so the tests see the friendly
/// error page instead of the developer exception page. The only additions are the static
/// files of the class libraries (a published site has them in its web root) and one URL
/// that fails on purpose.
/// </remarks>
internal class TestoWebApplication(string connectionString) : WebApplicationFactory<App>
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
        builder.UseSetting("ConnectionStrings:Testo", connectionString);

        // There is no certificate on a loopback test port, so a Secure cookie would never be
        // stored and every sign-in test would fail for a reason the product does not have.
        builder.UseSetting(TestoIdentityServiceCollectionExtensions.AllowInsecureCookiesKey, "true");

        // The migration and seeding statements are tens of lines of SQL each and bury the
        // failure a test is actually reporting.
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Warning");

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
