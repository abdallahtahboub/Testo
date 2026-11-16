# Commercial readiness

> Status: **§6 accepted by the owner on 2026-11-15**; the product name and the first-customer details
> remain open (§9). Written 2026-11-15 from the founder directive (Phase A to Phase E), against commit
> `5f6179d`. Every claim below cites code or a document in this repository. Items that were not checked
> are marked **Not verified**.

The directive's test for every decision is one question: does it protect customer safety, security,
execution correctness, or cost control before it adds anything else? The current order of work in
[`roadmap.md`](roadmap.md) already puts the safety gates first. What it does not yet contain is any
commercial control: there is no plan catalog, no usage ledger, no quota and no billing.

**Naming.** The directive calls the product *Autestiq*. `CLAUDE.md` and every document in the
repository say *Testo*, a working name that "may still change". This proposal keeps *Testo*. A rename
is a mechanical find-and-replace, but it is a decision for the owner, not for this document.

---

## 1. Current product assessment

Evidence is code or a test that was read. "Verified by running" is stated only where it was run.
This session did not run the test suite; the counts below come from grep and the inventory in
[`roadmap.md`](roadmap.md), which was last verified on 2026-11-14.

### Implemented

| Capability | Evidence |
|---|---|
| Organizations, memberships, invitations, roles, permission matrix | `src/Autestiq.Domain/Identity/`, `src/Autestiq.Application/Identity/`, `tests/Autestiq.Application.Tests/Identity/PermissionMatrixTests.cs` |
| Tenant isolation at the query layer | `IBelongsToOrganization` on entities; query filter and save guard. Two of three layers are the database's own (ADR-0012) |
| Sign-in is required on app pages | D-1 fixed in Milestone 3.1 ([`security-register.md`](security-register.md)) |
| Cross-tenant tenant-id trust, tokens in logs, rate limiting on sign-in | D-2, D-3, D-4 fixed in Milestone 3.1, each with a test that fails without the fix |
| Append-only audit entries, enforced through EF | `src/Autestiq.Domain/Audit/AuditEntry.cs`, `TenantGuardInterceptor`. Raw SQL can still rewrite it (R-4) |
| Test runs: queue, claim with optimistic concurrency, cancel that survives restart | `src/Autestiq.Application/Execution/RunDispatcher.cs`, `test_runs` table (R-2) |
| Worker out of process, default-deny egress proxy, judged per resolved address | `src/Autestiq.Worker/Egress/EgressPolicy.cs`, 45 tests in `tests/Autestiq.Worker.Tests/Egress` (R-1) |
| Artifact links: signed, expiring, tenant-filtered | `src/Autestiq.Application/Execution/ArtifactLinks.cs`, `ExecutionIsolationTests`, `ArtifactAccessTests` (R-3) |
| Planner boundary: model plans once, closed vocabulary, validator | `src/Autestiq.Ai/`, `src/Autestiq.Application/Planning/`, ADR-0019 |
| Per-run token and money ceilings ending as `BudgetExceeded` | ADR-0019 §6 |
| Planner cost arithmetic per call | `src/Autestiq.Ai/AnthropicPlanner.cs:144-148` |
| Measured planner benchmark with a self-check | `tools/Autestiq.Benchmark`, CI step "Check the benchmark can catch a planner that only pretends" (`.github/workflows/ci.yml`) |
| CI: build with warnings as errors, all tests against PostgreSQL 16, Playwright browser tests | `.github/workflows/ci.yml` |
| Public website, 29 routes, EN and DE, labeled placeholders | `src/Autestiq.Website/`, Milestone 4 in [`roadmap.md`](roadmap.md) |

Approximate size: about 525 `[Fact]`/`[Theory]` attributes across `tests/` (grep count, not a run).

### Partially implemented

