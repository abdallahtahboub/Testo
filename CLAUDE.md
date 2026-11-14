# CLAUDE.md — Conventions for every session

**Testo** (working name) is an AI-assisted QA platform covering the whole ISTQB testing
journey.

**Current state (2026-11-14, during Milestone 3.4).** Identity, organizations, memberships,
invitations, roles and the audit log are **real**, on PostgreSQL. The design system is real.
**Test runs are real** for three scripted scenarios against our own demo target: `Testo.Worker`
drives Chromium out of process behind an egress proxy, `test_runs`/`test_run_steps`/`artifacts`
are persisted inside the tenant boundary, and `/app/runs` starts, stops and reruns them. No
customer-supplied target. **An AI planner exists and is unmeasured**: the interface, the closed
action vocabulary, the validator, the Anthropic adapter and the benchmark are built (ADR-0019), the
hand-written baseline is measured at 36/36 correct, and the planner's own column in
`docs/ai-benchmark.md` is empty because no model key was available. Nothing in the product calls a
model yet, and no claim about AI-found defects is supported. Everything else about the testing journey —
plans, requirements, cases, suites, environments, defects, reporting — **does not exist yet, in
any form, not even mocked**: six of the seven module projects are shells of one class each, and
`Testo.Mocks` holds options and a random source, no services. `docs/roadmap.md` §Inventory is the
authoritative list of what is built.

Read first: `docs/architecture.md`, `docs/roadmap.md`, `docs/design-system.md`,
`docs/sitemap.md`, `docs/testing-journey.md`, `docs/decisions/`.

## Working agreement

- **Plan first.** Work milestone by milestone (`docs/roadmap.md`). **Stop after each milestone** and summarize for review. Don't start the next one without approval.
- **Branches:** work on `<type>/<slug>` cut from `develop`; never commit to `develop` or `main` directly. A change reaches `develop` through a pull request with green CI, and `develop` reaches `main` at a release. See `CONTRIBUTING.md`.
- **Ask before** adding a dependency, deviating from the architecture, using JavaScript, or using a non-.NET tool. Explain why.
- **Update docs when you change behavior or structure** — in the same commit. Record architectural decisions as ADRs in `docs/decisions/NNNN-short-title.md` (template: `0000-template.md`).
- Commit after each meaningful step; small commits with clear messages (imperative mood, e.g. `Add TKpiTile component with delta indicator`).
- Before saying a milestone is done: `dotnet build` (zero warnings) and `dotnet test` (incl. Playwright) must pass. Report failures honestly.

## Product name

**Testo** is the confirmed working name; it may still change. Never hard-code it in UI text: use the `Brand:ProductName`
resource / `BrandOptions.ProductName`. Project/namespace prefix is `Testo.`; a rename later is a
mechanical find-and-replace.

## Architecture rules (enforced by `DependencyRulesTests`)

- `Testo.Contracts`: DTOs + service interfaces only. **No UI dependencies.** The backend will reuse it.
- `Testo.Platform`: module/capability contracts and registries (`IFeatureModule`, `ITestCapability`, `INavigationRegistry`, `IDashboardWidgetRegistry`, `ICapabilityRegistry`, `Routes`).
- `Testo.UI.Shared`: design system, layouts, tokens. No business logic, no module references.
- `Testo.Modules.*` and `Testo.Capabilities.*` reference only `Contracts`, `Platform`, `UI.Shared`. **Never another module or capability.** Cross-module links go through `Testo.Platform.Routes` constants. (`IAppEventBus`, for cross-module events, is designed in `docs/architecture.md` §6 but **not built**.)
- `Testo.Website` (public pages) references only `Contracts`, `Platform`, `UI.Shared`.
- `Testo.Mocks` references only `Contracts`.
- `Testo.Domain`: entities and the rules that hold regardless of storage. References only `Contracts`. **No EF Core, no ASP.NET Core Identity, no UI.**
- `Testo.Persistence`: EF Core on PostgreSQL, the migrations, and the Identity stores. **The only project that may see ASP.NET Core Identity**, and only through `CredentialService`.
- `Testo.Application`: the use cases; implements the `Contracts` interfaces. References `Domain`, `Persistence`, `Contracts`. A failed use case returns **resource keys**, never English sentences.
- Everything inside the tenant boundary implements `IBelongsToOrganization`. Never write a `WHERE organization_id = …` by hand; the query filter does it. A query that must cross the boundary says `IgnoreQueryFilters()` with a comment saying why.
- Account pages (`/account/**`) are **static SSR with plain form posts** (ADR-0016): a cookie can only be issued during an HTTP request. They carry `[ExcludeFromInteractiveRouting]` and `[HasForm]`.
- `Testo.Web` is the composition root only: DI, routing, `App.razor`, dashboard host page. It is the **only** project that references the ASP.NET Core server framework; libraries never use `HttpContext` or other server-only types.
- A new project needs a rule in `DependencyRulesTests` and a row in `docs/architecture.md` §4.
- Each module exposes **one** public `IFeatureModule` class named `<Name>Module`, with a lowercase id and the flag `Module.<Name>` (listed in `appsettings.json`); everything else is `internal` where possible. The host registers it with one line: `builder.Services.AddTestoModule<NameModule>()`.
- Nav items, dashboard widgets and capabilities are **registered**, never hard-coded in the shell.
- All data access via interfaces from `Contracts`. Components never `new` a service or touch mock data directly.
- Service methods: async, `CancellationToken` last parameter, return Contracts DTOs (`record`s), lists via `PagedResult<T>`.
- Every unfinished module/capability sits behind a feature flag (`Module.X`, `Capability.X`, `Feature.X`).

