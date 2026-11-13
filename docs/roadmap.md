# Frontend Roadmap

> Status: **Accepted** (Step 0 approved on 2026-11-11).
> Each milestone ends in a demo-able state. I stop after each one for your review.

## Definition of Done (applies to every milestone)

- `dotnet build` with **zero warnings** (warnings are errors).
- All tests green: `dotnet test` runs the unit, component, rule and Playwright tests together.
- New reusable components have bUnit tests; new pages have a Playwright smoke test with an axe scan (0 serious/critical violations).
- Every new UI string exists in **English and German**.
- Light and dark theme both checked.
- Docs updated (`architecture.md`, `sitemap.md`, `design-system.md`, ADRs) where behavior or structure changed.
- Work committed in small, clearly described commits and pushed.

---

## Milestone 0 — Planning documents *(this step)*
**Done when:** `docs/testing-journey.md`, `architecture.md`, `sitemap.md`, `design-system.md`, `roadmap.md`, initial ADRs and `CLAUDE.md` are reviewed and approved, and the open questions are answered (name, palette, URL scheme, JS enhancement).

**Status: done (2026-11-11).** All six decisions followed the recommendations: working name "Testo", palette A, the small header script, culture-prefixed public URLs, Interactive Server for the app, and the adjusted solution structure.