| Capability | What exists | What is missing |
|---|---|---|
| Plans and entitlements | `Organization.Plan` is a free string, default `"trial"` (`src/Autestiq.Domain/Identity/Organization.cs:20`) | No catalog, no versions, no limits. Nothing reads `Plan` except persistence (grep `\.Plan\b`) |
| Concurrency control | `MaxConcurrentRuns = 2` in `ExecutionService.cs:293`, enforced by a per-process `SemaphoreSlim` in `RunDispatcher` | Not per organization. Not across processes. No tenant-level cap |
| Cost control | Per-run planner budget (ADR-0019 §6); per-call cost computed | No per-organization spend limit (R-10). Prices are hard-coded options (`AnthropicPlanner.cs:34,37`), not a price version. No usage record |
| Artifact storage | `IArtifactStore` with a local directory (`FileSystemArtifactStore`), one folder per organization and run | Object storage, retention, redaction (R-3). No deletion job exists (grep for purge or retention found nothing) |
| Background execution | `RunDispatcher` polls the `test_runs` table | Several processes; reaping of runs left `Running` by a dead host (R-2) |
| Rate limiting | `AccountRateLimiting` on sign-in | Forwarded-address handling behind a proxy (R-8). Not on run starts or planner calls |
| Organization data region | `Organization.DataRegion`, chosen at creation (`Organization.cs:14-17`) | Not enforced in routing or storage. No second region exists |
| Two-factor | `Organization.RequiresTwoFactor` flag | Authenticator enrollment deferred (roadmap inventory) |
| Projects | `Project` entity with `KeyPrefix` and `Status` (`src/Autestiq.Domain/Projects/Project.cs`) | No service, no UI, no project-scoped quota |
| Email | Invitation and reset links are written to the log outside development (D-3 fixed by refusing to start that way in production) | Email delivery is deferred. Customers cannot yet be onboarded without an operator |

### Missing

- **Billing.** No provider integration, no webhook handling, no idempotency keys for payment events,
  no subscription lifecycle (upgrade, downgrade, cancellation, grace, proration), no invoices.
- **Usage metering.** No usage record, no ledger, no billable-unit definition, no rounding rule,
  no idempotency identifier, no billing period.
- **Quotas and reservation.** No check before an expensive operation, no reservation, no
  reconciliation, no release of unused capacity.
- **Cost analytics.** No per-organization cost, no contribution, no margin. Nothing records what a
  run cost in browser minutes, storage or model tokens as one attributable row.
- **Kill switch.** No operational switch that stops execution or planning for all tenants or for
  one organization. A grep for kill, halt and emergency found only process-kill code in the
  dispatcher.
- **Proof of origin control.** Required before any customer-supplied target (execution-threat-model
  §5). Not built. Currently moot because `TargetOrigin` comes from configuration
  (`ExecutionService.cs:231,287`).
- **Container per run.** Required before any browser runs against a customer target (R-1). Not built.
- **GitHub and CI integration.** None. No API for CI (`tools/` lists `Autestiq.Api` as planned only).
- **Usage and quota dashboard for customers.** None.
- **Product analytics.** None. Activation, retention and conversion cannot be measured yet.
- **Autonomous exploration and self-healing.** Not built. The planner exists only as a measured
  prototype that failed its own benchmark: 5 of 12 plans passed a sound application (ADR-0019,
  [`roadmap.md`](roadmap.md) Milestone 3.4).
- **Retention and deletion procedures.** No purge job. `identity-and-access.md` §6 describes intent.

### Not verified

- **Compliance documents.** Subprocessor inventory, DPA, backup and restore runbook, incident
  response plan. Not checked in this session; treat as missing until a document is named.
- **Whether the test suite passes on this commit.** Not run in this session. CI state on `develop`
  should be read before any claim about green builds.
- **Accessibility claims in the public site,** beyond the Milestone 4 Lighthouse note in the roadmap.
- **Load behaviour** of the dispatcher and the proxy under concurrency. No load test exists.

---

## 2. Business model alignment report

What must change for the commercial model in the directive to be honest:

