# CLAUDE.md — Conventions for every session

**Autestiq** is an AI-assisted QA platform covering the whole ISTQB testing journey.

**Current state (2026-11-16, after Milestones 3.5 to 3.8 part 1).** Identity, organizations, memberships,
invitations, roles and the audit log are **real**, on PostgreSQL. The design system and the public website
(29 routes, English and German, placeholders labeled) are real. **Test runs are real** against our own target
applications: the demo shop, a clinic and an invoice desk, each with switchable defects. **Customer-site safety
is built (3.5)**: proof of site ownership, a kill switch, reaping of stuck runs, and a sealed container per run
(ADR-0026). Customer origins are refused outside container mode, and no screen asks for one until Milestone 5.
**Usage and quotas are built (3.6)**: an append-only usage ledger, per-organization concurrency and monthly
minutes with reservation, and rate limits on starts (ADR-0027). **The plan catalog is built (3.7)**: versioned
plans in the database, each organization on a version it keeps until an operator moves it (ADR-0028). **Cost per run
is built (3.8, part 1)**: a price book of versions that never change, cost rows per run at the price in force when it
finished, and an operator cost report (ADR-0029); machine and storage prices are estimates until hosting is chosen.
**The AI planner is built and measured, not yet in the product**: on the unseen set (20 scenarios written after the
prompt was frozen, 2026-11-17) it was 95% correct on sound applications, caught every seeded defect where a
verdict was reached, and never passed a broken application, so **the ADR-0020 gate's figures are met on our
three applications**, and the same holds with every test id removed. **Explored read-only, as a customer's site
is, it drops to 36 of 66 runs correct (full: 62 of 66), still with no false positive**: read-only exploration
never sees what an action leads to (ADR-0020). **On eShopOnWeb, the first application nobody here wrote, it gave the
right verdict on 14 of 22 runs, and 18 of 22 (82%) after two explorer changes made on that application's own results,
with no false positive either time**. Signed in, with the account guard, the tester's stated thank-you and ambiguous steps refused, it
is 18 of 22, still with no false positive: placing an order now reaches a verdict, and the sign-in check the guard distrusts is inconclusive. The gate's 90% is
not met there, and the 82% is not a clean measurement. **The clean measurement, on Spring PetClinic (2026-11-23,
scenarios written from its source before any run, nothing tuned): 20 of 22 runs correct (91%), no false positive**;
two passes tested less than they claimed (16 of 22 strictly), and it is one application without defects to catch
(ADR-0020). **Re-measured with the current prompt (2026-11-24, ADR-0035 step 2: a plan lists what it does not test)**: the unseen
sets 64 of 66 runs correct with and without test ids, no false positive; **PetClinic 12 of 22 correct, 10 inconclusive,
no false positive**, every inconclusive naming a real gap (an unlabelled search box; a check that cannot be scoped to a
row). Read-only exploration and eShopOnWeb figures above still describe the earlier prompt. **Milestone 5, the first customer flow, is done and accepted by the owner (2026-11-23, ADR-0030)**: part 1 built projects, environments with
ownership proofs, production off by default, and the app's navigation drawn from the module registry. Part 2 built
describing a test in a sentence: the site is explored (read-only on a customer's site) and the planner writes a plan,
**only if the project's AI permission, off by default, is given**, within the AI budgets, with what was sent recorded
and the AI's notes labelled unverified. Part 3 built approving and running a plan: steps that may change data are
decided by software, approval (Owner, QA lead) needs an explicit tick for them and is checked again by the dispatcher,
and a plan approved without it runs read-only, held in the browser by the worker.
Part 4 built downloading an approved test, which completes the first customer flow. **Exploration may act on a
customer's test site only with an Owner's explicit, audited permission, never on production (ADR-0032).** **A test
account's sign-in values are kept per test environment (ADR-0033)**: encrypted, write-only, never production, never to a
model; exploration signs in with them only under ADR-0032's permission. **A tester may state expected results
(ADR-0034)**, sent to the planner labelled as theirs; every check is labelled by software as observed, stated or assumed. **Evidence retention is built
(3.8 part 2, ADR-0031)**: a screenshot policy per project, an hourly job that deletes expired screenshots, none stored
when a project keeps none; object storage waits for hosting. **Exporting plans as tests** is built for Python and
verified (ADR-0023), previewed on a test's page and downloaded once approved, except a plan whose pass would rest on the test account's own value (Milestone 5.1 R4). **Deployable in principle, not deployed (2026-11-25)**: email through any SMTP relay and an operator command for the first owner (ADR-0040), a runbook rehearsed locally (`docs/operations/pilot-runbook.md`); no host, no email provider chosen, no release (`develop` far ahead of `main`). Milestone 5.1,
approved 2026-11-24, is the pre-pilot milestone; R1 (planning attempts recorded before the call, a lease, one planning
step per deployment, ADR-0037), R4 (no download of a plan resting on the account's own value), R2 (operator `migrate` and `create-organization`, `/health/live` and `/health/ready`, SMTP email; ADR-0039, ADR-0040) and R6 (content-free analytics views and the operator's `rehearsal-report`) are built; R3's runbook is rehearsed locally (2026-11-25); R5, the release-candidate measurement, ran in part on `v0.5.0-rc.1` (2026-11-25): 7 of 12 unseen runs, no false positive, every stated result confirmed or caught; the rest not run for lack of credit (ADR-0020) (`docs/roadmap.md`; findings in
`docs/reviews/2026-11-24-pilot-readiness.md`). **The owner's long-term goal (2026-11-25)**: understand a customer's application from exploring it and its context (business model, stories, stakeholder input; code optional), then build its scenarios, suites, test plan and strategy (`docs/business/goal-application-understanding.md`; a goal, not built). Milestone 5.1 R8 (guarded pressing) and R9 (more third-party applications) were added the same day; **R8 is built (ADR-0041)**: exploring a site it may not act on presses buttons, tabs and links with every write blocked in the browser and no form sent, which adds nothing measurable on our three server-rendered applications. **R9 has started** (`docs/reviews/2026-11-25-r9-juice-shop.md`): on OWASP Juice Shop, a client-side application, the explorer first showed 24% of what the baselines need read-only and 0% acting; after fixing fragment routes, reading pages once drawn and stopping in time, 68% read-only and 74% with guarded pressing or acting, with our applications unchanged (ADR-0021 amendment). Guarded pressing's browser guard leaked once; the egress proxy now refuses writes too, on http (R-23, open for https). Everything else about the testing journey —
plans, requirements, a case library, suites, defects, reporting — **does not exist yet**: six of the
seven module projects are shells. `docs/roadmap.md` §Inventory is the authoritative list of what is built.

