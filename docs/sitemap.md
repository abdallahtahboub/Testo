# Sitemap

> Status: **Accepted** (Step 0 approved on 2026-11-11).
> Public routes are shown without their culture prefix. Every public page is served under
> `/en/...` and `/de/...` (ADR-0006); `/` redirects to the visitor's language.
> "(P)" = placeholder content, clearly labeled as such in the UI.

## A) Public website (static SSR)

### Global
| Element | Purpose |
|---|---|
| Header | Logo, mega-menu (Product, Solutions, Resources, Company), Pricing link, language switcher (EN/DE), theme switch, Sign in, Request demo |
| Footer | Multi-column links (Product, Solutions, Resources, Company, Legal), language switcher, newsletter field (UI only), social placeholders, copyright, Imprint + Privacy links |
| Cookie notice | Minimal notice (we set only functional cookies: culture, theme, auth) |

### Pages
| Route | Page | Purpose |
|---|---|---|
| `/` | Home | Hero, value pillars, capability grid (one card per journey stage), "how it works" journey visual, social proof (placeholder logos, labeled), latest resources, CTA |
| `/product` | Product overview | The platform at a glance: journey, traceability, AI principles, integrations |
| `/product/planning` | Test planning | Plans, risks, entry/exit criteria |
| `/product/analysis` | Test analysis & traceability | Requirement review, test conditions, traceability matrix |
| `/product/design` | Test design | AI-assisted test case design, techniques, test data |
| `/product/execution` | Test execution | Manual runs, CI results, capability plug-ins |
| `/product/defects` | Defect management | Lifecycle, triage, linking, sync |
| `/product/reporting` | Reporting & release readiness | Dashboards, summary reports, go/no-go |
| `/product/capabilities` | Capabilities overview | Lists all registered test capabilities (registry-driven) |
| `/product/capabilities/e2e-web` | AI E2E web testing | The flagship capability, with live-run visual |
| `/product/capabilities/{id}` | Future capabilities | API, performance, security, accessibility ("coming soon", feature-flagged) |
| `/product/integrations` | Integrations | Git, Azure DevOps, Jira, CI systems (P) |
| `/product/security` | Security & trust | Data protection, hosting in the EU (P), GDPR approach |
| `/solutions` | Solutions overview | Entry to role and industry pages |
| `/solutions/qa-lead` | For QA leads | Planning, monitoring, release readiness |
| `/solutions/developer` | For developers | Fast feedback, CI integration, actionable defects |
| `/solutions/engineering-manager` | For engineering managers | Quality KPIs, risk, release confidence |
| `/solutions/industries/{industry}` | Industry pages (P) | e.g., `finance`, `insurance`, `retail`, `automotive`, `public-sector` |
| `/pricing` | Pricing (P) | 3 plan cards + feature comparison table, "pricing not final" label, FAQ |
| `/resources` | Resources hub | Entry to blog, docs, case studies, events |
| `/resources/blog` | Blog listing (P) | Filterable article cards |
| `/resources/blog/{slug}` | Blog article (P) | Article template |
| `/resources/docs` | Documentation listing (P) | Docs categories |
| `/resources/case-studies` | Case studies listing (P) | Case study cards |
| `/resources/case-studies/{slug}` | Case study (P) | Case study template |
| `/about` | About us | Mission, story, values, team (P) |
| `/careers` | Careers (P) | Open roles placeholder |
| `/contact` | Contact | Contact form (UI only, validation, success state), address (P) |
| `/request-demo` | Request a demo | Demo request form (UI only) |
| `/imprint` | Imprint (Impressum) (P) | Legal provider identification (§ 5 DDG) — placeholder, to be filled by you/legal |
| `/privacy` | Privacy policy (Datenschutz) (P) | GDPR privacy notice — placeholder, to be filled by you/legal |
| `/terms` | Terms (P) | Terms of use placeholder |
| `/accessibility` | Accessibility statement | WCAG 2.2 AA commitment, feedback contact (BFSG relevant) |
| `/sign-in` | Sign in (mock) | Demo users "Continue as …" |
| `/404`, `/error` | Not found / error | Friendly error pages with navigation |

### Mega-menu structure
- **Product:** Overview · Journey stages (6 links) · Capabilities (registry) · Integrations · Security
- **Solutions:** By role (3) · By industry (5, P)
- **Resources:** Blog · Docs · Case studies · Events (P)
- **Company:** About · Careers · Contact
- Featured panel in each menu: one highlighted card (e.g., "See a live AI test run")

## B) Product app (`/app`, interactive server, behind mock sign-in)

