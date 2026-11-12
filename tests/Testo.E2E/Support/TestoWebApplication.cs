using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testo.Web.Components;

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
internal sealed class TestoWebApplication : WebApplicationFactory<App>
{
    /// <summary>A URL that makes the server throw, so the error page can be tested.</summary>
    public const string FailingPath = "/__e2e/fail";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseStaticWebAssets();
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, FailingPathFilter>());
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
