# Architecture (Frontend Phase)

> Status: **Accepted** (Step 0 approved on 2026-11-11). Decisions marked *(ADR-xxxx)* are recorded in
> `docs/decisions/`.

## 1. Goals

1. A polished, enterprise-grade frontend built **now**, against mock data.
2. Swap mocks for the real backend later by **changing DI registration only**.
3. Add new testing-journey modules and new test capabilities **without touching existing modules**.
4. Fast, SEO-friendly public website; rich interactivity only in the product app.

## 2. Tech stack

| Concern | Choice |
|---|---|
| Runtime / framework | .NET 10, Blazor Web App |
| Language | C# (latest), `Nullable` enabled, `TreatWarningsAsErrors` |
| Complex app components | Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`), restyled with our tokens |
| Styling | Global design tokens (CSS custom properties) + CSS isolation per component |
| Localization | `IStringLocalizer<T>` + `.resx`, English (`en`) and German (`de`) |
| Feature flags | `Microsoft.FeatureManagement.AspNetCore` |
| Component tests | xUnit v3 + bUnit 2 |
| E2E tests | Playwright for .NET; the real application runs inside the test process *(ADR-0010)* |
| Accessibility checks | `Deque.AxeCore.Playwright` inside the E2E project |
| Test runner | Microsoft.Testing.Platform, through `dotnet test` *(ADR-0010)* |
| CI | GitHub Actions: restore → build → all tests, including Playwright |
| Central package versions | `Directory.Packages.props`; shared settings in `Directory.Build.props` |
| Solution file | `Testo.slnx` (the XML solution format, the default of the .NET 10 SDK) |

## 3. Solution structure

The suggested structure is kept, with **three changes** (reasons below):

```
Testo.slnx
Directory.Build.props            // nullable, warnings-as-errors, analyzers
Directory.Packages.props         // central package management
src/
  Testo.Web                      // Blazor Web App host: Program.cs, DI composition, routing, App.razor
  Testo.Website                  // NEW: public marketing pages (Razor class library, static SSR)
  Testo.UI.Shared                // design system: tokens, layouts, components, icons, theme
  Testo.Platform                 // NEW: module + capability contracts, registries (UI-side abstractions)
  Testo.Contracts                // DTOs + data service interfaces (no UI deps; backend reuses it)
  Testo.Modules.Planning
  Testo.Modules.Analysis
  Testo.Modules.Design
  Testo.Modules.Execution        // PARTLY BUILT (M3.3): run list + one run. Suites,
                                 // environments and regression still to come
  Testo.Modules.Defects
  Testo.Modules.Reporting        // incl. monitoring & control, release readiness, completion
  Testo.Modules.Settings         // NEW: integrations, team, capability settings host
  Testo.Capabilities.E2EWeb      // SHELL: an _Imports.razor and nothing else
  Testo.Mocks                    // SHELL: options and a random source only. No services, no seed data
  Testo.Domain                   // BUILT (M3): entities and rules (ADR-0014)
  Testo.Application              // BUILT (M3): use cases; implements the Contracts interfaces
  Testo.Persistence              // BUILT (M3): EF Core, PostgreSQL, migrations
  Testo.DemoShop                 // BUILT (M3.3): the website the slice runs against. Not the product
  Testo.Worker                   // BUILT (M3.3): drives a browser, out of process, in a container
  Testo.Api                      // PLANNED: REST host for CI integrations, over the same use cases
tests/
  Testo.UI.Tests                 // bUnit: UI.Shared components + module components
  Testo.Mocks.Tests              // xUnit: seed data consistency, mock feed timing
  Testo.E2E                      // Playwright: smoke per page + main flows; raw HTTP tests of the host