## Milestone 1 — Foundation
**Scope**
- Solution + all projects from `architecture.md` §3 (modules as empty shells), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json` (pin .NET 10 SDK).
- Design tokens file (`tokens.css`) with the chosen palette, light + dark themes, self-hosted Inter + JetBrains Mono.
- Base public layout and base app layout (empty shells), skip link, landmarks.
- Theme switch (light / dark / system) persisted in a cookie, no flash on load.
- Localization (en/de) with culture switch; resx parity test.
- `Testo.Platform` contracts: `IFeatureModule`, registries, `ITestCapability` (no implementations yet).
- Feature management wired up.
- Test projects: `Testo.UI.Tests` (bUnit), `Testo.Mocks.Tests`, `Testo.E2E` (Playwright) with one smoke test each.
- Architecture dependency-rules test.
- GitHub Actions CI: restore, build, unit tests, Playwright (headless Chromium), test result upload.

**Done when:** `dotnet build` and `dotnet test` pass locally and in CI; the app starts and shows a placeholder home page and an empty app shell; theme and language switches work and persist; an ADR records the pinned Fluent UI version.

**Status: built (2026-11-12), waiting for review.** What it delivered beyond the list above:

- Public pages ship no Blazor script and set no cookie on a plain visit (ADR-0009); a public page is about 85 KB in total.
- Security headers with a content security policy on every response.
- Guard tests for the dependency rules, the module conventions, the page routes, the resource files, the token contrast and "component styles use tokens".
- Friendly not-found and error pages in both languages.
- Fluent UI Blazor **5.0.1** is pinned (ADR-0007), and a first part of the token mapping already puts its components on our brand color.
- 440 tests: 8 for the mocks, 345 component and rule tests, 87 browser and HTTP tests.

Decisions recorded: ADR-0007 (Fluent UI 5), ADR-0008 (theme tokens), ADR-0009 (public pages), ADR-0010 (test tooling).

## Milestone 2 — Design system
**Scope:** the components in `design-system.md` §8: buttons, cards, inputs, badges, tabs, mega menu, header, footer, data table wrapper, KPI tile, timeline, states (skeleton/empty/error), dialog/drawer/menu/toast wrappers, simple SVG charts. A `/showcase` page (dev/staging only, feature-flagged) shows every component with all variants and states in both themes.

**Done when:** every component has bUnit tests (rendering, parameters, events, ARIA, keyboard); the showcase page passes an axe scan in light and dark, at mobile and desktop width; Fluent components visibly use our brand tokens (Milestone 1 mapped brand color, font, radius and focus; the rest of the mapping is done here).

Already built in Milestone 1 and extended here: `TLinkButton`, the theme and language switch (`TChoiceForm`), the skip link and the brand mark.

## Milestone 3 — Identity and data foundation

**Added after Milestone 1**, when login and a database came into scope. Planned in
[`domain-model.md`](domain-model.md) and [`identity-and-access.md`](identity-and-access.md);
decisions in ADR-0011 to ADR-0014.

**Scope**
- `Testo.Domain`, `Testo.Application`, `Testo.Persistence`; EF Core on PostgreSQL, migrations, a seeded development database
- Organizations, users, memberships, roles, invitations, the audit log
- Tenant isolation: the global query filter, assignment on write, the indexes — and the two-organization leak test
- Sign in, sign out, password reset, optional TOTP; the session cookie and its protections
- Invite, accept, change role, remove a person, with sessions ended immediately
- `AddTestoApplication()` replacing `AddTestoMocks()` in the host; mocks kept for tests and a demo mode
- CI runs the tests against a real PostgreSQL in a container

**Done when:** a person can be invited, accept, sign in and see only their organization's data; the permission table in `identity-and-access.md` §2 is asserted cell by cell by a test; the tenant leak test passes across every entity; every access change appears in the audit log; the general Definition of Done is met.

## Milestone 4 — Public website
**Scope:** all public pages from `sitemap.md` §A with real copy (EN + DE) where we have it and labeled placeholders elsewhere; mega-menu navigation, language switcher, rich footer; contact + demo forms (UI and validation only); Imprint, Privacy, Terms, Accessibility statement placeholders; original SVG illustrations; `sitemap.xml`, `robots.txt`, meta/OpenGraph tags, `hreflang`.

**Done when:** every public page has a Playwright smoke test (title, `h1`, no console errors, axe pass); the pages render in static SSR (no SignalR circuit opened — asserted in E2E); every page with a form carries `[HasForm]` and a test finds pages that forgot it (ADR-0009); Lighthouse (manual run, documented in the PR) shows Performance ≥ 90 and Accessibility ≥ 95 on Home and Pricing (mobile profile).

## Milestone 5 — App shell + module registry
**Scope:** the app shell on the real sign-in from Milestone 3; app shell (registry-driven left nav, top bar with project switcher, search, notifications, user menu); `Testo.Mocks` with the seed data generator and all `Mock*` services (with configurable latency/failure); dashboard rendering registered widgets: quality KPIs, run trends, open defects by severity, release readiness, recent runs; feature flags hiding unfinished modules; "module not available" page.

**Done when:** each module registers its nav entry with one line and appears in the nav; turning a flag off hides its nav entry, widgets and routes (tested); dashboard widgets show loading, empty, error and data states (bUnit); seed-data consistency tests pass; E2E covers sign-in → dashboard → switch project.

## Milestone 6 — Testing-journey modules
Delivered **one stage at a time**, each as its own reviewable sub-milestone:

| # | Module | Key screens |
|---|---|---|
| 6.1 | Planning | Test plan list/detail, risk register + heat map, entry/exit criteria |
| 6.2 | Analysis | Requirements list/detail, AI testability review (mock), **traceability matrix** with gaps |
| 6.3 | Design | Test case library, test case editor, AI test case generation flow (mock suggestions) |
| 6.4 | Execution | Run history, start-a-run (capability-driven), suites, environments, manual runner, test health |
| 6.5 | Defects | List + kanban, detail with lifecycle and links, "create from failed step" |
| 6.6 | Reporting | Metrics, release readiness + go/no-go, test summary report, closure |
| 6.7 | Settings | Integrations placeholders, team members, capability settings, profile, feature-flag panel |

**Done when (per module):** pages from `sitemap.md` exist with realistic mock data; bUnit tests for module components; Playwright smoke test per page; module only references `Contracts`, `Platform`, `UI.Shared` (dependency test passes); EN + DE complete.

## Milestone 7 — Live E2E Test Run view (showpiece)
**Scope:** `Testo.Capabilities.E2EWeb` implementing `ITestCapability` (settings, run setup, live view, results); `MockTestRunLiveFeed` with scripted scenarios (passing run, failure at step 7, self-healed locator); live view with step timeline, highlighted current step, per-step status, screenshot panel (mock screenshots as generated SVGs of a fake web shop), log console with level filters and auto-scroll lock, elapsed time, stop / rerun; polite screen-reader announcements; reconnect/gap handling via `Sequence`.

**Done when:** adding the capability required only the new project + one registration line (verified in the PR diff); reducer and component tests run with a fake `TimeProvider` (no real waiting); E2E watches a mock run to completion and asserts the final status; stop and rerun work; reduced-motion mode verified.

## Milestone 8 — Polish
**Scope:** responsive pass (mobile, tablet, desktop, wide) on every page; full accessibility pass (keyboard-only walkthrough of main flows, screen-reader spot checks with NVDA/VoiceOver, axe everywhere); dark mode review; E2E tests for main flows (public → request demo; sign in → dashboard → requirement → traceability → start E2E run → watch live → create defect from failed step → release readiness); performance check of public pages; all docs updated; a short "how to replace mocks with the real backend" guide in `docs/`.

**Done when:** all main-flow E2E tests pass in CI at desktop and mobile viewports in both themes; zero serious/critical axe violations; docs reflect the final state.

---

## Later (out of scope for the frontend phase)
- `Testo.Api`: the REST host for CI integrations, over the same use cases (ADR-0014).
- SignalR for the live run feed, replacing the simulated one.
- Per-organization single sign-on (OpenID Connect), behind the interfaces of ADR-0013.
- More capabilities: API, performance, security, accessibility testing.
- `InteractiveAuto` for the app area if needed.