## Render modes

- Public website (`Testo.Website`): **static SSR, without the Blazor script**. No `@rendermode`, no event handlers (`@onclick` does nothing there), no enhanced navigation. Use links and plain forms.
- A page is public when it has `[ExcludeFromInteractiveRouting]`; `Testo.Website/Pages/_Imports.razor` applies it (and `SiteLayout`) to every page in that folder. Everything else is an app page.
- App (`/app/**`): **InteractiveServer**, applied once in `App.razor`. App pages need no `@rendermode` and use `AppLayout` by default.
- Public pages get no antiforgery token. A public page with a form that posts back to itself needs `@attribute [HasForm]`.
- An interactive session has no HTTP request: no cookies, no `HttpContext`. Read such values while the page is rendered on the server and carry them over with `[PersistentState]` (see `ThemeState`).
- JavaScript only where Blazor can't do it, as small vanilla ES modules in `wwwroot/js/`, documented in an ADR. No JS frameworks, no inline scripts and no inline styles in our markup (CSP).

## Languages and URLs

- Every public page declares one literal route per language: `@page "/en/pricing"` and `@page "/de/pricing"`. Build public links with `LocalizedPath.For(SupportedCultures.Current, "pricing")`.
- App pages have no language prefix; routes start with `/app`.
- Texts: one marker class per project next to its resource files (`Resources/XResources.cs`, `.resx`, `.de.resx`), injected as `@inject IStringLocalizer<XResources> L`, used as `L["Area_Element_Purpose"]`.
- The product name goes into texts as a placeholder: `L["Home_PageTitle", Brand.Value.ProductName]`.

## Folder structure inside a module

```
Testo.Modules.Defects/
  DefectsModule.cs            // IFeatureModule: services, nav, widgets
  Pages/                      // routable components (@page), one folder per area if large
  Components/                 // module-specific components
  ViewModels/                 // presentation models + mapping from DTOs
  Widgets/                    // dashboard widgets
  Resources/                  // *.resx (en) + *.de.resx
  _Imports.razor
```

## Naming

- Namespaces = folder path (`Testo.Modules.Defects.Pages`). File-scoped namespaces.
- Design-system components: `T` prefix (`TButton`, `TDataTable`). Module components: no prefix, descriptive (`DefectSeverityChart`).
- Pages end with `Page` (`DefectListPage.razor`); widgets end with `Widget`; view models end with `ViewModel`.
- Interfaces `I*Service`; mock implementations `Mock*Service`; future HTTP implementations `Http*Service`.
- DTOs end with `Dto` only when needed to avoid a clash; otherwise plain nouns (`Defect`, `TestRun`).
- Routes: lowercase, kebab-case; app routes under `/app/p/{projectKey}/...`.
- Resource keys: `Area_Element_Purpose` (e.g., `DefectList_Filter_Severity`).
- CSS: component styles in `Component.razor.css` (CSS isolation); global tokens only in `UI.Shared/wwwroot/css/tokens.css`. **A class cannot cross a component boundary**: CSS isolation scopes every selector to the component that declares it, so a class shared by several components goes in `wwwroot/css/controls.css`, and a class passed *to* a child component never reaches it — wrap the child in an element the parent owns. Use tokens (`var(--space-4)`), **never raw colors or px values** in component CSS except 0 and 1px (`ComponentStyleTests` enforces it). Color tokens are named by role and hold both themes: `light-dark(<light>, <dark>)`. Media queries use the breakpoints of `docs/design-system.md` §5 in `rem`.