```

**Why the changes:**

| Change | Reason |
|---|---|
| `Testo.Platform` split from `Contracts` | `ITestCapability` and the registries reference Blazor types (`RenderFragment`, component `Type`). `Contracts` must stay free of UI dependencies so the future ASP.NET Core backend can reference it without pulling in Blazor. |
| `Testo.Website` split from `Web` | Keeps the host thin (composition only) and keeps all marketing pages in one static-SSR library, separate from the interactive app. Marketing can evolve independently. |
| `Testo.Modules.Settings` | Settings is its own area and hosts capability settings UIs contributed by plug-ins. |
| Monitoring, implementation, completion, maintenance merged into existing modules | Avoids 4 near-empty projects now. Each can be split out later; it's cheap because modules only depend on `Contracts` and `Platform`. |

The app **Dashboard** and the **app shell** (left nav, top bar) live in `Testo.UI.Shared`
(layouts) and `Testo.Web` (dashboard page that renders registered widgets).

## 4. Dependency rules

```
                     Testo.Web (host, composition root)
          ┌────────────┬────────┴─────────┬──────────────┬──────────────┐
          ▼            ▼                  ▼              ▼              ▼
     Testo.Website  Testo.Modules.*  Testo.Capabilities.*  Testo.Mocks   (later: Testo.Client.Http)
          │            │                  │              │
          └────────────┴───────┬──────────┘              │
                               ▼                         │
                  Testo.UI.Shared  ──►  Testo.Platform   │
                               │              │          │
                               └──────┬───────┘          │
                                      ▼                  │
                              Testo.Contracts  ◄─────────┘
                                      ▲
                                      │
     Testo.Web ──► Testo.Application ─┴─► Testo.Domain
                          │
                          └──────────► Testo.Persistence ──► PostgreSQL
```

The host calls the application layer in process: the app pages already render on the server,
so an HTTP hop to our own API would add latency and a failure mode for nothing (ADR-0014).
`Testo.Api`, a later milestone, is a second host over the same use cases.

`Domain` references `Contracts` rather than the other way round, because `Role` and `Permission`
are vocabulary the interface, the application and the future backend all share, and one
definition is better than two that agree by hand. `Contracts` still references nothing at all.

ASP.NET Core Identity is reached only through `Testo.Persistence.Identity.CredentialService`, so
the whole of that dependency is one class and swapping to an external provider changes one
project (ADR-0013).

| Project | May reference | Must NOT reference |
|---|---|---|
| `Contracts` | BCL only | anything UI, any other Testo project |
| `Platform` | `Contracts`, ASP.NET Core Components | modules, mocks, UI.Shared |
| `UI.Shared` | `Platform`, `Contracts`, Fluent UI | modules, capabilities, mocks |
| `Website` | `UI.Shared`, `Platform`, `Contracts` | modules, mocks |
| `Modules.*` | `UI.Shared`, `Platform`, `Contracts` | **other modules**, capabilities, mocks |
| `Capabilities.*` | `UI.Shared`, `Platform`, `Contracts` | modules, other capabilities, mocks |
| `Mocks` | `Contracts` | anything UI |
| `Domain` | `Contracts` | anything UI, EF Core, ASP.NET Core Identity |
| `Persistence` | `Domain`, `Contracts`, EF Core, Identity stores | anything UI, the application layer |
| `Application` | `Domain`, `Persistence`, `Contracts` | anything UI, ASP.NET Core Identity |
| `DemoShop` | nothing of ours | It is a target, not part of the product. The one other project that uses the ASP.NET Core server framework, because it is a website |
| `Worker` | `Contracts`, Playwright | **everything else.** No database, no Identity, no UI, no Application. It is treated as already compromised (ADR-0018) |
| `Api` *(planned)* | `Application`, `Contracts` | anything UI |
| `Web` | everything (composition root only, no business UI) | — |

These rules are **enforced by a test** (`tests/Testo.UI.Tests/Architecture/DependencyRulesTests.cs`).
It reads the project references from the `.csproj` files, so a forbidden reference fails
the build pipeline even before any code uses it. A project without a rule fails too, so
every new project gets a conscious place in this table.

Only `Testo.Web` references the ASP.NET Core **server** framework. All libraries use the
portable component packages and are marked for the `browser` platform, so they cannot
call server-only APIs by accident. That keeps WebAssembly possible later (§9).

Cross-module communication only happens through:
- **Contracts** (shared DTOs + service interfaces), and
- **routes** (a module links to another module's page by URL constants published in
  `Testo.Platform.Routes`, never by referencing its component types), and
- **events** via `IAppEventBus` in `Platform` (e.g., `DefectCreated`) for in-app notifications.
  *(Designed, not built: no such interface exists yet.)*

## 5. Module system

Each module has exactly one public entry point:

```csharp
// Testo.Platform
public interface IFeatureModule
{
    string Id { get; }                          // "planning"
    string? FeatureFlag { get; }                // "Module.Planning" (null = always on)
    Assembly Assembly => GetType().Assembly;    // for the router
    void RegisterServices(IServiceCollection services) { }
    void RegisterNavigation(INavigationRegistry navigation) { }
    void RegisterWidgets(IDashboardWidgetRegistry widgets) { }
}
```

The three `Register…` methods do nothing by default, so a module only writes what it needs.

Host registration (one line per module):

```csharp
builder.Services.AddTestoModule<PlanningModule>();
builder.Services.AddTestoModule<DefectsModule>();
```

`AddTestoModule` registers the module (all modules are available as
`IEnumerable<IFeatureModule>`), calls its `RegisterServices`, and rejects a second module
with the same id. The host collects the module assemblies in `RoutableAssemblies` and
passes them to the router and to `MapRazorComponents<App>().AddAdditionalAssemblies(...)`,
so a module's `@page` components become routable without the host knowing them.

`ModuleConventionTests` checks every module project: exactly one public `IFeatureModule`,
named after the project, a lowercase id, a `Module.<Name>` flag that is listed in
`appsettings.json`, and a registration line in `Program.cs`.

**Status after Milestone 1:** the contracts and the one-line registration exist, and every
module is an empty shell behind its flag. The registry implementations (navigation,
widgets) and the flag-aware shell arrive in Milestone 4.

### Navigation registry

```csharp
public sealed record NavItem(
    string Id, LocalizedText Label, string Href, string Icon,
    NavGroup Group, int Order = 0, string? FeatureFlag = null, string? RequiredRole = null);