1. **Plans must be data, not a string.** `Organization.Plan = "trial"` cannot carry a contractual
   entitlement. A versioned plan catalog with effective dates is needed, so an existing customer keeps
   the limits they signed for when a plan changes (directive §7, "Plan definitions must be
   versionable").
2. **Usage must be a ledger.** Metering must be append-only, idempotent on a key that a retry reuses,
   and reconciled to the final state of each run. A count in a row that is updated in place cannot
   reconcile.
3. **Two cost classes, two units.** Browser minutes and model tokens have different cost curves, so
   they must be recorded separately even if billed together.
4. **Quotas are enforced before work starts, not after.** Concurrency per organization must be a
   database-enforced count, not a per-process semaphore.
5. **No commercial claim is supportable yet.** Pricing (§5.2 of the directive) is a hypothesis. The
   product has no cost data to set it against.

---

## 3. Gap analysis

Severity follows [`security-register.md`](security-register.md): **Critical** blocks exposing the
platform to untrusted customers; **High** blocks paying customers; **Medium** blocks scale or
confidence; **Low** is hygiene. Complexity is S (under a week), M (one to three weeks), L (longer),
judged by the engineer, not measured.

| # | Gap | Business impact | Risk | Severity | Depends on | Recommended change | Complexity | Required tests | Milestone |
|---|---|---|---|---|---|---|---|---|---|
| G1 | **Built 2026-11-16 (Milestone 3.5), file method; no screen until Milestone 5.** No proof that a customer controls a target origin | A free browser aimed at others' websites from our address | 04, 02 | **Critical** before customer targets | none | Verification challenge (DNS TXT or file at origin), recorded per origin, checked at run start | M | proof cannot be forged by a second org; expired proof refused; target without proof refused at API and worker | 3.5 |
| G2 | **Fixed 2026-11-16 (R-11).** Loopback exception in the egress policy would apply to any customer target | A customer target of `http://127.0.0.1:…` reaches the worker host's own services (`EgressPolicy.cs:60-66` allows loopback when the origin is "trusted local"; `RunDispatcher.cs:140` sets it from the run's target) | 04 | **Critical** once G1 lands | G1 | Trusted-local applies only to origins from configuration, never from a stored run. Test that a stored loopback target is refused | S | loopback target from a row refused; configured demo origin still allowed | 3.5 |
| G3 | **Built 2026-11-16 (Milestone 3.5, ADR-0026).** No container per run | Browser compromise reaches the host and other runs | 04, 02 | **Critical** before customer targets | none | Ephemeral container per run, non-root, no credentials, read-only root, default-deny network | M–L | container cannot read host secrets, cannot reach metadata, cannot reach the database | 3.5 |
| G4 | **Fixed 2026-11-16 (Milestone 3.5).** No kill switch | Runaway cost or a bad target cannot be stopped quickly | 03 | **Critical** | none | Config flags checked at dispatch: global and per organization. Stop and cancel in-flight runs | S | flag set mid-run stops the run within a bound; flag per org leaves others running | 3.5 |
| G5 | **Fixed 2026-11-16 (Milestone 3.5).** Stale `Running` runs not reaped | Hung runs hold concurrency slots and spend | 03, 07 | High | none | Lease with heartbeat; reaper marks abandoned runs as infrastructure failure, not defect | S | killed worker process leaves run reaped; reaped run classified as infrastructure error | 3.5 |
| G6 | **Built 2026-11-16 (Milestone 3.6, ADR-0027).** No usage ledger | Cannot bill, cannot reconcile, cannot prove a cost | 08, 03 | **High** | none | `usage_events` append-only table: organization, project, run, category, quantity, unit, period, idempotency key, source | M | duplicate event with same key stored once; retry records once; events sum to run totals | 3.6 |
| G7 | No per-organization concurrency cap | One tenant starves others or multiplies cost | 03 | High | G6 partly | Count of active runs per organization enforced in the database, inside the claim transaction | S | two concurrent starts past the cap: one refused; cap survives restart | 3.6 |
| G8 | No reservation or reconciliation | Quota can be bypassed by parallel jobs | 03, 08 | High | G6 | Reserve estimated minutes and tokens at start; reconcile on finish; release the difference | M | parallel starts cannot exceed quota; cancelled run releases reservation; crash between reserve and finish is reconciled | 3.6 |
| G9 | No plan catalog or entitlements | Contracts cannot be enforced or changed safely | 08 | High before billing | G6 | Versioned plan definitions with effective dates; organization references a version; quotas read from the version | M | changing a plan does not alter an existing version; entitlement check refuses over-limit start | 3.7 |
| G10 | No cost record per run | Cannot compute contribution or margin | 03, 05 | High | G6 | Cost rows per run: browser seconds, storage bytes, model tokens × price version id | S–M | a run's cost rows sum to its recorded total; price version change does not rewrite history | 3.8 |
| G11 | Price constants in code | Price changes need a deploy and are not traceable | 08 | Medium | G10 | Price versions as configuration with effective dates | S | historic cost uses the price in force at the time | 3.8 |
| G12 | Artifacts on local disk, no retention | Customer screenshots kept indefinitely, no deletion | 09 | High before pilots | none | Object store behind `IArtifactStore`; retention job by policy (90 days; traces shorter, off by default) | M | expired artifact deleted; deleted artifact not served; other tenants' artifacts unaffected | 3.8 or pilot gate |
| G13 | No email delivery | Customers cannot be invited without an operator | 07 | High before pilots | none | Transactional email behind an interface, provider chosen by ADR | M | invitation delivered once; token never logged | pilot gate |
| G14 | Rate limits do not cover run starts or planner calls | Abuse through the expensive paths | 03 | High | G6 | Per-organization and per-user limits on start and planning | S | exceeding limit returns refusal without queuing a run | 3.6 |
| G15 | Billing provider absent | No revenue collection | 08 | High, not yet | G9, G6 | Stripe Billing in test mode behind an ADR; webhooks verified and idempotent | L | duplicate webhook produces one state change; failed payment cannot create an active subscription; test mode only | Phase D, after 3.8 |
| G16 | No backup, restore or incident runbook | Data loss is unrecoverable; no operating procedure | 09 | High before pilots | none | Documented procedure and one tested restore | S–M | restore into a clean database reproduces the tenant data set | pilot gate |
| G17 | Audit log not tamper-evident | Security review will ask | 09 | Medium | none | Hash chain and periodic verification, or soften the claim (R-4) | M | altered row detected by verification | 8 or before enterprise |
| G18 | No product analytics | Cannot measure activation or retention | commercial | Medium | none | Activation events recorded from usage ledger and sign-in; reported internally, not to customers | S | events recorded once; no personal data in event payloads | 3.8 |
| G19 | Compliance documentation absent | Blocks procurement | 09 | High before paying customers | legal review | Subprocessor list, DPA template, retention schedule. Flagged for legal review; no certification claimed | M (non-engineering) | — | pilot gate |

