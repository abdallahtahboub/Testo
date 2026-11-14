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

**Status: built (2026-11-13), waiting for review.** 35 components, 702 tests. The Fluent UI mapping is complete, so its data grid, dialogs and menus take every colour, font, radius and shadow from our tokens in both themes.

The mega menu, site header and footer **move to Milestone 4**, with the public website they belong to: building a mega menu before the pages it points at exists would be guesswork.

Three faults were found by opening the showcase in a browser rather than by a test, and are worth remembering:

- A class cannot cross a component boundary. CSS isolation scopes every selector to the component that declares it, so styles shared by several components now live in `wwwroot/css/controls.css`.
- A class passed *to* a child component does not reach it either; wrap the child in an element the parent owns.
- A sample of the type scale must not use real heading elements, or it joins the page outline. Type utility classes (`.t-h1`…`.t-h4`) exist for appearance without semantics.

Decision recorded: ADR-0015 (our own icon set rather than an icon package).

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

**Status: built (2026-11-13), waiting for review.** 807 tests, 105 of them new. The headline
works end to end in a browser: invited, accepted, signed in, and seeing only one organization.

What it delivered beyond the list above:

- Tenant isolation is refused in four places, not three. The query filter, the save guard and
  `organization_id` first in every index were planned; an alternate key of
  `(organization_id, id)` on organizations and projects means Entity Framework will not even
  let the column be changed, and lets the entities of Milestone 5 carry a composite foreign key
  the database itself checks.
- The leak test seeds two organizations with a row of every kind and asks each whether it can
  see the other's. A second test fails if an entity inside the boundary has no seed, so a new
  one cannot quietly go untested.
- The permission test transcribes the table from the document independently of the code, so the
  two cannot drift apart. Both guards were checked by breaking them on purpose.
- Removing somebody ends their sessions at the next request, not at the next expiry, and leaves
  their other organizations alone. A password reset ends every session everywhere.
- A seeded development database, so the application can be signed into the first time it runs.

Three things are deliberately not built, and `identity-and-access.md` §7 says why: sending email,
enrolling an authenticator, and the full breached-password check.

Decisions recorded: ADR-0016 (the account pages are static). ADR-0011 to ADR-0014 move from
proposed to built.

Found by opening it in a browser rather than by a test, and worth remembering:

- An `@layout` in an `_Imports.razor` applies to every component in that folder, including a
  layout kept there — which then nests inside itself without end. The page never finishes
  rendering, and the symptom is a timeout, not an error. Layouts live in `Components/Layout`.
- `EditForm` renders its own antiforgery token in static rendering. A second one posts two
  values and the server rejects the pair with 400.
- A test that clicks `button[type=submit]` clicks the language switch, because the layout has
  four submit buttons of its own.

---

## Inventory — what is actually built

> Last verified on 2026-11-14, after Milestone 3.3: `dotnet build` clean, every test green in CI.
> This table is authoritative. Where another document disagrees with it, this one is right.

| Area | State |
|---|---|
| Identity, organizations, memberships, invitations, roles, audit log | **Real**, on PostgreSQL |
| Tenant isolation: query filter, save guard, indexes, alternate key | **Real.** D-2, the hole in it, was closed in Milestone 3.1 |
| Sign in, sign out, password reset, one-time code, accept invitation | **Real** |
| Design system, 35 components | **Real** |
| Public website | **One page** (home, EN + DE) plus not-found and error |
| App area | **One placeholder page** at `/app`, plus the two execution pages. Signing in is required everywhere (D-1 closed in Milestone 3.1) |
| Test runs: start, stop, rerun, steps, screenshots | **Real** for three scripted scenarios against our own demo target. No AI, no customer targets |
| Browser execution: worker out of process, egress proxy, signed artifact links | **Real.** Not yet a container per run (R-1) |
| The other six feature modules | **Shells.** One class each: an id and a feature flag. No pages, no services |
| `Testo.Capabilities.E2EWeb` | **Empty.** An `_Imports.razor` |
| `Testo.Mocks` | **Shell.** Options and a random source. No services, no seed data |
| Plans, requirements, test cases, suites, environments, defects, reporting | **Do not exist**, in any form, not even mocked |
| Email delivery | **Deferred.** Invitation and reset links go to the log, and the application refuses to start that way outside development (D-3 closed in Milestone 3.1) |
| Authenticator enrollment | **Deferred.** Signing in with a code works; setting one up does not |
| Breached-password check | **Partial.** An embedded list plus runs and repeats; no online check |

---

## Order of work

Changed on 2026-11-13 by ADR-0017, with the owner's approval. The milestones below keep their
numbers; only the order changed. New work is inserted as 3.1 to 3.4 rather than renumbering,
because renumbering is what put the issue tracker a number behind this document.

**3.1 → 3.2 → 3.3 → 3.4 → 4 → 5 → 6 → 7 → 8**