## C# style

- Nullable on, warnings as errors, latest analyzers. No `!` null-forgiving unless justified in a comment.
- `record` for DTOs; `sealed` by default for classes; primary constructors for DI.
- Code-behind (`.razor.cs`) once a component's `@code` block exceeds ~40 lines.
- Components small and focused; no copy-pasted markup between pages — extract a component.
- `ILogger<T>` for logging; no `Console.WriteLine`.
- Use `TimeProvider` instead of `DateTime.Now` / `Task.Delay` in anything time-based (testability).
- Log with `[LoggerMessage]` partial methods, not `logger.LogInformation("…", args)`; the analyzers require it.
- A layout never lives in the folder its `_Imports.razor` points at with `@layout`: it would be given itself as its own layout and nest without end. Layouts go in `Components/Layout`.
- `EditForm` renders its own antiforgery token in static rendering. Do not add `<AntiforgeryToken />` as well, or the post carries two and the server answers 400.

## UI rules

- Follow `docs/design-system.md`: tokens, type scale, grid, motion, accessibility.
- **WCAG 2.2 AA**: keyboard support, visible focus, contrast, labels, landmarks, one `h1` per page, status never by color alone, `prefers-reduced-motion` respected.
- Every async view has loading, empty, error and data states.
- All user-facing text via `IStringLocalizer` — **English and German together**, never one without the other.
- Mock data must look realistic (believable names, IDs, dates); seed is deterministic.
- Placeholders (logos, pricing, legal pages) are visibly labeled as placeholders.
- Original brand only: never copy another company's logo, text, images, icons, fonts or layouts.

## Testing rules

- Every reusable component (`UI.Shared`, module components used in more than one place) has **bUnit** tests: rendering, parameters, events, ARIA, keyboard.
- **Look at it in a browser before calling it done.** Unit tests do not catch an unstyled control, a class that never applied, or a sample heading that joined the page outline. All three happened in Milestone 2.
- A browser test must wait for the page to be interactive before pressing keys: until the circuit connects the page is only prerendered, and its components are replaced when it does.
- Every page has at least one **Playwright smoke test**: loads, correct title + `h1`, no console errors, axe scan with zero serious/critical violations.
- Main flows get dedicated E2E tests (see roadmap Milestone 7).
- Test names: `MethodOrScenario_Condition_ExpectedResult`.
- Use fakes (in-test implementations of the service interfaces) in bUnit tests; don't depend on mock seed data details.
- Time-based tests use a fake `TimeProvider`; no real sleeps.
- bUnit tests derive from `DesignSystemTestContext` (real resource files, brand options, theme state, fixed clock). Test German with `using var german = new CultureScope("de");`.
- Browser tests derive from `BrowserTest`, open pages with `OpenAsync` and end with `page.AssertNoProblems()`; page smoke tests also call `page.AssertAccessibleAsync()`. Tests over raw HTTP derive from `HttpTest`.
- In browser tests, check styles and state with Playwright's waiting assertions (`Expect(...).ToHaveCSSAsync`, `ToHaveAttributeAsync`), never with a one-off read right after an action.
- A rule in this file or in `docs/` that can be checked by a test should be (see `tests/Testo.UI.Tests/Architecture`, `Design`, `Localization`).

## Commands

Run these from the repository root (the folder with `Testo.slnx`); from inside a project
folder `dotnet test` reports "No test projects were found".

```bash
dotnet build
dotnet test                                    # all tests, including the browser tests
dotnet test --project tests/Testo.UI.Tests     # one test project
dotnet test --project tests/Testo.E2E --filter-class "Testo.E2E.Pages.ThemeTests"
dotnet run --project src/Testo.Web             # http://localhost:5080
```

The product needs PostgreSQL. `docker run -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16`,
or point `ConnectionStrings__Testo` elsewhere. The data and browser tests create a database of
their own per class and find the server through `TESTO_TEST_DB`.

- Tests run on Microsoft.Testing.Platform (xUnit v3): filters are `--filter-class`, `--filter-method`, `--filter-namespace`; reports are `--report-xunit-trx` and `--report-xunit-html`.
- The browser tests start the application themselves; nothing needs to run beforehand.
- Browser for the tests, once per machine after a build: `pwsh tests/Testo.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`. Or point `E2E_BROWSER_PATH` at an existing Chromium executable.
