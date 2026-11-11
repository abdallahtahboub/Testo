# Architecture (Frontend Phase)

> Status: **Draft for review (Step 0)**. Decisions marked *(ADR-xxxx)* are recorded in
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
| Component tests | xUnit + bUnit |
| E2E tests | Playwright for .NET (+ xUnit) |
| Accessibility checks | `Deque.AxeCore.Playwright` inside the E2E project |
| CI | GitHub Actions: restore → build → bUnit → Playwright |
| Central package versions | `Directory.Packages.props`; shared settings in `Directory.Build.props` |

## 3. Solution structure

The suggested structure is kept, with **three changes** (reasons below):

```
Testo.sln
Directory.Build.props            // nullable, warnings-as-errors, analyzers, LangVersion
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
  Testo.Modules.Execution        // incl. implementation (suites, environments) + regression
  Testo.Modules.Defects
  Testo.Modules.Reporting        // incl. monitoring & control, release readiness, completion
  Testo.Modules.Settings         // NEW: integrations, team, capability settings host
  Testo.Capabilities.E2EWeb      // first capability plug-in
  Testo.Mocks                    // Mock* services, seed data, simulated live feed
tests/
  Testo.UI.Tests                 // bUnit: UI.Shared components + module components
  Testo.Mocks.Tests              // xUnit: seed data consistency, mock feed timing
  Testo.E2E                      // Playwright: smoke per page + main flows
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
```

| Project | May reference | Must NOT reference |
|---|---|---|
| `Contracts` | BCL only | anything UI, any other Testo project |
| `Platform` | `Contracts`, ASP.NET Core Components | modules, mocks, UI.Shared |
| `UI.Shared` | `Platform`, `Contracts`, Fluent UI | modules, capabilities, mocks |
| `Website` | `UI.Shared`, `Contracts` | modules, mocks |
| `Modules.*` | `UI.Shared`, `Platform`, `Contracts` | **other modules**, capabilities, mocks |
| `Capabilities.*` | `UI.Shared`, `Platform`, `Contracts` | modules, other capabilities, mocks |
| `Mocks` | `Contracts` | anything UI |
| `Web` | everything (composition root only, no business UI) | — |

These rules are **enforced by a test** (`tests/Testo.UI.Tests/Architecture/DependencyRulesTests.cs`,
reflection over referenced assemblies) so they can't silently erode.

Cross-module communication only happens through:
- **Contracts** (shared DTOs + service interfaces), and
- **routes** (a module links to another module's page by URL constants published in
  `Testo.Platform.Routes`, never by referencing its component types), and
- **events** via `IAppEventBus` in `Platform` (e.g., `DefectCreated`) for in-app notifications.

## 5. Module system

Each module has exactly one public entry point:

```csharp
// Testo.Platform
public interface IFeatureModule
{
    string Id { get; }                          // "planning"
    string? FeatureFlag { get; }                // "Module.Planning" (null = always on)
    Assembly Assembly { get; }                  // for router AdditionalAssemblies
    void RegisterServices(IServiceCollection services);
    void RegisterNavigation(INavigationRegistry nav);
    void RegisterWidgets(IDashboardWidgetRegistry widgets);
}
```

Host registration (one line per module):

```csharp
builder.AddTestoModule<PlanningModule>();
builder.AddTestoModule<DefectsModule>();
```

The host collects module assemblies and passes them to the router
(`AdditionalAssemblies`) and to `MapRazorComponents<App>().AddAdditionalAssemblies(...)`,
so a module's `@page` components become routable without the host knowing them.

### Navigation registry

```csharp
public sealed record NavItem(
    string Id, string LabelResourceKey, string Href, string Icon,
    NavGroup Group, int Order, string? FeatureFlag = null, string? RequiredRole = null);

public interface INavigationRegistry { void Add(NavItem item); IReadOnlyList<NavItem> Items { get; } }
```

The shell's left navigation renders `Items` grouped by `NavGroup`
(Overview, Plan, Build, Run, Report, Settings), filtered by feature flags and role.
Labels are resource keys, so modules localize their own entries.

### Dashboard widget registry

```csharp
public sealed record DashboardWidget(
    string Id, string TitleResourceKey, Type ComponentType,
    WidgetSize Size, int Order, string? FeatureFlag = null);
```

The dashboard renders registered widgets with `<DynamicComponent>` inside a responsive grid.
Each widget component loads its own data through service interfaces.

## 6. Capability plug-ins

```csharp
// Testo.Platform
public interface ITestCapability
{
    string Id { get; }                      // "e2e-web"
    string NameResourceKey { get; }
    string DescriptionResourceKey { get; }
    string Icon { get; }
    CapabilityKind Kind { get; }            // E2EWeb, Api, Performance, Security, Accessibility, ...
    string? FeatureFlag { get; }
    Type SettingsComponent { get; }         // shown in Settings → Capabilities
    Type RunSetupComponent { get; }         // shown when starting a run
    Type RunViewComponent { get; }          // live run UI (receives RunId parameter)
    Type ResultsComponent { get; }          // finished run results (receives RunId parameter)
}
```

- Registered via DI: `builder.AddTestoCapability<E2EWebCapability>();` — **one line**.
- `ICapabilityRegistry` exposes all enabled capabilities.
- Execution pages, Settings and the public "Capabilities" listing are **generic**: they
  list registered capabilities and render the capability's components with `DynamicComponent`.
  Adding "API testing" = new `Testo.Capabilities.Api` project + one registration line.