### Shell
| Element | Purpose |
|---|---|
| Left navigation | Registry-driven, grouped (Overview, Plan, Build, Run, Report, Settings), collapsible, feature-flag and role aware |
| Top bar | Project switcher (+ release selector), global search (Ctrl/⌘ + K), notifications panel, help, user menu (profile, language, theme, sign out) |
| Breadcrumbs | Current location within the project |

### Pages
Routes are scoped by project: `/app/p/{projectKey}/...` (e.g., `/app/p/orion/defects`).

| Route | Module | Page | Purpose |
|---|---|---|---|
| `/app` | Web | Project picker | Choose or switch project; redirect to last used |
| `/app/p/{p}` | Web | Dashboard | Quality KPIs, run trends, open defects by severity, release readiness, recent runs (registered widgets) |
| `/app/p/{p}/planning` | Planning | Test plans | List of plans per release |
| `/app/p/{p}/planning/{planId}` | Planning | Test plan detail | Scope, approach, schedule, entry/exit criteria, approvals |
| `/app/p/{p}/planning/risks` | Planning | Risk register | Risk list + risk matrix heat map |
| `/app/p/{p}/analysis` | Analysis | Requirements | Requirement list with coverage status, import placeholder |
| `/app/p/{p}/analysis/{reqId}` | Analysis | Requirement detail | Acceptance criteria, AI testability review, test conditions, linked tests and defects |
| `/app/p/{p}/analysis/traceability` | Analysis | Traceability matrix | Requirements × test cases, gap highlighting, filters, export (P) |
| `/app/p/{p}/design` | Design | Test case library | Folder tree + grid, filters, bulk actions |
| `/app/p/{p}/design/{tcId}` | Design | Test case editor | Steps, expected results, data, versions, AI-suggestion panel |
| `/app/p/{p}/design/generate` | Design | AI test case generation | Pick requirement/condition + technique → review suggestions → accept |
| `/app/p/{p}/execution` | Execution | Test runs | Run history list with status, capability, duration, filters |
| `/app/p/{p}/execution/new` | Execution | Start a run | Choose suite, environment, capability (renders capability's setup UI) |
| `/app/p/{p}/execution/suites` | Execution | Test suites | Suite builder |
| `/app/p/{p}/execution/environments` | Execution | Environments | Environment registry |
| `/app/p/{p}/execution/runs/{runId}` | Execution | Run detail | Results per test; delegates to the capability's results UI |
| `/app/p/{p}/execution/runs/{runId}/live` | Capability E2EWeb | **Live E2E test run** | Step timeline, current step, status per step, screenshot panel, log console, elapsed time, stop/rerun |
| `/app/p/{p}/execution/manual/{runId}` | Execution | Manual test runner | Step-by-step execution with evidence |
| `/app/p/{p}/execution/health` | Execution | Test health | Flaky tests, never-failing tests, maintenance backlog |
| `/app/p/{p}/defects` | Defects | Defect list / board | Grid and kanban views, severity and status filters |
| `/app/p/{p}/defects/{defId}` | Defects | Defect detail | Report, evidence, lifecycle history, links to tests/requirements/runs |
| `/app/p/{p}/reporting` | Reporting | Reports overview | Metrics, trends, coverage |
| `/app/p/{p}/reporting/releases/{relId}` | Reporting | Release readiness | Exit criteria, readiness score, go/no-go sign-off |
| `/app/p/{p}/reporting/releases/{relId}/summary` | Reporting | Test summary report | Printable summary, AI draft (P) |
| `/app/p/{p}/reporting/releases/{relId}/closure` | Reporting | Test completion | Closure checklist, lessons learned, archive |
| `/app/settings` | Settings | Settings overview | Entry to settings areas |
| `/app/settings/integrations` | Settings | Integrations (P) | Git, Azure DevOps, Jira connection cards (UI only) |
| `/app/settings/team` | Settings | Team members | Members, roles, invite (UI only) |
| `/app/settings/capabilities` | Settings | Capabilities | List of registered capabilities; renders each one's settings UI |
| `/app/settings/project` | Settings | Project settings | Name, key, releases, workflow |
| `/app/settings/profile` | Settings | My profile | Language, theme, notification preferences |
| `/app/settings/feature-flags` | Settings | Feature flags (dev only) | Current flag states |
| `/app/search?q=` | Web | Search results | Results across requirements, tests, runs, defects |
| `/showcase` | UI.Shared | Component showcase (dev/staging only) | All design-system components with variants and states |

## Public URLs and language (decided, ADR-0006)

Public pages use culture-prefixed URLs (`/en/...`, `/de/...`) with `hreflang` tags — best
for German SEO. Paths stay English in both languages (`/de/pricing`). Translated slugs
(`/de/preise`) stay possible later, because every page declares its routes per language.
The app (`/app/...`) has no prefix; its language comes from the user's preference cookie.
