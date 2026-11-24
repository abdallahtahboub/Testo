# Data architecture

> **Status: accepted by the owner on 2026-11-24** (ADR-0038). Accepted as the design; **its §5 to §9 are not built**,
> and each is tracked where §11 says.
>
> **Provider-neutral by design.** Hosting is deferred to the deployment and go-live stage, and whether customer data
> must stay in the EU is undecided and to be studied (owner, 2026-11-24, `docs/open-questions.md`). So this document
> names **kinds** of store and the properties each must have, never a product or a region. Where a choice depends on
> hosting or residency, it says so.
>
> **Built and designed are kept apart.** §2 and §3 describe what exists today, checked against the code on
> 2026-11-24. Everything from §4 on is designed, not built, unless it says "built".

Related: [`domain-model.md`](domain-model.md) (the entities and how isolation is enforced),
[`secure-onboarding-and-privacy.md`](secure-onboarding-and-privacy.md) (the binding rules on customer data),
[`security-register.md`](security-register.md) (R-4, R-7, R-17, R-18, R-19),
[`secure-onboarding-gap-analysis.md`](secure-onboarding-gap-analysis.md) (G-2, G-5),
[`ai-learning/data-architecture.md`](ai-learning/data-architecture.md) (lab and experience data, which is separate).

---

## 1. Principles

1. **One system of record.** PostgreSQL holds every fact the product decides on (ADR-0011, ADR-0012). Other stores
   hold bytes the database points to (screenshots) or keys the database must never hold (the key ring).
2. **Every piece of customer data has a class, an owner, a retention and a deletion path**, before it is stored. A
   new kind of data without all four is not merged.
3. **The tenant boundary is in every store**: a row's `organization_id`, an object key's first segment, a log line's
   organization field. Nothing customer-owned is addressable without it.
4. **Big and short-lived things leave the database first.** Rows that decide things stay; blobs that are only looked
   at move to object storage when their volume says so (§6).
5. **Deletion is proved, not assumed.** A deletion is done when a test finds no row, file, link, log entry or
   analytics row that names the deleted item, and the backups that still hold it expire within a stated time.
6. **Analytics never carries content.** Counts, durations, states and costs, yes; page text, prompts, screenshots,
   descriptions and credentials, never.
7. **Decided later, designed now.** Hosting and residency are not chosen, so nothing here depends on one provider:
   PostgreSQL, an S3-compatible object store, a managed key service and structured logs exist on every candidate.

## 2. The stores today (built)

| Store | What it holds | Where it is today | Tenant boundary |
|---|---|---|---|
| **PostgreSQL**, one database for every tenant (ADR-0012) | Every entity: identity, projects, tests, runs, usage, costs, audit | One server; development uses a local one | `organization_id` on every row inside the boundary; EF query filter and save guard; composite foreign keys |
| **Artifact store** (`IArtifactStore`, `FileSystemArtifactStore`) | Screenshots per run step | A local directory | Every key begins with the organization; served only through signed links (`ArtifactLinks`) |
| **Data Protection key ring** (`DataProtection:KeyDirectory`) | The keys that encrypt test-account values (ADR-0033) | A local directory, required outside development | Purpose per organization and environment |
| **Application logs** | Events, warnings, errors from `[LoggerMessage]` methods | The console (stdout); nothing collects or keeps them | None structured yet: messages carry ids, never page text or secrets |
| **The AI provider** | What a planning call sends, for as long as the provider keeps it | Anthropic, under its terms | Sent only for projects with the AI permission (G-1); recorded per attempt |
| **Backups** | — | **None** (G16 in `commercial-readiness.md`) | — |
| **Benchmark and lab files** | Reports, recorded plans, experiment records | The repository (`docs/`, `tools/`) | Our own applications only; no customer data |

## 3. Data inventory

The owner's classes (`secure-onboarding-and-privacy.md`): **M** approved metadata, **A** confidential application
information, **C** credentials, **P** personal or regulated data, **E** execution evidence. "Product" is our own data,
not a customer's.