- Each capability may also register its own nav items/widgets through the same registries
  (it implements `IFeatureModule` too, optionally).

## 7. Data access: interfaces now, HTTP later

All data comes from interfaces in `Testo.Contracts`, grouped per domain:

| Interface | Purpose |
|---|---|
| `IProjectService` | projects, releases, current project |
| `IRequirementService` | requirements, test conditions, traceability matrix |
| `ITestPlanService` | test plans, risks, entry/exit criteria |
| `ITestCaseService` | test cases, versions, suites |
| `ITestRunService` | run list, run details, start/stop/rerun |
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
builder.Services.AddTestoMocks();          // Testo.Mocks
// later
builder.Services.AddTestoHttpClients(apiBaseUrl);  // Testo.Client.Http, same interfaces
```

Mock seed data is deterministic (fixed random seed) so tests and screenshots are stable.
Seed: 3–4 believable projects (e.g., a retail web shop, an insurance customer portal,
a logistics tracking app), each with releases, ~150 requirements, ~600 test cases,
run history over 90 days, and ~120 defects with realistic lifecycles.

## 8. Real-time: `ITestRunLiveFeed`

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

## 9. Render modes *(ADR-0003)*

| Area | Render mode | Why |
|---|---|---|
| Public website (`/`, `/product`, `/pricing`, …) | **Static SSR** + enhanced navigation | Fast first load, SEO, no SignalR circuit for visitors |
| Contact form | Static SSR form post (enhanced form) | No interactivity needed |
| Product app (`/app/**`) | **Interactive Server** (per page/layout) | Rich components (Fluent grids, dialogs), live feed, simplest with in-process mocks |

Interactive WebAssembly is deliberately **not** used in this phase: mocks would have to
ship to the browser and the download is larger. Because every service sits behind an
interface, switching the app area to `InteractiveAuto` later is a contained change
(add a `.Client` project + HTTP implementations).

### Small JavaScript on static pages *(ADR-0004, needs your approval)*

In static SSR there is no Blazor circuit, so Blazor event handlers don't run. Three public
header features need behavior:

| Feature | No-JS baseline | Small JS enhancement (~2 KB, vanilla ES module) |
|---|---|---|
| Mega menu | `<details>/<summary>` disclosure, keyboard-accessible | close on Escape / outside click, one menu open at a time, `aria-expanded` sync |
| Theme switch | form POST sets a cookie, page reloads | instant toggle without reload, respects `prefers-color-scheme` before first paint (avoids flash) |
| Mobile menu | `<details>` drawer | focus trap while open |

The alternative — making the header an interactive Blazor island — would open a SignalR
circuit (or download WASM) for every visitor, which hurts first load. Recommendation: the
small JS module. No JS framework.

## 10. Localization

- `IStringLocalizer<T>` with `.resx` files **inside each module** (`Resources/` folder),
  so modules stay self-contained.
- Cultures: `en` (default) and `de`. Dates, numbers, and relative times use the current culture.
- **Public website:** culture in the URL path (`/en/pricing`, `/de/pricing`),
  with `hreflang` links for SEO. See open question in `sitemap.md`.
- **App:** culture from the user's preference (cookie `.AspNetCore.Culture`), switchable in the user menu.
- A unit test checks that every key in `*.resx` (en) exists in `*.de.resx` and vice versa.

## 11. Feature flags

- `Microsoft.FeatureManagement` with flags in `appsettings.json` (`FeatureManagement` section).
- Naming: `Module.<Name>`, `Capability.<Name>`, `Feature.<Name>` (e.g., `Feature.AiSuggestions`).
- Registries filter nav items, widgets and capabilities by flag; pages use
  `[FeatureGate]`-style checks (a `<FeatureGate>` component and a route guard) so a
  disabled module returns a friendly "not available" page, not a 500.
- A dev-only "Feature flags" panel in Settings shows current flag states.

## 12. Authentication (mock)

- `MockAuthenticationStateProvider` with 3–4 demo users (QA lead, QA engineer, developer,
  engineering manager) to demonstrate role-based nav.
- `/sign-in` page with "Continue as …" demo buttons; cookie-based so the static and
  interactive parts agree on the user.
- The real backend will use OpenID Connect (e.g., Entra ID); only the provider registration changes.

## 13. Testing strategy

| Layer | Tool | What |
|---|---|---|
| Components | bUnit + xUnit | Every reusable component: rendering, parameters, events, ARIA attributes, keyboard behavior |
| Module pages | bUnit | Loading / empty / error / data states using fake services |
| Mocks | xUnit | Seed data consistency (all links valid), live-feed event order and timing |
| Architecture | xUnit | Dependency rules, resx key parity |
| E2E | Playwright for .NET | Smoke test per page (renders, no console errors, title, `h1`), main flows, axe accessibility scan, light + dark, mobile + desktop viewports |

## 14. Cross-cutting

- **Error handling:** a shell-level `ErrorBoundary` per widget/region so one failing widget doesn't break a page.
- **Loading states:** skeleton components from `UI.Shared`.
- **Logging:** `ILogger<T>` everywhere; no `Console.WriteLine`.
- **Security headers:** CSP (no inline scripts, which also rules out inline theme scripts — the theme script is an external file), HSTS, `X-Content-Type-Options`, `Referrer-Policy`.
- **Privacy:** no third-party trackers, fonts self-hosted (no Google Fonts CDN — relevant for German GDPR case law).