**Not a gap now:** multi-tenant database isolation (ADR-0012, R-7 accepted), public website
claims (Milestone 4 already labels unbuilt work), and mobile, desktop, API and performance testing
(directive §16, deliberately not started).

---

## 4. Risk matrix

Each directive risk, mapped to the register's existing items where they exist. Probability and
impact are judgements, not measurements.

| Directive risk | Severity | Probability (now) | Business impact | Current mitigation | Missing mitigation | Required action | Verification |
|---|---|---|---|---|---|---|---|
| 01 AI produces unreliable results | Critical | **Certain**, measured: 7 of 12 false negatives | Trust collapse | Benchmark; closed vocabulary; validator; `Inconclusive` for partial plans | Re-measurement after four fixes; confidence only from data | Re-run benchmark with a model key (developer machine); publish result beside baseline | `tools/Autestiq.Benchmark` self-check in CI; published rates |
| 02 Tenant leakage | Critical | Low given current scope (demo target only) | Existential | Query filter; save guard; D-1, D-2 fixed; isolation tests against PostgreSQL | No cross-tenant test for worker and artifact paths in a second org beyond the current set; no fuzz | Add second-organization adversarial tests to each new path | `ExecutionIsolationTests`, `ArtifactAccessTests` extended per path |
| 03 Uncontrolled cost | Critical | High once paid plans exist | Loss per customer | Per-run budget; concurrency of 2 per process | G4 kill switch; G6–G8 ledger, quota, reservation; R-10 | Gaps G4, G6, G7, G8, G14 | Concurrency and quota tests with parallel starts |
| 04 Unsafe autonomous actions and injection | Critical | Medium once customer targets exist | Customer data, abuse of our address | Default-deny egress; no selectors as code; prompt injection test; worker has no database or credentials | G1 proof of origin; G2 loopback; G3 container per run | Gates in execution-threat-model §5 | Egress tests; container tests; G2 regression test |
| 05 No product-market fit | Critical, business | Outside code | Wasted build | None in code | Design partners; interviews | Founder work, tracked outside the repository | Pilot conversions, not code |
| 06 Competitor parity | High, strategic | Unmeasurable here | Price pressure | Differentiators named in directive | — | Keep scope narrow | — |
| 07 Churn | High | Unmeasured | Revenue | Honest claims on website | G18 analytics; G5 reaping for reliability | G18 | Activation events |
| 08 Billing errors | High | Not yet relevant (no billing) | Disputes, refunds | Audit log (append-only through EF) | G6, G9, G15 | Ledger before billing | Duplicate and reconciliation tests |
| 09 Security, privacy, compliance | High | Medium | Procurement blocked; fines | Secrets not logged (D-3); accessibility statement; Impressum placeholders | G12, G16, G17, G19; R-4, R-5 | Gates G12, G16, G19 before pilots | Retention and restore tests; legal review |
| 10 Overcomplexity | High | Medium | Slow delivery | Modular monolith; one-line module registration; ADRs | Scope creep into planner before measurement | Keep roadmap order; insert gates ahead of commercial work | Milestone reviews |

