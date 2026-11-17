using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Autestiq.Application;
using Autestiq.Application.Execution;
using Autestiq.Application.Identity;
using Autestiq.Mocks;
using Autestiq.Modules.Analysis;
using Autestiq.Modules.Defects;
using Autestiq.Modules.Design;
using Autestiq.Modules.Execution;
using Autestiq.Modules.Planning;
using Autestiq.Modules.Reporting;
using Autestiq.Modules.Settings;
using Autestiq.Platform;
using Autestiq.Platform.Features;
using Autestiq.Platform.Localization;
using Autestiq.Platform.Modules;
using Autestiq.Platform.Navigation;
using Autestiq.UI.Shared;
using Autestiq.UI.Shared.Branding;
using Autestiq.UI.Shared.Identity;
using Autestiq.UI.Shared.Theming;
using Autestiq.Web.Antiforgery;
using Autestiq.Web.Components;
using Autestiq.Web.Execution;
using Autestiq.Web.Seo;
using Autestiq.Website;
using Autestiq.Web.Features;
using Autestiq.Web.Identity;
using Autestiq.Web.Localization;
using Autestiq.Web.Preferences;
using Autestiq.Web.Routing;
using Autestiq.Web.Security;
using Autestiq.Web.StaticAssets;

// The host is the composition root: it wires the pieces together and contains no feature code.

var builder = WebApplication.CreateBuilder(args);

// Blazor. Public pages are rendered statically; app pages run interactively on the server.
// The theme choice travels from the server-rendered page into the interactive session.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .RegisterPersistentService<ThemeState>(RenderMode.InteractiveServer)
    .RegisterPersistentService<SignedInUser>(RenderMode.InteractiveServer);

// Public pages without forms stay free of the antiforgery cookie.
builder.Services.AddPublicPageAntiforgery();

// Design system, brand, languages, feature flags.
builder.Services.AddAutestiqDesignSystem();

// The public site's menu and footer. Registered rather than written into the chrome, so the
// design system renders words it does not own and the website keeps its copy (CLAUDE.md).
builder.Services.AddSingleton<ISiteMenu, SiteMenuCatalogue>();
builder.Services.Configure<BrandOptions>(builder.Configuration.GetSection(BrandOptions.SectionName));
builder.Services.AddAutestiqLocalization();
builder.Services.AddFeatureManagement();
builder.Services.AddScoped<IFeatureFlags, FeatureManagementFlags>();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<CspNonce>();

// How often one address may try to sign in (D-4).
builder.Services.AddAccountRateLimiting(builder.Configuration);

// Signing in. The session cookie, and the services that answer "who is asking".
builder.Services.AddAutestiqIdentity(
    allowInsecureCookie: builder.Configuration.GetValue<bool>(AutestiqIdentityServiceCollectionExtensions.AllowInsecureCookiesKey));

// Data, in two parts while the product is half built (ADR-0014).
//
// The mock services still answer everything about the testing journey — plans, cases, runs,
// defects — because their real implementations arrive with Milestone 5. They are registered
// first, so that anything the application layer does implement replaces them.
builder.Services.AddAutestiqMocks(options => builder.Configuration.GetSection(MockOptions.SectionName).Bind(options));

// Identity and access are real, and need a database. Starting without one would give a product
// nobody can sign in to, so this fails here rather than at the sign-in screen.
var connectionString = builder.Configuration.GetConnectionString("Autestiq")
    ?? throw new InvalidOperationException(
        "No database is configured. Set ConnectionStrings:Autestiq, for example "
        + "\"Host=localhost;Database=autestiq_dev;Username=postgres;Password=postgres\". "
        + "See README.md for how to get a PostgreSQL to point it at.");