Read first: `docs/architecture.md`, `docs/data-architecture.md` (accepted design, ADR-0038), `docs/roadmap.md`, `docs/design-system.md`,
`docs/sitemap.md`, `docs/testing-journey.md`, `docs/decisions/`.

## Working agreement

- **Plan first.** Work milestone by milestone (`docs/roadmap.md`). **Stop after each milestone** and summarize for review. Don't start the next one without approval.
- **Branches:** work on `<type>/<slug>` cut from `develop`; never commit to `develop` or `main` directly. A change reaches `develop` through a pull request with green CI, and `develop` reaches `main` at a release. See `CONTRIBUTING.md`.
- **Ask before** adding a dependency, deviating from the architecture, using JavaScript, or using a non-.NET tool. Explain why.
- **Update docs when you change behavior or structure** — in the same commit. Record architectural decisions as ADRs in `docs/decisions/NNNN-short-title.md` (template: `0000-template.md`).
- Commit after each meaningful step; small commits with clear messages (imperative mood, e.g. `Add TKpiTile component with delta indicator`).
- Before saying a milestone is done: `dotnet build` (zero warnings) and `dotnet test` (incl. Playwright) must pass. Report failures honestly.

## Knowledge management and AI collaboration (binding, owner's requirement of 2026-11-24)

Autestiq is developed with several AI assistants: chiefly Claude Code for engineering, and ChatGPT for strategic
analysis, research, independent review and company knowledge management. **The GitHub repository is the
authoritative source of truth for engineering.** Another engineer or AI assistant must be able to understand the
project's current state from the repository alone, without any earlier conversation. So documentation is updated in
the same change as the implementation, and:

