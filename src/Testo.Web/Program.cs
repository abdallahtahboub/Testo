using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Testo.Application;
using Testo.Application.Identity;
using Testo.Mocks;
using Testo.Modules.Analysis;
using Testo.Modules.Defects;
using Testo.Modules.Design;
using Testo.Modules.Execution;
using Testo.Modules.Planning;
using Testo.Modules.Reporting;
using Testo.Modules.Settings;
using Testo.Platform;
using Testo.Platform.Features;
using Testo.Platform.Localization;
using Testo.Platform.Modules;
using Testo.UI.Shared;
using Testo.UI.Shared.Branding;
using Testo.UI.Shared.Identity;
using Testo.UI.Shared.Theming;
using Testo.Web.Antiforgery;
using Testo.Web.Components;
using Testo.Web.Features;
using Testo.Web.Identity;
using Testo.Web.Localization;
using Testo.Web.Preferences;
using Testo.Web.Routing;
using Testo.Web.Security;
using Testo.Web.StaticAssets;

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
builder.Services.AddTestoDesignSystem();
builder.Services.Configure<BrandOptions>(builder.Configuration.GetSection(BrandOptions.SectionName));
builder.Services.AddTestoLocalization();
builder.Services.AddFeatureManagement();
builder.Services.AddScoped<IFeatureFlags, FeatureManagementFlags>();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<CspNonce>();

// Signing in. The session cookie, and the services that answer "who is asking".
builder.Services.AddTestoIdentity(
    allowInsecureCookie: builder.Configuration.GetValue<bool>(TestoIdentityServiceCollectionExtensions.AllowInsecureCookiesKey));

// Data, in two parts while the product is half built (ADR-0014).
//
// The mock services still answer everything about the testing journey — plans, cases, runs,
// defects — because their real implementations arrive with Milestone 5. They are registered
// first, so that anything the application layer does implement replaces them.
builder.Services.AddTestoMocks(options => builder.Configuration.GetSection(MockOptions.SectionName).Bind(options));

// Identity and access are real, and need a database. Starting without one would give a product
// nobody can sign in to, so this fails here rather than at the sign-in screen.
var connectionString = builder.Configuration.GetConnectionString("Testo")
    ?? throw new InvalidOperationException(
        "No database is configured. Set ConnectionStrings:Testo, for example "
        + "\"Host=localhost;Database=testo_dev;Username=postgres;Password=postgres\". "
        + "See README.md for how to get a PostgreSQL to point it at.");

builder.Services.AddTestoApplication(connectionString);

// Feature modules: one line each. A module brings its own pages, services, navigation and widgets.
builder.Services.AddTestoModule<PlanningModule>();
builder.Services.AddTestoModule<AnalysisModule>();
builder.Services.AddTestoModule<DesignModule>();
builder.Services.AddTestoModule<ExecutionModule>();
builder.Services.AddTestoModule<DefectsModule>();
builder.Services.AddTestoModule<ReportingModule>();
builder.Services.AddTestoModule<SettingsModule>();
builder.Services.AddSingleton<RoutableAssemblies>();

var app = builder.Build();

// Before anything touches the database, so that a server which is not running is answered with
// a sentence rather than with Entity Framework's connection error.
await DatabaseStartup.EnsureReachableAsync(app.Services);

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
app.UseAuthentication();
app.UseAuthorization();
app.UseImmutableFontCaching();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPreferenceEndpoints();

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