| Data | Store (table or place) | Class | Kept today | Deleted today | Proposed retention |
|---|---|---|---|---|---|
| Users, sign-in credentials (hashed) | `users`, Identity tables | P, C | Forever | Never | While the account exists; deleted with it |
| Memberships, invitations, password resets | `memberships`, `invitations`, `password_resets` | P | Forever | Never (resets expire, rows stay) | Expired invitations and resets: 30 days |
| Audit log | `audit_entries` (append-only, R-4) | P, M | Forever | Never | **Needs counsel**; proposed 2 years, longer where law requires |
| Organizations, projects, environments, origin proofs | `organizations`, `projects`, `environments`, `origin_proofs` | M (an origin can be A) | Forever | An environment can be removed if no test uses it | While the project exists |
| Test-account values | `environment_secrets` (encrypted), key ring | C | Until removed | Removable by Owner or QA lead | Until removed; deleted with the environment |
| Test descriptions, stated expected results | `test_cases.description`, `expected_results` | A (can contain P) | Forever | Never | While the test exists |
| **What exploring saw** | `test_cases.snapshot` | A, possibly P | **Forever** | **Never** | The project's evidence policy (90, 30 or 7 days, or none), then cleared; the plan stays |
| **What was sent to the model** | `test_cases.sent_to_model` | A, possibly P | **Forever** | **Never** | As the snapshot |
| Plans, notes, approvals | `test_cases.plan`, `notes`, `approved_*` | A | Forever | Never | While the test exists |
| Planning attempts and outcomes | `planning_attempts`, `planning_attempt_outcomes` (append-only) | Product, M | Forever | Never | As usage and cost records |
| Runs and steps | `test_runs`, `test_run_steps` | A (step messages can quote page text) | Forever | Never | 13 months, then summarized (§7) |
| Screenshots | Artifact store + `artifacts` rows | E, possibly P | Project policy (built, ADR-0031) | Hourly job, files then rows (built) | As built |
| Usage ledger, run costs | `usage_events`, `run_costs` (append-only) | Product, M | Forever | Never | **Needs counsel**: accounting records may have to be kept for years |
| Plan catalog, price book | `plans`, `plan_versions`, `price_versions`, `unit_prices` | Product | Forever | Never | Forever (versions never change, ADR-0028, ADR-0029) |
| Application logs | stdout | M (ids only) | Not kept | — | 30 days in the log store |
| Backups | — | All of the above | — | — | §8 |

**Two rows are the urgent ones**: what exploring saw and what was sent to the model are customer application
content, possibly personal, kept forever with no deletion path. G-5 already lists them; §5 says how they would be
handled.

## 4. Data flow

```
 Customer's staging site ──(sealed worker, egress proxy)──► worker ──► snapshot, step results, screenshots
                                                                          │
                                   PostgreSQL ◄────────────────────────────┤ rows
                                   artifact store ◄────────────────────────┘ files (org/run/…)
                                       │
         planning step ── snapshot + description + stated results ──► AI provider (only with the AI permission)
                                       │                                      │
                                   sent_to_model, plan, notes, attempt ◄──────┘
                                       │
             people (test page) ◄──────┤ via the tenant filter and signed links
             analytics views (R6) ◄────┘ counts, durations, states, costs only
             backups ◄─────────────────── PostgreSQL + key ring + artifact store (§8)
```

Credentials never leave the worker towards the model: exploring replaces their values before anything is described
(ADR-0033), and the validator accepts secrets by name only.

## 5. Lifecycle and deletion (designed, not built)

1. **Snapshots and planning payloads follow the project's evidence policy.** The hourly retention job (ADR-0031)
   also clears `snapshot` and `sent_to_model` on tests planned before the cut-off, keeping the plan, its notes and
   the attempt rows. A project that keeps no evidence clears them once the plan is stored. Consequence: a test whose
   snapshot is cleared cannot be re-planned from it; changing its expected results explores again.
2. **Project and organization deletion**, which do not exist yet: removes every row of the project or organization,
   every object under its key prefix, its key-ring purpose's values, and its analytics rows; audit entries are kept
   only as counsel requires, with personal fields removed.
3. **Deletion verified end to end**: a test that creates every kind of data for an organization, deletes it, and
   finds nothing that names it in any table, the artifact store or the analytics views.
4. **Backups expire** within §8's retention, so deleted data leaves them within a known time, stated to customers.
5. **Logs** carry ids, never content, so a deleted item's log lines name nothing readable; they expire at 30 days.

## 6. Volume estimates and when the design changes

Measured figures where we have them, otherwise stated assumptions.