**Items not in the directive's list but in the register:** R-1 (container), R-2 (background work), R-3
(artifact storage), R-8 (forwarded headers), R-9 (hostile page), R-10 (model spend). Each is cross-referenced
above where it matters.

---

## 5. Architecture recommendations

The smallest defensible changes. None adds a service.

1. **Keep the modular monolith.** The directive asks us to avoid microservices. The execution worker is
   already the one separate process, and it is separate for security, not for scale (ADR-0018). That
   is the right number of boundaries for a pilot.
2. **Usage ledger as one append-only table** in the tenant boundary, with an idempotency key and a
   unique constraint on it. Runs write reservations and reconciliations as rows, never updates to a
   count.
3. **Plan catalog as versioned rows**, read-only in the application, changed by migration or by a
   deliberate administrative action that is audited. The organization stores a plan version, not a name.
4. **Concurrency and quota checked inside the claim transaction** with a count under a row lock on the
   organization. This is the cheapest correct option on PostgreSQL and needs no new infrastructure.
5. **Kill switches as configuration** read at dispatch and at planning, global and per organization.
   Changing them must be audited.
6. **Price versions as configuration** with effective dates, referenced by cost rows.
7. **Billing provider only after the ledger is correct.** Stripe Billing test mode is the candidate
   (directive §7, Phase D). It needs an ADR and must never receive raw usage that the ledger has not
   reconciled.

**Tradeoff to decide:** the ledger is only as good as the events that feed it. Recording a run's cost
at finish means a crash mid-run loses it unless reconciliation handles the gap. Reconciliation on
reaped runs (G5) is therefore part of the ledger work, not a later cleanup.

---

## 6. Proposed roadmap insertions

The current order is **3.1 → 3.2 → 3.3 → 3.4 → 4 → 5 → 6 → 7 → 8**. Milestones 3.1–3.4 and 4 are built
and waiting for review. Milestone 3.3's "container per run" gate is still open, and the threat model
says it must close before any customer target.

Proposed additions, to be inserted **only after the owner accepts this document**:

| Milestone | Scope | Gaps | Done when | Gate it opens |
|---|---|---|---|---|
| **3.5 — Customer-target safety** (P0) | Proof of origin; loopback limited to configured origins; container per run; kill switch; stale-run reaping | G1–G5 | Each gate in execution-threat-model §5 has a failing-without-fix test | Any run against a customer URL |
| **3.6 — Usage ledger and quotas** (P0) | Append-only usage events; per-organization concurrency; reservation and reconciliation; start-rate limits | G6, G7, G8, G14 | Parallel start test cannot exceed quota; duplicate events stored once; crash-recovery test passes | Paid plans, and any customer with a cost ceiling |
| **3.7 — Plan catalog and entitlements** (P1) | Versioned plans; organization references a version; enforcement at start | G9 | Changing a plan leaves existing versions unchanged | Customer-visible quota information |
| **3.8 — Cost and evidence retention** (P0/P1) | Cost rows per run; price versions; object storage; retention job; product events | G10–G12, G18 | Retention job deletes expired artifacts and is tested; cost rows reconcile to usage | Pilot with real data |
| **Pilot gate** (non-code) | Email delivery; backup and restore; subprocessor list; DPA; incident runbook | G13, G16, G19 | Each item has a document or a tested procedure, reviewed by counsel where flagged | Design partner pilots |
| **Phase D — billing sandbox** (P1) | Stripe Billing in test mode, webhooks, subscription lifecycle | G15 | ADR accepted; duplicate webhook test; failed payment test | Paid pilot, after owner approval |

**Sequencing.** Milestone 4 (public website) is built and needs review; it is not blocking. Milestone 5
(the testing-journey modules) is large and mostly UI over mock data. This proposal puts 3.5 and 3.6
**before** it, because the directive's priority order puts safety and cost first, and because the
execution slice is the only part of the product that spends money or touches the internet.