builder.Services.AddAutestiqApplication(connectionString);
builder.Services.Configure<ExecutionOptions>(builder.Configuration.GetSection(ExecutionOptions.SectionName));
builder.Services.Configure<Autestiq.Application.Usage.UsageOptions>(builder.Configuration.GetSection(Autestiq.Application.Usage.UsageOptions.SectionName));
builder.Services.Configure<Autestiq.Application.Testing.PlanningOptions>(builder.Configuration.GetSection(Autestiq.Application.Testing.PlanningOptions.Section));
builder.Services.Configure<Autestiq.Application.Execution.EvidenceRetentionOptions>(builder.Configuration.GetSection(Autestiq.Application.Execution.EvidenceRetentionOptions.Section));
builder.Services.Configure<ArtifactStoreOptions>(builder.Configuration.GetSection(ArtifactStoreOptions.SectionName));

// Runs are carried out by a worker process, outside this one (ADR-0018).
builder.Services.AddAutestiqRunDispatcher();

// The product's AI planner (ADR-0030): the key from server configuration only, prices from the price book. Without a
// key there is no planner, and planning says so rather than calling anything.
builder.Services.AddHttpClient(nameof(Autestiq.Web.Planning.AnthropicPlannerFactory));
builder.Services.Configure<Autestiq.Web.Planning.PlannerKeyOptions>(builder.Configuration.GetSection(Autestiq.Web.Planning.PlannerKeyOptions.Section));
builder.Services.AddSingleton<Autestiq.Application.Testing.IPlannerFactory, Autestiq.Web.Planning.AnthropicPlannerFactory>();

// Plans written out as tests by fixed templates, never by a model (ADR-0023).
builder.Services.AddSingleton<Autestiq.Contracts.Export.ITestExporter, Autestiq.Export.PythonExporter>();

// Feature modules: one line each. A module brings its own pages, services, navigation and widgets.
builder.Services.AddAutestiqModule<PlanningModule>();
builder.Services.AddAutestiqModule<AnalysisModule>();
builder.Services.AddAutestiqModule<DesignModule>();
builder.Services.AddAutestiqModule<ExecutionModule>();
builder.Services.AddAutestiqModule<DefectsModule>();
builder.Services.AddAutestiqModule<ReportingModule>();
builder.Services.AddAutestiqModule<SettingsModule>();
builder.Services.AddSingleton<RoutableAssemblies>();

var app = builder.Build();

// Before anything touches the database, so that a server which is not running, a database which
// does not exist or a refused password is answered with a sentence rather than with Entity
// Framework's connection error. In development the migrations below create a missing database.
await DatabaseStartup.EnsureReachableAsync(app.Services, allowMissingDatabase: app.Environment.IsDevelopment());

// A deployed application that writes invitation tokens to its log should not start (D-3).
NotifierStartup.EnsureLinksReachPeople(app.Services, app.Environment.IsDevelopment());

// A developer who cannot sign in reaches for a shortcut that is worse than a seeded database.
if (app.Environment.IsDevelopment())
{
    await DevelopmentData.EnsureSeededAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(Routes.Error, createScopeForErrors: true);
    app.UseHsts();
}

// A friendly page for failed page requests (GET and HEAD). A rejected form post keeps its
// plain status code instead of being re-run as a post to the not-found page.
app.UseStatusCodePagesWithReExecute(Routes.NotFound, createScopeForStatusCodePages: true);
app.Use((context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
    {
        statusCodePages.Enabled = false;
    }

    return next(context);
});
app.UseHttpsRedirection();
app.UseSecurityHeaders(app.Environment);
app.UseRequestLocalization();

// Before authentication, so a refused attempt costs a dictionary lookup rather than a password
// hash and a database read.
app.UseAccountRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Who is signed in, for every request: endpoints that are not pages need it as much as pages do.
app.UseSessionFromClaims();
app.UseImmutableFontCaching();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPreferenceEndpoints();
app.MapArtifactEndpoints();
app.MapTestDownloadEndpoints();

// sitemap.xml and robots.txt, generated from the page catalogue so they cannot go stale.
app.MapSiteMap();

// The bare address leads to the home page in the visitor's language.
app.MapGet("/", (HttpContext context) =>
{
    context.Response.Headers.Vary = "Accept-Language, Cookie";
    return Results.Redirect(LocalizedPath.For(SupportedCultures.Current));
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. app.Services.GetRequiredService<RoutableAssemblies>().Additional]);

app.Run();