**Per item** (from the code and the benchmark):
- a screenshot (viewport, PNG): ~20–150 KB; a run has one per step, 3–15 steps;
- what exploring saw: ~30–100 KB of JSON (the planner reads 7,000–24,000 tokens of it, ADR-0020's reports);
- what was sent to the model: about the same again, per attempt;
- a run with its steps: ~2–5 KB of rows; a usage event or cost row: ~0.3 KB.

**Three sizes of the business** (assumptions to revise with real use):

| | Pilot | 100 customers | 1,000 customers |
|---|---|---|---|
| Runs a day | 50 | 20,000 | 200,000 |
| New or re-planned tests a day | 10 | 1,000 | 10,000 |
| Database growth a day | ~2 MB | ~250 MB | ~2.5 GB |
| Database after a year, without §5 | < 1 GB | ~90 GB | ~900 GB |
| Database after a year, with §5 (snapshots cleared after 90 days) | < 1 GB | ~55 GB | ~550 GB |
| Screenshots held at 90 days | ~2 GB | ~700 GB | ~7 TB |

**What follows:**
- **PostgreSQL stays the system of record at every size here.** One well-provisioned primary handles hundreds of GB
  and this write rate; no sharding or second database is proposed.
- **Screenshots need object storage from about 100 customers**, and before that if more than one machine serves the
  product. `IArtifactStore` is already the seam.
- **Triggers to change, each measured, not guessed:**
  - `test_run_steps` or `usage_events` beyond ~50 GB, or their monthly queries slower than their budget →
    partition them by month (PostgreSQL native partitioning);
  - snapshots and payloads beyond ~20 % of the database → move them to the artifact store, keeping a key in the row;
  - analytics queries visible in the primary's load → a read replica for them (§7);
  - an organization needing physical separation → its own database (R-7 accepts this as a future option).
- **Hot queries** (the queue claim, the stale-run reap, the monthly AI spend, the retention sweep) are measured with
  query plans on representative data before the pilot (review finding 4.3); indexes follow the measurements.

## 7. Analytics (designed; views are Milestone 5.1 R6)

1. **Now (Milestone 5.1 R6):** PostgreSQL views over the operational tables, one row per run and per planning
   attempt: durations, states, verdicts, corrections, cost. No content columns, tested column by column.
2. **When analytics load shows on the primary:** the same views on a read replica.
3. **A warehouse only when one of these holds:** product analytics across years that the operational schema should
   not keep; joining with billing or CRM data; reporting customers run themselves at volume. Fed by change data
   capture or a nightly export **of the analytics views only**, so no content can reach it. Not before the pilot
   gives a reason (review finding 4.5).
4. **Run history beyond 13 months** is kept as daily aggregates per project (counts by status, durations, cost),
   and the detailed rows are deleted.

## 8. Backups and recovery (designed; the mechanism waits for hosting)

| Property | Proposal | Why |
|---|---|---|
| Database backup | Continuous (point-in-time recovery) plus a daily full backup | Every candidate managed PostgreSQL offers it |
| Recovery point (RPO) | ≤ 15 minutes in production; ≤ 24 hours for the pilot rehearsal | Losing a day of a pilot is acceptable; of production, not |
| Recovery time (RTO) | ≤ 4 hours | A test tool down for a morning is survivable; days are not |
| Backup retention | 35 days | Long enough to notice a mistake; short enough that deleted data leaves within a stated time |
| **Key ring** | Backed up **with** the database, separately encrypted, restored together | Without it, every stored test-account value is unreadable (ADR-0033); with it in the same place as the data, one leak reveals both |
| Artifact store | The store's own durability and versioning, no separate backup | Screenshots are evidence, not records the product decides on; losing one loses a picture, not a verdict |
| Encryption | At rest by the provider; test-account values additionally by the application (ADR-0033); in transit always TLS | |
| Proof | A restore rehearsed before the pilot and then quarterly: database and key ring together, a stored secret decrypts, a screenshot link resolves or is reported deleted (Milestone 5.1 R3) | An untested backup is not a backup |

## 9. Access to data (designed, not built; R-19)

- **Three database roles**: migration (owns the schema), application (data only; no DDL; no `UPDATE` or `DELETE` on
  `audit_entries`, `usage_events`, `run_costs`, `planning_attempts`, `planning_attempt_outcomes`), operator (read,
  plus the operator commands' writes).
- **No person reads customer content directly** in production; operators use the operator tool, which is audited.
- Row-level security in PostgreSQL is evaluated separately (R-19), with a plan for the background jobs that cross
  organizations on purpose.

## 10. Residency: what the study needs from this document

The residency decision is open. This design keeps it open and gives the study what it needs:
- **every store and processor in §2 has one location to choose**: database, its backups, the artifact store, the key
  ring, logs, and the AI provider's processing location, which is the one most likely to sit outside the EU;
- `Organization.DataRegion` exists and is chosen at creation; nothing routes by it yet. If a region per customer is
  needed, each store above needs one instance per region and the control plane must route by it;
- the classes in §3 say what would move: C and P are the strictest, E and A next, M the least.

## 11. What is not built, and where it is tracked

| Item | Tracked in |
|---|---|
| Lifecycle of snapshots and planning payloads; project and organization deletion; verified deletion | G-5 (gap analysis) |
| Backups and a rehearsed restore | Milestone 5.1 R3; G16 (`commercial-readiness.md`) |
| Object storage for screenshots | ADR-0031 (waits for hosting) |
| Least-privilege database roles; RLS evaluation | R-19 |
| Composite environment key on tests | R-18 |
| Analytics views | Milestone 5.1 R6 |
| Query plans for the hot queries | `open-questions.md` (technical debt) |
| Retention of the audit log and of accounting records | `open-questions.md` (needs counsel) |
| Log collection and its retention | Waits for hosting |