### Milestone 3.1 — Milestone 3 debt
The confirmed defects in [`security-register.md`](security-register.md): the authorization gate
that was never fitted, tokens in logs, no rate limiting, and the tenant guard's trust in a pre-set
organization. Not new work — Milestone 3 finished properly.

**Done when:** every defect D-1 to D-4 is closed with a test that fails without the fix; the
register records the date and the pull request.

**Status: built (2026-11-13), waiting for review.** 860 tests, 53 of them new. Each fix was
checked by breaking it again: removing the page attribute, commenting out the rate limiter,
writing into another organization. Each failed, naming what was wrong.

Two things found along the way:

- A fallback authorization policy, the obvious way to be secure by default, does not fit a Blazor
  Web App: it challenges the catch-all endpoint that serves unknown URLs, so a public 404 becomes
  a redirect to sign-in. Pages declare the attribute instead, and a test refuses any page that
  declares neither.
- The browser harness read lists that Playwright appends to from its own threads, which failed a
  test with "collection was modified". It looked like a product fault and was a harness fault.

Making sign-in compulsory also made it necessary to be able to leave: the top bar now says who is
signed in and how to sign out. The user menu proper belongs to Milestone 5.

### Milestone 3.2 — Execution isolation threat model and ADR
Written before any code opens a browser. Covers SSRF including DNS rebinding and redirect chains,
internal network reach, prompt injection from hostile pages, downloads, secrets, and model cost.
Decides the worker boundary, the egress allowlist and the artifact storage model.

**Done when:** an accepted ADR exists and R-1 to R-3 in the register point at it.

**Status: built (2026-11-13), waiting for review.** [`execution-threat-model.md`](execution-threat-model.md)
and [ADR-0018](decisions/0018-execution-isolation.md); R-1 to R-3 point at them. No code. Two
things it decides that the review did not ask for: a target must be *proved* before an organization
may run against it, or the product is a free way to send a browser at somebody else's website from
our address; and the worker cannot name an organization at all, so a worker a hostile page fully
owns learns one run's configuration and nothing about any other customer. Milestone 3.3 inherits
seven refusals as acceptance tests.

### Milestone 3.3 — A deterministic execution slice
One allowlisted local demo target. One happy path and one known failure. Real `TestRun` and step
persistence, artifacts, cancel and rerun. A worker outside the Blazor process. **No AI.**

**Done when:** an authorized person can start, cancel and rerun a real Playwright run; a failure
carries replayable steps, a screenshot and timestamps; cross-tenant access is refused in adversarial
tests; it all passes in CI.

**Status: built (2026-11-14), waiting for review.** What is real: `Testo.DemoShop`, a small shop
with a deliberate bug in its checkout; `Testo.Worker`, a separate process that drives Chromium
through an egress proxy of its own and prints a result; `test_runs`, `test_run_steps` and
`artifacts` on PostgreSQL inside the tenant boundary; `RunDispatcher`, which claims a queued run,
launches the worker and writes back under `TenantWriteScope`; and two pages, `/app/runs` and
`/app/runs/{id}`.

Four things worth knowing:

- **Every way a run can stop has its own name.** Passed, failed, stopped, out of time, over
  budget, could not run. A run that hit its budget is not a failing application, and a run the
  infrastructure lost says nothing about the application at all; one word for all of them would
  make the failure rate a lie. The badge shows out-of-time and could-not-run as warnings, not
  failures.
- **The worker is never told an organization.** It is given a run id, a target, a budget and a
  directory. A worker a hostile page fully owns therefore learns one run's configuration and
  nothing about any other customer. The tenant is re-established on the way back in, from the
  stored row (ADR-0018).
- **The proxy judges resolved addresses, not URLs**, on every connection including each redirect
  hop, which is what closes DNS rebinding. A test drives a redirect to an *allowed* hostname that
  resolves to loopback and watches it be refused — the case a hostname allowlist cannot catch.
- **A wall-clock budget test caught a real race**, not a test problem: the deadline was only
  checked while waiting, so a fast run could pass its deadline unnoticed. The runner now checks it
  at every step boundary.

Not built, and deliberately: the container per run (the worker is a process, which is the right
shape but not an isolation boundary — R-1 says so plainly), object storage, retention, and a reaper
for runs left behind by a host that died. Nothing customer-supplied runs against anything yet: the
only reachable target is ours.

### Milestone 3.4 — Bounded AI, measured
A planner behind a provider-agnostic interface. Schema-validated plans, a fixed browser-action
vocabulary, hostile pages treated as untrusted data, never model-generated code. Benchmarked on
10 to 20 controlled scenarios against the deterministic baseline.

**Done when:** pass rate, false positives, flake rate, time and cost per run are measured and
published next to the baseline. No claim of autonomy without those numbers.

---
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