**What this does not do.** It does not start the billing provider, does not change the planner, and
does not promise a price. It does not claim any of the commercial success criteria in directive §17,
which need customers, not code.

---

## 7. GitHub issue plan

Reviewed against the open and closed issues on `abdallahtahboub/Testo` on 2026-11-15. **No issues have
been created or changed yet.** Issue creation and closure are visible to others, and this roadmap is not
accepted.

| Issue | Title (as it stands) | Recommendation |
|---|---|---|
| #26 | Architecture review: validate real autonomous E2E QA vertical slice | Leave open. This document is its follow-up; link it after acceptance |
| #3 | Milestone 3 — Public website | **Review.** Built per roadmap Milestone 4 and waiting for review. Close only after the owner's review, not before |
| #4 | Milestone 4 — App shell + module registry | **Review.** The shell and registry exist (inventory). Same rule as #3 |
| #5 | Milestone 5 — Testing-journey modules | Keep. Renumber against the accepted roadmap |
| #6 | Milestone 6 — Live E2E Test Run view | **Review.** Partly built as the execution slice (M3.3) |
| #7 | Milestone 7 — Polish | Keep |
| #9–#15 | Milestone 5.1–5.7 module issues | Keep, but these are children of #5 and should be moved under it or closed as superseded once #5 is revised |

**New issues, after acceptance** (one per milestone in §6, plus the gaps that need their own tracking):
G1 proof of origin, G2 loopback limit, G3 container per run, G4 kill switch, G6 usage ledger,
G8 reservation, G9 plan catalog, G12 retention, G13 email, G16 backup and restore, G19 compliance
documents. Each new issue gets the fields the directive asks for (business value, risk, priority,
current state, acceptance criteria, tests, dependencies, definition of done).

**Labels.** Propose `commercial`, `safety-gate`, `pilot-gate`. Confirm before creating them.

---

## 8. Commercial readiness checklist

What must be true before accepting paying customers. Status is as of this commit.

| # | Condition | Status |
|---|---|---|
| 1 | Users cannot reach app pages without signing in | **Met** (D-1) |
| 2 | Tenant isolation tested for every data path that exists | **Partly met**. Tests cover runs and artifacts; no second-organization test for planning |
| 3 | Customer targets are verified as theirs before any run | **Not met** (G1) |
| 4 | Browser runs against customer targets are isolated in a container | **Not met** (G3) |
| 5 | Loopback and private reach are refused for every stored target | **Partly met**: refused for non-configured targets; loopback exception needs a stored-target test (G2) |
| 6 | An operator can stop all execution and any single organization's execution | **Not met** (G4) |
| 7 | Concurrency and quotas cannot be bypassed by parallel requests | **Not met** (G7, G8) |
| 8 | Every billable unit is recorded once, idempotently | **Not met** (G6) |
| 9 | Plans and entitlements are versioned | **Not met** (G9) |
| 10 | Cost per run is traceable to usage | **Not met** (G10) |
| 11 | Artifacts expire and are deleted by policy | **Not met** (G12) |
| 12 | Invitations are delivered by email | **Not met** (G13) |
| 13 | Backup and restore procedure tested once | **Not met** (G16) |
| 14 | Subprocessor list, DPA and retention schedule exist and were reviewed by counsel | **Not met** (G19); flagged for legal review |
| 15 | Accessibility statement matches tested behaviour | **Partly met**: the statement lists limitations, and no screen-reader user has tested the product |
| 16 | Planner reliability measured with a model key, and published next to the baseline | **Not met**: the re-run is pending |
| 17 | Pricing set against measured cost | **Not met**; pricing values in the directive are hypotheses |

No checklist item is a claim about compliance. Items 14 and 15 need documents and people, not code.

---

## 9. Open decisions for the owner

These change what the next milestone should be, so they are questions rather than assumptions:

1. **Product name. Decided 2026-11-15: *Autestiq*, adopted on the owner's decision, with clearance
   incomplete.** The rename is done on this branch: the product, namespaces, projects and paths now say
   Autestiq, and the GitHub repository is still named Testo. What was checked, and what was not:
   - **Checked:** one EUIPO search for the exact term *Autestiq*, with no trademarks found. The search
     mode and Nice classes were not recorded, so this does not show that classes 9 and 42, software
     and testing services, were covered.
   - **Not checked:** DPMA (Germany, where the first customers are likely to be), USPTO, similar-sound
     search on EUIPO, and any registration for the Grazitti product *AutoTestIQ*.
   - **Unresolved conflict:** AutoTestIQ is one letter away and in the same category. Buyers may confuse
     the two, and a trademark attorney's opinion is still recommended before the product is sold under
     this name.

   Earlier findings (2026-11-15), kept for the record: *Testo* is a registered trademark of Testo SE & Co. KGaA for measuring apparatus, in
   the same test-and-measurement space, so it was a poor choice. *Autestiq* has no exact match, but it is
   one letter from **AutoTestIQ** (Grazitti), a live AI software-testing product in the same category,
   and it is close to **AutonomIQ**, another autonomous-testing company. A clearance search was needed
   before the rename.
   (2026-11-15): *Testo* is a registered trademark of Testo SE & Co. KGaA for measuring apparatus, in
   the same test-and-measurement space, so it is a poor choice. *Autestiq* has no exact match, but it is
   one letter from **AutoTestIQ** (Grazitti), a live AI software-testing product in the same category,
   and it is close to **AutonomIQ**, another autonomous-testing company. A clearance search is needed
   before the rename. Until then, nothing is renamed.

   **Check run 2026-11-15, not a clearance.** Web search found no registration for *Autestiq* on
   USPTO, EUIPO or DPMA results, though the tool does not query EUIPO or DPMA directly, so this is not
   conclusive. No DNS record for autestiq.com, .io, .de or .ai (an unregistered domain and an
   unpublished one look the same from here). No GitHub user, organization or repository with the name.
   Near marks found: AUTOTECHIQ (pending, automotive repair information, 2022–2023), AUTOTEQ (pending,
   2025), AUTOIQ and AUTHENIQ. No registration found for the Grazitti product *AutoTestIQ*; whether it is
   registered is unknown, and an unregistered product name can still be protected in use. Judgement:
   AutoTestIQ is the same category as ours, so the conflict is real for buyers even before a legal
   question. Recommend a trademark attorney's clearance opinion before any name is adopted.
2. **Accept §6.** **Accepted 2026-11-15.** Recorded in [`roadmap.md`](roadmap.md) under Order of work.
3. **Pilot scope.** Which first design partner, and do they accept a demo target only, or do they need
   their own staging URL soon? A staging URL makes G1 to G3 urgent.
4. **Billable failures. Decided 2026-11-15: option B.** Runs that reach the customer's application are billable; our proxy, worker and planner failures are not. The owner first confirmed both A and B, which exclude each other, and then chose B when asked. Options considered:
   - **A. Bill every started run** (simplest, what most per-minute test services do). Customers pay for
     what they started, and our own faults are the cost of doing business. Risk: a customer disputes a
     bill for our outage.
   - **B. Bill only runs that reached the target** (recommended). A run counts once the browser reaches
     the customer's application. Failures of the target, the test, or the customer's configuration are
     billable. Failures from our proxy, worker crash, or planner outage are not. Risk: needs a clear
     "reached" definition and the ledger to record it, which 3.6 builds.
   - **C. Bill only passing runs.** Rejected: it rewards tests that never reach a verdict and makes
     failures free, which invites abuse.
   Recommendation: B. It is what the ledger in 3.6 can actually measure, and it is the easiest to
   explain on an invoice. Needs the owner's decision before 3.6 begins.
5. **Retention periods.** 90 days for screenshots is the ADR-0018 figure. Confirm before any real customer data.
6. **Billing provider. Confirmed 2026-11-15:** Stripe Billing, test mode only. An ADR must be written
   before any code calls it, and no live charges until the security and financial-correctness gates pass.
7. **Data region.** Which region for the first customers? `DataRegion` exists but only one value is in use.
8. **Model re-run.** Who will run the planner benchmark with a model key, and when?

---

## 10. What was not checked

- The test suite was not run against this commit in this session. Run `dotnet build` and `dotnet test`
  before accepting any "passing" statement here.
- No load test, no security scan beyond reading the code paths named above, no dependency vulnerability scan.
- No legal review. Items marked for legal review are not reviewed.
- Pricing and margin numbers are the directive's hypotheses, not measurements.
