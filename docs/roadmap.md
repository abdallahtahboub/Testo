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

**Status: done (2026-11-11).** All six decisions followed the recommendations: working name "Testo" *(historical — the company and product became **Autestiq** on 2026-11-14)*, palette A *(replaced by "Navy & Electric Cyan" in the same rename, ADR-0005)*, the small header script, culture-prefixed public URLs, Interactive Server for the app, and the adjusted solution structure.

## Milestone 1 — Foundation
**Scope**
- Solution + all projects from `architecture.md` §3 (modules as empty shells), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json` (pin .NET 10 SDK).
- Design tokens file (`tokens.css`) with the chosen palette, light + dark themes, self-hosted Inter + JetBrains Mono.
- Base public layout and base app layout (empty shells), skip link, landmarks.
- Theme switch (light / dark / system) persisted in a cookie, no flash on load.
- Localization (en/de) with culture switch; resx parity test.
- `Autestiq.Platform` contracts: `IFeatureModule`, registries, `ITestCapability` (no implementations yet).
- Feature management wired up.
- Test projects: `Autestiq.UI.Tests` (bUnit), `Autestiq.Mocks.Tests`, `Autestiq.E2E` (Playwright) with one smoke test each.
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
- `Autestiq.Domain`, `Autestiq.Application`, `Autestiq.Persistence`; EF Core on PostgreSQL, migrations, a seeded development database
- Organizations, users, memberships, roles, invitations, the audit log
- Tenant isolation: the global query filter, assignment on write, the indexes — and the two-organization leak test
- Sign in, sign out, password reset, optional TOTP; the session cookie and its protections
- Invite, accept, change role, remove a person, with sessions ended immediately
- `AddAutestiqApplication()` replacing `AddAutestiqMocks()` in the host; mocks kept for tests and a demo mode
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

> Last verified on 2026-11-17, after evidence retention (3.8 part 2, ADR-0031): `dotnet build` clean, every test green
> locally. Rows checked against the code again on 2026-11-24 by the pilot-readiness review
> ([`reviews/2026-11-24-pilot-readiness.md`](reviews/2026-11-24-pilot-readiness.md)).
> This table is authoritative. Where another document disagrees with it, this one is right.

| Area | State |
|---|---|
| Identity, organizations, memberships, invitations, roles, audit log | **Real**, on PostgreSQL |
| Tenant isolation: query filter, save guard, indexes, alternate key | **Real.** D-2, the hole in it, was closed in Milestone 3.1 |
| Sign in, sign out, password reset, one-time code, accept invitation | **Real** |
| Design system, 35 components | **Real** |
| Public website | **Real.** 29 routes in English and German, with every placeholder page labeled as one |
| App area | **Navigation drawn from the module registry** (Milestone 5). Real pages: projects, environments with ownership proofs (Milestone 5 part 1, ADR-0030), tests, their plans, approval, runs and downloads (parts 2 to 4), project settings with the screenshot policy, and the two run pages; `/app` is still a placeholder. Signing in is required everywhere (D-1 closed in Milestone 3.1) |
| Test runs: start, stop, rerun, steps, screenshots | **Real.** Scripted scenarios against our own targets, and approved plans against a project's environments (Milestone 5) |
| Customer-site safety (3.5) | **Real.** Proof of site ownership, kill switch, stuck-run reaping, loopback from configuration only, a sealed container per run (ADR-0026). Customer origins are refused outside container mode |
| Usage and quotas (3.6) | **Real.** Append-only usage ledger, per-organization concurrency and monthly minutes with reservation, rate limits on starts (ADR-0027). No screen shows usage yet |
| Plan catalog (3.7) | **Real.** Versioned plans in the database, organizations on a version they keep until an operator moves them, limits read from that version (ADR-0028). No prices; an operator command changes plans; no screen |
| Cost per run (3.8, part 1) | **Real.** A price book of versions that never change, cost rows per run at the price in force when it finished, a run's total, an operator cost report (ADR-0029). Machine and storage prices are **estimates** until hosting is chosen |
| Exploration that may act (ADR-0032) | **Real.** Per test environment, off by default, Owner only with an acknowledgement, never production, audited; the dispatcher passes it to the worker only while it stands; the test page lists what exploring did |
| Evidence retention (3.8, part 2) | **Real.** A screenshot policy per project (90, 30 or 7 days, or none), an hourly job that deletes expired screenshots, files then rows, audited; results and steps kept (ADR-0031). The store is still a local directory: object storage waits for hosting |
| Exporting tests (ADR-0023) | **Built; in the product.** Python, verified 107 of 107. A plan's Python is previewed on its test page and downloaded once approved, with a fixed header saying what was approved (Milestone 5 part 4). A plan whose pass would rest on the test account's own value is not downloadable, and the page says why (Milestone 5.1 R4) |
| Described tests and their plans (Milestone 5 part 2) | **Real.** A per-project AI permission, off by default, enforced in the service and checked again before each call; a test described in a sentence; the site explored as a run (read-only on a customer's site); the plan written by the measured planner and validator, with its cost at the price book's prices, within $10 a month for the product and $2 per organization; what was sent recorded and shown; the AI's notes labelled unverified (ADR-0030); expected results stated by the tester, up to five, and every check labelled by software as observed, stated or assumed (ADR-0034). Needs `Planner__ApiKey` on the server. Read-only exploration is not yet measured |
| Approving and running a plan (Milestone 5 part 3) | **Real.** Steps that may change data decided by software and marked; approval by Owner or QA lead with the plan's fingerprint, an explicit tick for a plan that may change data, refused for partial plans and for plans typing a secret the environment lacks; runs refused unless approved, checked again by the dispatcher; read-only runs held by a guard in the browser (ADR-0030) |
| Test-account secrets (ADR-0033) | **Real.** One test account per test environment, set by Owner or QA lead, write-only, audited by name, never production; encrypted with Data Protection under a per-environment purpose, key ring in `DataProtection:KeyDirectory` (required outside development); names only to the planner; values to approved runs, and to explorations only with ADR-0032's permission, where the explorer signs in and the values are removed from what it describes. An interim until a private agent. On eShopOnWeb signed in: 18 of 22, no false positive; checkout reached, paying never pressed, so placing an order stays inconclusive. A run that passes only by finding the account's own value ends inconclusive. With the tester's stated thank-you (ADR-0034), placing an order is correct; with ambiguous steps refused by the validator (ADR-0024): 18 of 22, no false positive. **The clean measurement, Spring PetClinic (scenarios written from its source before any run, nothing tuned): 20 of 22 correct (91%), no false positive** (ADR-0020) |
| AI planning: interface, vocabulary, validator, provider adapter, benchmark | **Built; the ADR-0020 gate's figures are met on our applications, with and without test ids.** On the unseen set (20 scenarios written after the prompt was frozen, 2026-11-17): 95% correct on sound applications, every defect caught where a verdict was reached, no false positive, including three defects no planner had seen; the same with every test id removed. **Explored read-only, as a customer's site is: 36 of 66 runs correct (full: 62 of 66), no false positive**; flows whose result appears only after an action are lost. **On eShopOnWeb, not built by us: 14 of 22 runs correct, then 18 of 22 (82%) after explorer changes made on its own results, no false positive either time**; pages behind sign-in remain, so the gate is not met on an application we did not build, and the 82% is not a clean measurement. Remaining misses are inconclusive: values generated at run time and states the crawl never saw. **Re-measured 2026-11-24 with the current prompt (ADR-0035 step 2):** unseen sets 64 of 66 with and without test ids, no false positive; Spring PetClinic (clean) 12 of 22 correct, 10 inconclusive naming real gaps, no false positive; read-only and eShopOnWeb not re-measured. The product calls it from Milestone 5 part 2, only with the project's permission |
| Browser execution: worker out of process, egress proxy, signed artifact links | **Real.** A sealed container per run in container mode (ADR-0026); process mode for development and tests |
| The other six feature modules | **Shells.** One class each: an id and a feature flag. No pages, no services |
| `Autestiq.Capabilities.E2EWeb` | **Empty.** An `_Imports.razor` |
| `Autestiq.Mocks` | **Shell.** Options and a random source. No services, no seed data |
| Plans, requirements, suites, defects, reporting | **Do not exist**, in any form, not even mocked. Environments and AI-described tests exist (Milestone 5); a test case library does not |
| Planning attempts and recovery (Milestone 5.1 R1) | **Real.** Each planning call an append-only attempt written before the model is asked, counted at its ceiling until answered; budgets sum attempts, re-plans included; a 15-minute lease fails a stuck test visibly, never re-planning it; one planning step per deployment by PostgreSQL advisory lock (ADR-0037) |
| Email delivery | **Deferred.** Invitation and reset links go to the log, and the application refuses to start that way outside development (D-3 closed in Milestone 3.1). **So no non-development deployment can start today** (Milestone 5.1, R2) |
| Deployment and release | **None.** No deployment, no migration command outside development (migrations run only when the development seed runs), no health checks, no release or tag: `develop` is 184 commits ahead of `main` (Milestone 5.1, R2, R3, R7) |
| Authenticator enrollment | **Deferred.** Signing in with a code works; setting one up does not |
| Breached-password check | **Partial.** An embedded list plus runs and repeats; no online check |

---

## Order of work

Changed on 2026-11-13 by ADR-0017, with the owner's approval. The milestones below keep their
numbers; only the order changed. New work is inserted as 3.1 to 3.4 rather than renumbering,
because renumbering is what put the issue tracker a number behind this document.

**Accepted by the owner (2026-11-15):** milestones 3.5 (customer-target safety), 3.6 (usage ledger
and quotas), 3.7 (plan catalog) and 3.8 (cost and evidence retention) sit between 3.4 and Milestone 4.
Milestone 4 is already built and awaits review, so it stays where it is in the list above. The pilot
gate and the billing sandbox (Phase D, Stripe Billing in test mode) are not milestones; they are gates
that come after 3.8. Scope is in [`commercial-readiness.md`](commercial-readiness.md) §6.

**Order, updated:** 3.1 → 3.2 → 3.3 → 3.4 → 3.5 → 3.6 → 3.7 → 3.8 → 3.9 → 4 → 5 → 6 → 7 → 8.

**Product scope (ADR-0020, accepted 2026-11-15):** the first product is AI-generated E2E UI tests.
**Milestone 3.9 — validation gate:** re-measure the planner on an expanded benchmark, using a developer
machine with a model key, and publish the result beside the baseline. The thresholds are in ADR-0020,
confirmed by the owner on 2026-11-15. Milestone 5 is narrowed to projects, environments,
generated test cases and suites, run history and reports. The other modules wait until after the
first pilot. The business model is in [`business/BUSINESS_MODEL.md`](business/BUSINESS_MODEL.md).

*(Restored on 2026-11-16: the merge of the rebrand into this branch took the rebrand's copy of this file
and dropped this section for a day.)*

**3.1 → 3.2 → 3.3 → 3.4 → 4 → 5 → 6 → 7 → 8** *(the order before 2026-11-15, kept for the record)*

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

**Status: built (2026-11-14), waiting for review.** What is real: `Autestiq.DemoShop`, a small shop
with a deliberate bug in its checkout; `Autestiq.Worker`, a separate process that drives Chromium
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

**Status: measured (2026-11-14). The planner is not usable, and that is the finding.**

A run against `claude-sonnet-5-5`, twelve scenarios, six of them with a real defect switched on and
off. Read carefully, because the obvious reading is wrong:

| | |
|---|---|
| Plans that passed a **sound** shop | **5 of 12** |
| Plans that failed a sound shop (false negatives) | **7 of 12** |
| Defects **genuinely caught** | **1 of 6** |
| Defects **missed** — passed a broken shop | **1 of 6** (`empty-basket-cannot-be-ordered`) |
| Defects where nothing can be concluded | **4 of 6** |

The four unknowable ones are the important part. For `checkout-matches-basket`, `quantity-counts`,
`discount-applied-once` and `remove-takes-out-the-right-line`, the planner's plan failed the broken
shop — and also failed the sound one. A plan that always fails is right about a broken application
for the same reason a stopped clock is right twice a day. Counting those as correct verdicts would
have reported **"five of six defects caught"** instead of one, which is exactly the flattering number
this benchmark exists to prevent. The harness now classifies them as *detected nothing* rather than
correct.

So the honest summary: against a working application the planner writes a plan that runs correctly
**five times out of twelve**, and of six real defects it credibly found **one**, missed **one**, and
told us nothing about **four**. The hand-written baseline scored 36 of 36 on the same scenarios in
the same run, so the scenarios, the executor and the defect switch are not what is failing.

**This is not a reason to abandon the planner; it is the reason the benchmark was built first.** The
next question is *why* the plans fail on a sound shop, which the report's notes and refusal reasons
begin to answer. Nothing in the product calls a model, and no claim about AI-found defects is
supported by this.

What is real and measured:

- **The boundary** ([ADR-0019](decisions/0019-the-planner-boundary.md)). The model plans once,
  before the browser opens, and is not in the execution loop. The vocabulary is nine actions and a
  target that is a test id or a role and a name — no field in which a selector or a script could be
  written, and a test that fails if one is ever added. A second test refuses every way Playwright
  offers to run a string as code, anywhere in the worker.
- **The validator.** A plan is refused for an action nobody defined, a path that could name another
  origin, a length over budget, or asserting nothing. Refused once with its errors, then the run
  ends saying planning failed — never the valid prefix. One test is the whole injection written out:
  a page whose heading is an instruction, and the plan a fully obedient model would produce, which
  does not run.
- **The baseline, measured.** Twelve controlled scenarios, six with a real defect behind a switch,
  36 runs: 36 correct verdicts, 0 false positives, 0 flake, median 1.3 s. The hand-written plan
  catches every defect and passes every sound run.
- **The instrument, calibrated.** The benchmark includes a planner that writes a plan designed to
  pass without testing anything, and CI insists the harness records it as a false positive. A report
  claiming zero false positives means nothing unless a false positive is something it can see.

**What the measurement changed, and what it has not yet proved.** Reading the planner's own notes
made it clear that most of the seven failures were ours, not the model's. Four fixes followed, and
none of them has been re-measured yet — the numbers above still stand as the published result until
a second run replaces them:

1. **The crawl was blind to text.** It reported roles, names and test ids, but not what an element
   said, so a plan asked to check a price was written against a page where no price was visible. The
   snapshot now carries the text of anything with a `data-testid`.
2. **The crawl saw one state.** It starts with an empty basket, so the basket showed its empty
   state, so there was no checkout link, so `/checkout`, `/account` and `/order/placed` were never
   described at all — and plans about checkout were written for pages the planner had never seen. It
   now also follows the paths the application advertises in its own `sitemap.xml`.
3. **The vocabulary could not express an absence.** The planner asked three times for a way to say
   "the cup is gone", "there is no way to place an order", "the error is no longer shown". Half of
   what a person wants checked is an absence, and without it a plan asserts some *other* presence
   instead, which passes either way. `assert-not-visible` was added under an
   [ADR-0019 §2 amendment](decisions/0019-the-planner-boundary.md).
4. **A partial plan could report a pass.** Plans said in their notes that they tested part of what
   was asked, passed that part, and were reported as passed. Coverage is now a field on the plan,
   and a passing partial plan ends as `Inconclusive`
   ([ADR-0019 §7 amendment](decisions/0019-the-planner-boundary.md)). The prompt rule that told a
   planner never to type a password also made it leave the field empty in the one scenario that
   tests a *wrong* password; it now distinguishes a real credential from a deliberately wrong one.

**Still open:** the re-run. It needs a model key, which this environment cannot hold, so it is a
command for a developer machine (`PLANNER__APIKEY=… dotnet run --project tools/Autestiq.Benchmark`) and
the numbers above are what the product claims until it has been done.

---
## Milestone 4 — Public website
**Scope:** all public pages from `sitemap.md` §A with real copy (EN + DE) where we have it and labeled placeholders elsewhere; mega-menu navigation, language switcher, rich footer; contact + demo forms (UI and validation only); Imprint, Privacy, Terms, Accessibility statement placeholders; original SVG illustrations; `sitemap.xml`, `robots.txt`, meta/OpenGraph tags, `hreflang`.

**Done when:** every public page has a Playwright smoke test (title, `h1`, no console errors, axe pass); the pages render in static SSR (no SignalR circuit opened — asserted in E2E); every page with a form carries `[HasForm]` and a test finds pages that forgot it (ADR-0009); Lighthouse (manual run, documented in the PR) shows Performance ≥ 90 and Accessibility ≥ 95 on Home and Pricing (mobile profile).

**Status: built (2026-11-14), waiting for review.** 29 routes in English and German: the home page,
the product section with the six journey stages, solutions with three roles and five industry
placeholders, pricing, the resources hub with three listings, about, careers, contact, request a
demo, and the four legal pages.

What the milestone turned out to be about, more than layout, was what a website is allowed to
claim:

- **Every page that describes something unbuilt says so**, in a box that cannot be skimmed past.
  Five of the six journey pages say "not built". The product overview says one of six stages is
  built. The home page says it above the fold rather than in small print.
- **Pricing shows no price.** A number would be a guess a visitor could budget against, which makes
  it the one placeholder that costs the reader something. The comparison table marks every row that
  does not exist yet.
- **There are no customer logos and no case studies**, and both sections say why. A row of borrowed
  logos is what a buyer checks first.
- **The Impressum lists every item § 5 DDG requires**, each marked "to be supplied", because a
  developer inventing company details would be worse than an empty page. The privacy notice
  separates what the code can prove from what a lawyer has to write. The accessibility statement is
  the one legal page that is real, and it lists three known limitations including that no
  screen-reader user has tested this.
- **Thirteen placeholder pages carry `noindex`** and are left out of `sitemap.xml`: a placeholder
  that ranks is a placeholder a stranger reads first.

Three faults found by building it rather than by review:

- The obvious responsive mega-menu — the whole navigation in one `<details>`, forced open by CSS on
  a desktop — **does not work**, because a browser hides a closed `<details>`'s content in a way CSS
  cannot override. Measured in Chromium at 1280px before the header was written: both the menu and
  its toggle were invisible. Each section is now its own disclosure.
- The `[HasForm]` test passed with the attribute deleted, because the page's own comment mentions
  `[HasForm]` and the test searched for the string. Found by deleting the attribute and watching the
  test stay green.
- The home page was still the Milestone 1 placeholder, and its hero claimed present-tense support
  for all six stages.

**Lighthouse, measured 2026-11-14** on a Release build, mobile profile, Lighthouse 13.6.0:

| | Performance | Accessibility | Best practices | SEO |
|---|---|---|---|---|
| Home (`/en`) | **100** | **100** | 100 | 100 |
| Pricing (`/en/pricing`) | **100** | **100** | 100 | 100 |

First contentful paint 1.2–1.3 s, largest contentful paint the same, total blocking time 0–10 ms,
cumulative layout shift 0.

Two things that number does not include, and which will lower it in the real world: the pages were
served from localhost, so there is no DNS, no TLS handshake and no distance to a server — Lighthouse
simulates a slow mobile network and a slow CPU, but it cannot simulate where the server is. And the
application was started with its database-reachability check patched out locally, because this
environment has no PostgreSQL the session can authenticate to. That patch was not committed and
affects nothing that was measured: a public page issues no query, which is why the pages served
correctly with no database at all.

A run against a deployed instance is still worth doing before anybody quotes these numbers
publicly.

## Milestone 5 — The first customer flow (ADR-0030)
**Status: done, accepted by the owner on 2026-11-23.** ADR-0020 narrowed this milestone, and ADR-0030 defined it, as
the first customer flow: project → environment with its site proved → a test described in a sentence → the AI's plan,
with its notes, its evidence labels and its Python → approved by a person → run → result → the approved test
downloaded. Built in four parts, each reviewed by the owner, with the security requirements of 2026-11-17 built
alongside: evidence retention (ADR-0031), exploring that may act (ADR-0032), test accounts (ADR-0033), expected
results stated by the tester (ADR-0034), and the validator's refusal of ambiguous steps (ADR-0024).

**Accepted with these limits, stated in the review:**
- no clean measurement yet on an application nobody here tuned the explorer on (next, ADR-0020);
- read-only exploration, a customer site's default, is markedly weaker (36 of 66 against 62 of 66);
- downloaded tests do not carry the test-account guard *(closed 2026-11-24 by Milestone 5.1 R4: such plans are not downloadable)*;
- exploring signed in changes the test account's data;
- not pilot-ready infrastructure: the key ring and evidence on local disk, G-2's minimized AI mode not built;
- no private agent (G-8).

### Deferred from the earlier plan for this milestone (issue #4)
The app shell as first planned, kept for when it comes back after the first pilot. Its registry-driven navigation and
feature flags are built; its mock sign-in and mock data were superseded by real ones (ADR-0017).

**Scope:** the app shell on the real sign-in from Milestone 3; app shell (registry-driven left nav, top bar with project switcher, search, notifications, user menu); `Autestiq.Mocks` with the seed data generator and all `Mock*` services (with configurable latency/failure); dashboard rendering registered widgets: quality KPIs, run trends, open defects by severity, release readiness, recent runs; feature flags hiding unfinished modules; "module not available" page.

**Done when:** each module registers its nav entry with one line and appears in the nav; turning a flag off hides its nav entry, widgets and routes (tested); dashboard widgets show loading, empty, error and data states (bUnit); seed-data consistency tests pass; E2E covers sign-in → dashboard → switch project.

## Milestone 5.1 — Pilot readiness *(approved by the owner on 2026-11-24; R1 and R4 built, R2, R3, R5–R7 not started)*
**Objective.** One QA team repeatedly uses the first customer flow on one authorized staging application, with
synthetic data and one dedicated test account, and trusts the verdict. Real customer data needs a separate readiness
decision afterwards (the P1 items below). The findings behind it, and their evidence, are in the dated review
[`reviews/2026-11-24-pilot-readiness.md`](reviews/2026-11-24-pilot-readiness.md).

**Out of scope:** the six shell modules, a warehouse, model training, billing automation, a `RunDispatcher` rewrite,
RLS, the minimized AI mode (G-2) and object storage. Each of these is P1 or later, kept in its register.

| # | Item | Acceptance criteria (including negative and security tests) | Depends on |
|---|---|---|---|
| R1 | **Planning recovery and one planner** (R-17). **Built 2026-11-24 (ADR-0037)** | A test left in Planning longer than a configured lease is moved on, but never silently re-planned. If no cost was recorded, it fails with "planning was interrupted" and the person can describe it again. Each planning attempt gets a row written **before** the provider call (started, model, ceiling) and completed after it. An attempt with no completion counts against the budget at its ceiling until it is reconciled, so a crash can never undercount. The deployment runs exactly one planning instance: a PostgreSQL advisory lock, and a second instance that cannot take it does not plan, which is tested. Tests cover a crash before the call, after the call, and before the save, all with a fake planner | — |
| R2 | **A host that can be deployed** | A migration command in `tools/Autestiq.Operator` (`migrate`, and `migrate --to <name>` for rollback), never run at web start. A real email notifier behind `IInvitationNotifier` (SMTP, provider chosen by the owner); the startup refusal stays. `/health/live` and `/health/ready` (database, key ring readable, worker image present). Tests: the host refuses to start without a notifier or key ring outside development (exists), readiness fails when the database is down, and a migration up and down round-trips on an empty database | Owner: email provider |
| R3 | **Pilot runbook, rehearsed once** | `docs/operations/pilot-runbook.md` for an exact commit and worker image digest. It covers deployment, migration and rollback, email, worker isolation (container mode), the kill switch, health checks, alerts and recovery. It also covers a **restore rehearsal: PostgreSQL and the key ring together**, after which a stored test-account secret decrypts and a screenshot link from before the restore resolves, or is reported as deleted. Steps and results are recorded as they happened | R2; owner: hosting for the rehearsal machine |
| R4 | **Export carries the guard**. **Built 2026-11-24 (ADR-0033 amended)** | For a plan whose positive check names a value the plan typed by secret name, export is **blocked**, saying why, until the template can carry the guard. Parity test: the same regression fixture that gives a hosted inconclusive gives a refused export, never a passing download. The test page shows the reason before download | — |
| R5 | **Re-measure on the release candidate** | The protected unseen sets (three apps, with and without test ids, read-only) and PetClinic, run on the exact commit tagged as the candidate. The report records the revision, model, prompt hash, target version, exploration mode and reset procedure. **Each stated expected result is scored**: false pass, wrong fail, partial, inconclusive, kept apart. Tuning fixtures stay out of the held-out sets. ADR-0020's figures are replaced only by these, with the old qualifications kept beside them | The owner's re-measurement of 2026-11-24 (in progress) gives the prompt's figures; R5 repeats it on the candidate |
| R6 | **Rehearsal measures** | One PostgreSQL view per run and per planning attempt. It gives time to first approved test, time to first verdict, manual corrections (re-describes, edits to expected results), repeat use, full, partial and inconclusive rates, queue wait, and cost per completed run. It holds **no DOM, screenshots, prompts, page text or credentials**, which a test asserts column by column | R1 (the attempt rows) |
| R7 | **A first release** | `release/0.5.0` cut from `develop` per `CONTRIBUTING.md`, merged to `main`, tagged, with the worker image digest in the release notes | R1–R6; owner: visibility, branch protection |

**Implementation order:** R1 (smallest, removes a stuck state and a budget undercount), R4, R2, R6, R3, R5, R7.
**Smallest useful first PR:** R1's lease, recovery and single-planner lock, with its crash tests.

**Checks before the staging rehearsal:** R1–R7 done; `dotnet build` clean and the full suite green on the tagged
commit; the restore rehearsed; the kill switch exercised once on the staging host; R5's figures published with their
limits; synthetic data and one dedicated test account only.

**Checks before a pilot with real customer data (a separate decision):** G-2's minimized mode, built and measured;
the lifecycle of snapshots, planning payloads, logs and backups (G-5); least-privilege database roles (R-19); the
composite environment key (R-18); object storage and a key service, once hosting is decided; transactional budget
reservations if more than one planner runs (R-17); repository visibility decided.

**The rehearsal:** one QA team, one authorized staging application, synthetic data, one test account, three to five
known journeys. It is judged by repeat use and by the time and correction needed to reach a verdict the team trusts.
Its results decide which feature earns the next investment.

## AI learning programme (proposed, 2026-11-24)
A long-term programme to make Autestiq's QA intelligence improve from verified experience: a Training Lab grown from
the benchmark harness, experience retrieval measured first, fine-tuning only on evidence, no model trained from scratch.
**Proposed, not started; nothing in it is built.** Strategy, phases and gates in
[`ai-learning/roadmap.md`](ai-learning/roadmap.md); decision in ADR-0036 (proposed). It complements this roadmap and
does not reorder it; its first phases use the benchmark tool only, beside pilot readiness.

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
**Scope:** `Autestiq.Capabilities.E2EWeb` implementing `ITestCapability` (settings, run setup, live view, results); `MockTestRunLiveFeed` with scripted scenarios (passing run, failure at step 7, self-healed locator); live view with step timeline, highlighted current step, per-step status, screenshot panel (mock screenshots as generated SVGs of a fake web shop), log console with level filters and auto-scroll lock, elapsed time, stop / rerun; polite screen-reader announcements; reconnect/gap handling via `Sequence`.

**The real live view (the owner's request of 2026-11-17).** Beyond the scripted feed: a customer watches their
own run inside Autestiq, step by step, while it happens. The browser always runs on our side, in its sealed
container (or, later, in the customer's private agent), and never opens a window on the customer's machine.
Two stages:
1. **Step by step.** The worker already screenshots each step. It sends each step's result and picture as soon as
   the step ends, and the run page updates over the circuit the app already holds. Near-live, a picture per step.
2. **A continuous picture.** The browser streams its screen while it runs (Playwright's screencast, a few frames
   a second): smoother, with more bandwidth and storage to plan for.

What streams is customer evidence (`docs/secure-onboarding-and-privacy.md` §4–5):
- it is masked like screenshots, with password fields hidden;
- it is shown only to members who may read runs;
- it is kept no longer than the retention policy allows, and follows the project's evidence policy;
- from a private agent it streams only if the customer's data-sharing policy allows, and otherwise only step
  results come through;
- the viewer is read-only: nobody can click through it into the customer's site.

**Done when:** adding the capability required only the new project + one registration line (verified in the PR diff); reducer and component tests run with a fake `TimeProvider` (no real waiting); E2E watches a mock run to completion and asserts the final status; stop and rerun work; reduced-motion mode verified.

## Milestone 8 — Polish
**Scope:** responsive pass (mobile, tablet, desktop, wide) on every page; full accessibility pass (keyboard-only walkthrough of main flows, screen-reader spot checks with NVDA/VoiceOver, axe everywhere); dark mode review; E2E tests for main flows (public → request demo; sign in → dashboard → requirement → traceability → start E2E run → watch live → create defect from failed step → release readiness); performance check of public pages; all docs updated; a short "how to replace mocks with the real backend" guide in `docs/`.

**Done when:** all main-flow E2E tests pass in CI at desktop and mobile viewports in both themes; zero serious/critical axe violations; docs reflect the final state.

---

## Later (out of scope for the frontend phase)
- `Autestiq.Api`: the REST host for CI integrations, over the same use cases (ADR-0014).
- SignalR for the live run feed, replacing the simulated one.
- Per-organization single sign-on (OpenID Connect), behind the interfaces of ADR-0013.
- More capabilities: API, performance, security, accessibility testing.
- `InteractiveAuto` for the app area if needed.