- **Significant architectural and technical decisions** go in an ADR (`docs/decisions/`), with the reasoning and the
  alternatives rejected, not only the choice.
- **The roadmap and its implementation inventory** (`docs/roadmap.md` §Inventory) match the code. **Implemented and
  planned are never mixed**: say which, every time.
- **Business requirements, founder decisions and product constraints** are recorded: in the ADR they shape (with "the
  owner approved it on …"), in `docs/business/`, or in `docs/secure-onboarding-and-privacy.md` for the binding rules.
- **Benchmark results** keep their methodology, limitations and known failures beside the figures: the report in
  `docs/ai-benchmark*.md` as the tool wrote it, and the write-up in ADR-0020.
- **The security and risk registers** (`docs/security-register.md`, `docs/secure-onboarding-gap-analysis.md`) stay
  accurate: a fixed defect is marked fixed, a new risk is added when it is found.
- **Unresolved questions and technical debt** go in `docs/open-questions.md`, and leave it when they are resolved.
- **Critical knowledge never lives only in a conversation.** If a decision, a finding or an owner's answer matters
  beyond this session, it is written into the repository before the session moves on.
- **Never commit** credentials, confidential customer information, or unredacted conversation logs.

## AI Training & Continuous Learning Strategy

Autestiq has a long-term strategic initiative to develop specialized AI intelligence for autonomous software Quality
Assurance. The authoritative documentation is maintained in [`docs/ai-learning/`](docs/ai-learning/README.md); the
decision is ADR-0036 (proposed, 2026-11-24). **Nothing in it is built yet unless its documents say so.**

When working on AI agents, synthetic testing environments, experience collection, model evaluation, fine-tuning, or
reinforcement learning:

1. Review `docs/ai-learning/README.md` and the relevant strategy documents.
2. Follow existing architectural, security, privacy, and development requirements.
3. Distinguish implemented functionality from research proposals.
4. Prioritize verified improvements over dataset size or model ownership.
5. Preserve existing benchmark methodology and held-out evaluations.
6. Update the relevant strategy documentation when new decisions are accepted or experiments are completed.
7. Do not start expensive training jobs or make major architectural changes without explicit owner approval.
8. Do not use confidential customer data for shared model training without explicit authorization.
9. Follow the approved product roadmap and its milestone review process.

The AI learning roadmap complements the main Autestiq product roadmap and does not automatically override existing
development priorities.

## Security, privacy and onboarding (binding, owner's requirements of 2026-11-17)

The full text is [`docs/secure-onboarding-and-privacy.md`](docs/secure-onboarding-and-privacy.md); its gap analysis
against the current system is [`docs/secure-onboarding-gap-analysis.md`](docs/secure-onboarding-gap-analysis.md).
These rules hold for every change. They are long-term requirements, **not** an instruction to build everything
now or to rewrite the roadmap; working features are preserved.

- **Customer trust comes before speed, feature count and AI autonomy.** Never silently weaken a security
  requirement to go faster. If a request conflicts with these rules, say so and propose a safer alternative.
- **No source code is required.** Source access stays optional, explicitly authorized and least-privilege. The
  product must be useful from a staging URL alone; specifications, requirements, a private agent and repositories
  only add to it, alone or combined.
- **Control plane and execution plane are separate.** Execution must be able to move to customer-controlled
  infrastructure (a private agent: outbound-authenticated, local secrets, local evidence by default, sends only
  what the customer's policy allows, revocable and audited). A tunnel to our cloud browser is **not** private
  execution, and private execution is **not** private AI processing: never claim either.
- **AI processing needs explicit permission and minimal data.** Never send confidential customer information to an
  external AI provider automatically. Never assume staging data is harmless. Treat as distinct: approved
  metadata; confidential application information; credentials (never to a model); personal or regulated data;
  execution evidence (screenshots, DOM, traces, logs, API responses).
- **Security is enforced by deterministic software, not by prompts.** Prompts are advisory (ADR-0019 §5).
  Allowlists, proofs, validators, proxies, quotas and permissions are what hold.
- **Production is off by default.** Testing a production environment needs the customer's explicit authorization
  and configuration. **State-changing or destructive test steps need explicit approval.**
- **Observed, documented and assumed are never mixed.** The AI keeps observed behaviour, documented expectations and
  its own assumptions apart; an inferred rule is never shown as a verified requirement, and every generated test
  traces to its evidence with its uncertainty stated.
- **Evidence is customer data:** retained and deleted by policy, stored and transmitted as configured.
- **Before any change touching application access, AI processing, testing, infrastructure or customer data:**
  examine the existing design; name the trust boundaries and risks; check it against these rules; take the
  simplest design that keeps the future open; write acceptance criteria **with negative and security tests**;
  test it; record the decision and its trade-offs (ADR); state what is deferred or temporarily limited.
- **No major architecture change or new infrastructure dependency without explaining why, and the owner's
  approval.**

## Product name

**Autestiq** is the company and the product. The brand is written **AUTESTIQ** in logo and wordmark
positions and *Autestiq* in prose; the tagline is *Autonomous Testing. Intelligent Quality.*, which
is a brand line and not a claim about what the software does today.

Never hard-code the name in UI text: use the `Brand:ProductName` configuration value through
`BrandOptions.ProductName`, and the `Brand_Tagline` resource for the tagline. That rule is why the
rename from the earlier working name "Testo" cost one line in the UI — it is worth keeping.

Project and namespace prefix is `Autestiq.`. The `T` prefix on design-system components (`TButton`,
`TDataTable`) and the `t-` CSS classes are the design system's, not the brand's, and they stay.

## Architecture rules (enforced by `DependencyRulesTests`)

- `Autestiq.Contracts`: DTOs + service interfaces only. **No UI dependencies.** The backend will reuse it.
- `Autestiq.Platform`: module/capability contracts and registries (`IFeatureModule`, `ITestCapability`, `INavigationRegistry`, `IDashboardWidgetRegistry`, `ICapabilityRegistry`, `Routes`).
- `Autestiq.UI.Shared`: design system, layouts, tokens. No business logic, no module references.
- `Autestiq.Modules.*` and `Autestiq.Capabilities.*` reference only `Contracts`, `Platform`, `UI.Shared`. **Never another module or capability.** Cross-module links go through `Autestiq.Platform.Routes` constants. (`IAppEventBus`, for cross-module events, is designed in `docs/architecture.md` §6 but **not built**.)
- `Autestiq.Website` (public pages) references only `Contracts`, `Platform`, `UI.Shared`.
- `Autestiq.Mocks` references only `Contracts`.
- `Autestiq.Domain`: entities and the rules that hold regardless of storage. References only `Contracts`. **No EF Core, no ASP.NET Core Identity, no UI.**
- `Autestiq.Persistence`: EF Core on PostgreSQL, the migrations, and the Identity stores. **The only project that may see ASP.NET Core Identity**, and only through `CredentialService`.
- `Autestiq.Application`: the use cases; implements the `Contracts` interfaces. References `Domain`, `Persistence`, `Contracts`. A failed use case returns **resource keys**, never English sentences.
- Everything inside the tenant boundary implements `IBelongsToOrganization`. Never write a `WHERE organization_id = …` by hand; the query filter does it. A query that must cross the boundary says `IgnoreQueryFilters()` with a comment saying why.
- Account pages (`/account/**`) are **static SSR with plain form posts** (ADR-0016): a cookie can only be issued during an HTTP request. They carry `[ExcludeFromInteractiveRouting]` and `[HasForm]`.
- `Autestiq.Web` is the composition root only: DI, routing, `App.razor`, dashboard host page. It is the **only** project that references the ASP.NET Core server framework; libraries never use `HttpContext` or other server-only types.
- A new project needs a rule in `DependencyRulesTests` and a row in `docs/architecture.md` §4.
- Each module exposes **one** public `IFeatureModule` class named `<Name>Module`, with a lowercase id and the flag `Module.<Name>` (listed in `appsettings.json`); everything else is `internal` where possible. The host registers it with one line: `builder.Services.AddAutestiqModule<NameModule>()`.
- **Prices live in the price book, never in code** (ADR-0029). A cost is a stored row at the price in force when its run finished; a price change is a new version from `set-prices`, effective now or later. The planner gets its token prices from the book and does not plan without them.
- Nav items, dashboard widgets and capabilities are **registered**, never hard-coded in the shell.
- All data access via interfaces from `Contracts`. Components never `new` a service or touch mock data directly.
- Service methods: async, `CancellationToken` last parameter, return Contracts DTOs (`record`s), lists via `PagedResult<T>`.
- Every unfinished module/capability sits behind a feature flag (`Module.X`, `Capability.X`, `Feature.X`).
- **Exported tests come from fixed templates, never from a model** (ADR-0023). `Autestiq.Export` references `Contracts` only; a plan's strings appear in exported code only as escaped literals or line-safe comments. A language is offered only after `--export-check <language>` gives the plan's verdict on every run of every benchmark set. Python is built; the language is a per-project setting once projects exist.
- **Locators are chosen by the software, never by the model** (ADR-0024). Without a test id, the explorer tries role and accessible name, then exact text, then each narrowed to a container cell or line, and keeps the first the browser finds **exactly one** element for, through `Targets.Locate`. Never describe an element as reachable on a guess; a change that lowers the share reached shows up in `--without-test-ids --explore`.

## Render modes

- Public website (`Autestiq.Website`): **static SSR, without the Blazor script**. No `@rendermode`, no event handlers (`@onclick` does nothing there), no enhanced navigation. Use links and plain forms.
- A page is public when it has `[ExcludeFromInteractiveRouting]`; `Autestiq.Website/Pages/_Imports.razor` applies it (and `SiteLayout`) to every page in that folder. Everything else is an app page.
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
Autestiq.Modules.Defects/
  DefectsModule.cs            // IFeatureModule: services, nav, widgets
  Pages/                      // routable components (@page), one folder per area if large
  Components/                 // module-specific components
  ViewModels/                 // presentation models + mapping from DTOs
  Widgets/                    // dashboard widgets
  Resources/                  // *.resx (en) + *.de.resx
  _Imports.razor
```

## Naming

- Namespaces = folder path (`Autestiq.Modules.Defects.Pages`). File-scoped namespaces.
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
- A rule in this file or in `docs/` that can be checked by a test should be (see `tests/Autestiq.UI.Tests/Architecture`, `Design`, `Localization`).

## Commands

Run these from the repository root (the folder with `Autestiq.slnx`); from inside a project
folder `dotnet test` reports "No test projects were found".

```bash
dotnet build
dotnet test --max-parallel-test-modules 1      # all tests, including the browser tests
dotnet test --project tests/Autestiq.UI.Tests     # one test project
dotnet test --project tests/Autestiq.E2E --filter-class "Autestiq.E2E.Pages.ThemeTests"
dotnet run --project src/Autestiq.Web             # http://localhost:5080
dotnet run --project tools/Autestiq.Operator -- migrate   # schema for a deployed database (ADR-0039)
```

The product needs PostgreSQL. `docker run -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16`,
or point `ConnectionStrings__Autestiq` elsewhere. The data and browser tests create a database of
their own per class and find the server through `AUTESTIQ_TEST_DB`.

- The sealed-run tests (ADR-0026) need Docker and the worker image, built once after a change to the worker:
  `dotnet publish src/Autestiq.Worker -c Release -o artifacts/worker -p:UseAppHost=false`, then
  `docker build -f src/Autestiq.Worker/Dockerfile -t autestiq-worker:dev artifacts/worker`. Without them those tests fail,
  by design: a gate that is skipped is not a gate.
- Run the whole suite with `--max-parallel-test-modules 1`, as CI does. Plain `dotnet test` runs the test projects
  side by side, and the sealed-run tests then create Docker networks while the browser tests run: Chromium
  takes each new host interface for a network change and fails requests in flight with `ERR_NETWORK_CHANGED`.
  Chromium has no documented switch to ignore it. Tests inside one project still run in parallel.
- Tests run on Microsoft.Testing.Platform (xUnit v3): filters are `--filter-class`, `--filter-method`, `--filter-namespace`; reports are `--report-xunit-trx` and `--report-xunit-html`.
- The browser tests start the application themselves; nothing needs to run beforehand.
- Browser for the tests, once per machine after a build: `pwsh tests/Autestiq.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`. Or point `E2E_BROWSER_PATH` at an existing Chromium executable.