public interface INavigationRegistry { void Add(NavItem item); IReadOnlyList<NavItem> Items { get; } }
```

The shell's left navigation renders `Items` grouped by `NavGroup`
(Overview, Plan, Build, Run, Report, Settings), filtered by feature flags and role.
A label is a `LocalizedText`: a resource key together with the module's own resource class.
The shell looks the text up in the user's language when it renders, so modules keep
their translations to themselves.

### Dashboard widget registry

```csharp
public sealed record DashboardWidget(
    string Id, LocalizedText Title, Type ComponentType,
    WidgetSize Size = WidgetSize.Medium, int Order = 0, string? FeatureFlag = null);
```

The dashboard renders registered widgets with `<DynamicComponent>` inside a responsive grid.
Each widget component loads its own data through service interfaces.

## 6. Capability plug-ins

```csharp
// Testo.Platform
public interface ITestCapability
{
    string Id { get; }                      // "e2e-web"
    LocalizedText Name { get; }
    LocalizedText Description { get; }
    string Icon { get; }
    CapabilityKind Kind { get; }            // E2EWeb, Api, Performance, Security, Accessibility, ...
    string? FeatureFlag { get; }
    Type SettingsComponent { get; }         // shown in Settings → Capabilities
    Type RunSetupComponent { get; }         // shown when starting a run
    Type RunViewComponent { get; }          // live run UI (receives RunId parameter)
    Type ResultsComponent { get; }          // finished run results (receives RunId parameter)
}
```

- Registered via DI: `builder.Services.AddTestoCapability<E2EWebCapability>();` — **one line** (Milestone 6).
- `ICapabilityRegistry` exposes all enabled capabilities.
- Execution pages, Settings and the public "Capabilities" listing are **generic**: they
  list registered capabilities and render the capability's components with `DynamicComponent`.
  Adding "API testing" = new `Testo.Capabilities.Api` project + one registration line.
- Each capability may also register its own nav items/widgets through the same registries
  (it implements `IFeatureModule` too, optionally).

## 7. Data access: interfaces now, HTTP later

All data comes from interfaces in `Testo.Contracts`, grouped per domain:

> **Almost none of the interfaces in this table exist yet.** They are the design for the
> testing-journey milestones. `Testo.Contracts` defines six about identity —
> `IAuthenticationService`, `ICurrentUser`, `IOrganizationContext`, `IMembershipService`,
> `IInvitationService`, `IAuditLog` — and, since Milestone 3.3, `IExecutionService`, which is the
> row below named `ITestRunService`: it was built under the name of the module it serves, and
> carries the current project with it until the app shell has a project switcher (Milestone 5).
> Treat the other rows as a plan, not as an inventory.

| Interface | Purpose |
|---|---|
| `IProjectService` | projects, releases, current project |
| `IRequirementService` | requirements, test conditions, traceability matrix |
| `ITestPlanService` | test plans, risks, entry/exit criteria |
| `ITestCaseService` | test cases, versions, suites |
| `ITestRunService` | run list, run details, start/stop/rerun — **built** as `IExecutionService` (M3.3) |
| `ITestRunLiveFeed` | live run updates (see §8) |
| `IDefectService` | defects, lifecycle transitions, links |
| `IReportingService` | metrics, trends, release readiness, summary reports |
| `IEnvironmentService` | test environments |
| `ITeamService` | members, roles |
| `IIntegrationService` | integration status (Git, Azure DevOps, Jira placeholders) |
| `INotificationService` | notifications for the top bar |
| `ISearchService` | global search |
| `ICurrentUser` / `IAuthService` | mock sign-in, current user, roles |

Rules:
- **All methods are async** (`Task<T>` / `IAsyncEnumerable<T>`) and take a `CancellationToken`,
  even in mocks — so real HTTP calls fit without signature changes.
- Methods return **DTOs from Contracts** (immutable `record`s), never mock-internal types.
- Lists use a shared `PagedResult<T>` and `QueryOptions` (paging, sort, filter) shape that
  maps 1:1 to a future REST query.
- Errors use a `Result`-style return where failure is expected (validation), exceptions
  only for the unexpected. Mocks can **simulate latency and failures** (configurable)
  so loading and error states get built and tested now.

Composition (later swap):

```csharp
// now
builder.Services.AddTestoMocks();          // Testo.Mocks, in memory
// after the identity and data milestone
builder.Services.AddTestoApplication();    // Testo.Application, over PostgreSQL
```

The user interface does not change when that line changes: it is why every service is an
interface in `Contracts`. `Testo.Mocks` stays afterwards, for component tests and for a
demo mode that needs no database (ADR-0014).

Persistence, tenancy and identity are specified in [`domain-model.md`](domain-model.md)
and [`identity-and-access.md`](identity-and-access.md), with the decisions in ADR-0011
to ADR-0014.

Mock seed data is deterministic (fixed random seed) so tests and screenshots are stable.
Seed: 3–4 believable projects (e.g., a retail web shop, an insurance customer portal,
a logistics tracking app), each with releases, ~150 requirements, ~600 test cases,
run history over 90 days, and ~120 defects with realistic lifecycles.

## 8. Real-time: `ITestRunLiveFeed` *(designed, not built)*

> This interface does not exist in the code. The shape below is the intent; whether it survives
> contact with a real Playwright worker is one of the things the execution slice decides
> (ADR-0017).

```csharp
public interface ITestRunLiveFeed
{
    IAsyncEnumerable<TestRunEvent> SubscribeAsync(string runId, CancellationToken ct);
    Task StopAsync(string runId, CancellationToken ct);
}

public abstract record TestRunEvent(string RunId, DateTimeOffset Timestamp, long Sequence);
public sealed record RunStatusChanged(...)   : TestRunEvent(...);   // Queued, Running, Passed, Failed, Stopped
public sealed record StepStarted(...)        : TestRunEvent(...);   // step index, title, AI intent
public sealed record StepCompleted(...)      : TestRunEvent(...);   // status, duration, assertion
public sealed record ScreenshotCaptured(...) : TestRunEvent(...);   // step index, image URL
public sealed record LogEntryWritten(...)    : TestRunEvent(...);   // level, message, source
```

- **Mock:** `MockTestRunLiveFeed` replays scripted scenarios (e.g., "Checkout as guest")
  with timed events via an injectable `TimeProvider`, so bUnit tests can fast-forward time.
  Scenarios include a passing run, a run that fails at step 7, and a run with a
  self-healed locator.
- **Real (later):** `SignalRTestRunLiveFeed` wraps a SignalR hub connection and maps hub
  messages to the same event records. `Sequence` lets the UI de-duplicate and detect gaps
  on reconnect.
- The live view is a pure function of the event stream (reducer → `LiveRunState`), which
  keeps it easy to test.

## 9. Render modes *(ADR-0003, ADR-0009)*

| Area | Render mode | Why |
|---|---|---|
| Public website (`/en/...`, `/de/...`), not-found and error page | **Static SSR, without the Blazor script** | Fast first load, SEO, no server connection and no cookie for visitors |
| Product app (`/app/**`) | **Interactive Server** | Rich components (Fluent grids, dialogs), live feed, simplest with in-process mocks |

How a page gets its mode:

- A page is **public** when it carries `[ExcludeFromInteractiveRouting]`. `Testo.Website`
  applies it to all of its pages in `Pages/_Imports.razor`. Every other page is an **app page**.
- `App.razor` asks the framework (`HttpContext.AcceptsInteractiveRouting()`) and renders
  the router either statically or with `InteractiveServer`. App pages therefore need no
  `@rendermode` of their own, and navigation inside the app stays on one server connection.
- For a public page, `App.razor` leaves out `blazor.web.js`, the import map and the app's
  style bundle. A public page loads our three small style sheets, two component style
  bundles, one font file and `theme.js` — about 85 KB in total, uncompressed.
- **No cookie on a plain visit.** Blazor issues an antiforgery token (a cookie plus
  "no-store") for every page it renders. `PublicPageAntiforgery` skips that for public
  pages. A public page that has a form posting back to itself opts in with `[HasForm]`.
- **State from the request to the interactive session.** An interactive session has no
  HTTP request and so no cookies. The theme choice is read from the cookie while the page
  is rendered on the server and carried over with `[PersistentState]` (`ThemeState`).
  Milestone 4 uses the same route for the signed-in user and the current project.
- **Friendly status pages** (`/not-found`, `/error`) are shown for GET and HEAD requests
  only; a rejected form post keeps its plain status code. On these pages the language
  comes from the URL the visitor asked for, and links and forms keep pointing at it
  (`RequestedUrl`).

Interactive WebAssembly is deliberately **not** used in this phase: mocks would have to
ship to the browser and the download is larger. Because every service sits behind an
interface, and because no library references the server framework (§4), switching the
app area to `InteractiveAuto` later is a contained change (add a `.Client` project + HTTP
implementations).

### Small JavaScript on static pages *(ADR-0004)*

In static SSR there is no Blazor circuit, so Blazor event handlers don't run. Three public
header features need behavior:

| Feature | No-JS baseline | Small JS enhancement (~2 KB, vanilla ES module) |
|---|---|---|
| Mega menu | `<details>/<summary>` disclosure, keyboard-accessible | close on Escape / outside click, one menu open at a time, `aria-expanded` sync |
| Theme switch *(built in Milestone 1: `js/theme.js`, 1.9 KB)* | form POST sets a cookie, page reloads | instant toggle without reload; writes the same cookie |
| Mobile menu | `<details>` drawer | focus trap while open |

The alternative — making the header an interactive Blazor island — would open a SignalR
circuit (or download WASM) for every visitor, which hurts first load. Decision: the
small JS module. No JS framework.

There is no flash of the wrong theme even without JavaScript: the server renders
`<html data-theme="…">` from the cookie, and "same as system" is handled by CSS alone
(`color-scheme` and `light-dark()`, ADR-0008).

All JavaScript in the repository: `Testo.UI.Shared/wwwroot/js/theme.js` (ours) and
`ReconnectModal.razor.js` (the reconnect dialog, unchanged from the .NET project template).

## 10. Localization

- `IStringLocalizer<T>` with `.resx` files **inside each project** (`Resources/` folder),
  so modules stay self-contained. Each project has a small marker class next to its
  resource files (for example `Resources/SharedResources.cs` + `SharedResources.resx` +
  `SharedResources.de.resx`) and injects `IStringLocalizer<SharedResources>` as `L`.
- Cultures: `en` (default) and `de`, defined once in `Testo.Platform.Localization.SupportedCultures`.
  Dates and numbers use the current culture.
- **Public website:** the language is the first URL segment (`/en/pricing`, `/de/pricing`).
  Every page declares one literal route per language; `/` redirects to the visitor's
  language. `App.razor` emits `canonical` and `hreflang` links *(ADR-0006)*.
- **App:** the language comes from the preference cookie (`.AspNetCore.Culture`), then
  from the browser. The cookie is written only when the visitor uses the language switch.
- The language of a request is resolved in this order: URL prefix, cookie,
  `Accept-Language`, English. The built-in `?culture=` query string is switched off.
- `ResourceFileTests` checks every resource file: German has exactly the English keys, no
  text is empty, placeholders match, every key used in code exists, and every key is used.
- `PageRouteTests` checks that every public page has exactly one route per language.

## 11. Feature flags

- `Microsoft.FeatureManagement` with flags in `appsettings.json` (`FeatureManagement` section).
- Naming: `Module.<Name>`, `Capability.<Name>`, `Feature.<Name>` (e.g., `Feature.AiSuggestions`).
- UI code asks through `Testo.Platform.Features.IFeatureFlags`, not through the library.
  The host implements it with Microsoft.FeatureManagement today; a WebAssembly client or
  the backend can supply the flags differently later without touching UI code.
- From Milestone 4: registries filter nav items, widgets and capabilities by flag, and a
  disabled module returns a friendly "not available" page, not a 500.
- A dev-only "Feature flags" panel in Settings shows current flag states (Milestone 5.7).
  Until then the app preview page lists every module with its flag.

## 12. Authentication (mock)

- `MockAuthenticationStateProvider` with 3–4 demo users (QA lead, QA engineer, developer,
  engineering manager) to demonstrate role-based nav.
- `/sign-in` page with "Continue as …" demo buttons; cookie-based so the static and
  interactive parts agree on the user.
- The real backend will use OpenID Connect (e.g., Entra ID); only the provider registration changes.

## 13. Testing strategy

| Layer | Tool | What |
|---|---|---|
| Components | bUnit + xUnit | Every reusable component: rendering, parameters, events, ARIA attributes, keyboard behavior; with the real English and German texts |
| Module pages | bUnit | Loading / empty / error / data states using fake services |
| Mocks | xUnit | Seed data consistency (all links valid), live-feed event order and timing |
| Rules | xUnit | Dependency rules, module conventions, page routes, resource files, token contrast (WCAG AA in both themes), "component styles use tokens" |
| Host over HTTP | xUnit + `HttpClient` | Preference endpoints, redirects, cookies, caching and security headers, feature flags from configuration |
| E2E | Playwright for .NET | Smoke test per page (title, one `h1`, no browser problems, axe scan in light and dark), theme, language, keyboard, reflow at 320 px |

How the tests run *(ADR-0010)*:

- `dotnet test` runs all three test projects on Microsoft.Testing.Platform.
- The E2E project starts the real application inside the test process on a free port
  (`WebApplicationFactory` with Kestrel), in the "Production" environment, and one browser
  for the whole run. Every test gets its own browser context, so tests share nothing.
- Every browser test fails on any console error or warning, script error, failed request
  or HTTP error it did not announce. That is also what catches a violation of the
  content security policy.

## 14. Cross-cutting

- **Error handling:** a shell-level `ErrorBoundary` per widget/region so one failing widget doesn't break a page (from Milestone 4). Unhandled server errors show the friendly error page with a request ID and no details.
- **Loading states:** skeleton components from `UI.Shared` (Milestone 2).
- **Logging:** `ILogger<T>` everywhere; no `Console.WriteLine`.
- **Security headers** (`SecurityHeadersExtensions`): `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, HSTS outside development, and a content security policy on every response. Public pages allow nothing inline. App pages allow inline styles (the Fluent UI components need them) and the one inline script that carries the request's nonce (the import map).
- **Preference endpoints** (`/preferences/theme`, `/preferences/culture`): plain form posts without an antiforgery token, because the switches sit in the header of every page. They store only a display preference, reject posts from other websites (`Sec-Fetch-Site` / `Origin`) and only redirect within the site.
- **Privacy:** no third-party requests at all. Fonts are self-hosted (no font CDN — relevant for German GDPR case law). A plain visit to a public page sets no cookie; the theme and language cookies are written only when the visitor uses a switch.
- **Fonts:** the file names carry the font version and are cached for a year (`immutable`), so pages do not re-validate them on every load.
