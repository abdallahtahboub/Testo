# Pilot-readiness review, 2026-11-24 (dated record)

> **A dated record, not a living document.** It says what an independent engineering review found and what was
> verified against the code on 2026-11-24. It will not be updated as findings are fixed: each finding has one
> authoritative home, linked in the table, and that home carries its current state.

## What was reviewed, and what was verified

- **The review** (owner's brief of 2026-11-24) examined `develop` at `0fa6ebde545640b623a06a7aa3ffa80c15fe55b9`,
  with GitHub Actions green at that revision. It read source, tests, configuration and documentation. It did not run
  the suite or verify a deployment.
- **This verification** ran against `develop` at `72a4cada9acf13e4cbebac0dc2558727b4b2729a` (2026-11-24, after #76:
  the AI learning strategy, documents only). The changes since the reviewed revision are PRs #75 (ADR-0035 step 2,
  the prompt's `notTested` list) and #76 (documents). Neither touches a finding below except the benchmark's
  currency (3.1).
- **Checked here:**
  - the code, at the file and line given;
  - `dotnet build`, clean with zero warnings, on the Experiment 0001 branch, which is `develop` plus a benchmark-tool
    change;
  - CI green on #76's head, `3c60395`. Its single job builds, builds the worker image, runs every test project with
    `--max-parallel-test-modules 1`, and runs the benchmark's `--self-check`;
  - the repository's visibility through GitHub search: **public**.
- **Not checked here:**
  - branch protection and required checks, because this session's GitHub access cannot read repository settings;
  - any deployment, because none exists;
  - query plans, because there is no representative data.

## Branch and release state (verified)

- `develop` is **184 commits ahead of `main` and 1 behind**.
- There are **no tags and no GitHub Releases**.
- `CONTRIBUTING.md` defines `release/x.y.z` and `hotfix/x.y.z` branches into `main`, each tagged, but none has ever
  been cut.
- `LICENSE` calls the source "proprietary and confidential" while the repository is public. **That is an owner
  decision.** Visibility was not changed.

## Findings

Dispositions:
- **Confirmed**: the finding holds against current code.
- **Already addressed**: fixed or recorded before this review.
- **Runtime**: it needs runtime verification that this session cannot do.
- **Deferred**: real, but not needed before the rehearsal.

Priorities:
- **P0** is needed before a staging rehearsal with synthetic data.
- **P1** is needed before a pilot with real customer data.
- **P2** is needed later.

| # | Finding | Evidence (current code) | Disposition | Priority | Authoritative home |
|---|---|---|---|---|---|
| 1.1 | No reproducible staging release: no runbook, no deployed migration path, no health checks | Migrations run only inside `DevelopmentData.EnsureSeededAsync` (`src/Autestiq.Application/DevelopmentData.cs:54`), called only in development (`src/Autestiq.Web/Program.cs:129-132`). `tools/Autestiq.Operator` has no migrate command. No `MapHealthChecks` anywhere | **Confirmed** | P0 | Roadmap, Milestone 5.1 (R1–R3) |
| 1.2 | A deployed host needs real email | Only `LoggingInvitationNotifier` exists (`src/Autestiq.Application/ApplicationServiceCollectionExtensions.cs:56`). `NotifierStartup.EnsureLinksReachPeople` refuses to start with it outside development (`src/Autestiq.Web/Program.cs:126`). **Today no non-development deployment can start at all** | **Confirmed** | P0 | Roadmap, Milestone 5.1 (R2); open questions (provider) |
| 1.3 | Hosting, evidence storage, key ring; restore rehearsal | The key ring is a directory (`src/Autestiq.Web/Security/SecretProtection.cs:23-41`). Evidence is on local disk (ADR-0031). Hosting is undecided. No restore has ever been rehearsed | **Confirmed**; hosting already an open question | P0 for the restore rehearsal; P1 for object storage and a key service | Open questions (hosting); roadmap, Milestone 5.1 (R3) |
| 1.4 | Release procedure, branch protection, visibility | See above. Protection is not readable from here | **Confirmed** (release); **Runtime** (protection); **Owner decision** (visibility) | P0 | Open questions |
| 2.1 | AI data minimization (G-2) still open | The whole `SiteSnapshot` goes to the planner (`src/Autestiq.Application/Testing/PlanningDispatcher.cs:137-156`). Credential values are kept out by `SecretRedaction` in the worker and by the validator's names-only rule, tested in `tests/Autestiq.Application.Tests/Projects/EnvironmentSecretTests.cs`. Planning logs hold counts and validator errors, never the prompt (`src/Autestiq.Application/Planning/PlanningService.cs:166-175`) | **Confirmed** open (G-2) | P1. Not needed for synthetic data | Gap analysis G-2 |
| 2.2 | Data lifecycle beyond screenshots | Retention covers screenshots only (`src/Autestiq.Application/Execution/EvidenceRetentionService.cs:111-117`). `TestCase.Snapshot` and `TestCase.SentToModel` (the planning payload) are kept forever. There is no project or organization deletion in `ProjectService`. Log and backup retention are undefined (no hosting) | **Confirmed** | P1 | Gap analysis G-5 |
| 2.3 | A planning crash leaves a test in Planning | `PlanNextAsync` sets `Planning` and saves (`PlanningDispatcher.cs:87-98`), then calls the provider. Nothing ever selects `Planning` again: the query takes only `Exploring` (`:64`). The index on `(Status, PlanningStartedAt)` exists (`src/Autestiq.Persistence/Configurations/TestCaseConfiguration.cs:27`) but no reaper uses it. A stuck test also refuses edits to its expected results (`TestCaseService.cs:174`). A crash after the provider answered loses the cost record, so **the budget undercounts** | **Confirmed** | P0 | Security register R-17 |
| 2.4 | AI budget is read, then spent, without a reservation | `AiBudget.LeftAsync` sums `TestCase.ModelCost` (`src/Autestiq.Application/Testing/AiBudget.cs:18-36`). The cost is written only after the call (`PlanningDispatcher.cs:162`). Two planning instances can both see the same room. One instance plans one test at a time | **Confirmed** | P0 with one planner enforced; P1 before more than one | Security register R-17 |
| 2.5 | Test → environment foreign key on `EnvironmentId` alone | `TestCaseConfiguration.cs:38-41`. The project relationship is composite on `(OrganizationId, ProjectId)` (`:31-35`); the environment's is not. Creation checks `candidate.ProjectId == projectId` under the tenant filter (`TestCaseService.cs:249-251`), so **no reachable cross-tenant path was found**: a schema invariant, not a live bug | **Confirmed** (schema) | P1 | Security register R-18 |
| 2.6 | Tenant boundary: filters bypassed, controls only in EF | 49 `IgnoreQueryFilters()` calls in 15 files under `src/`, each for a background job, sign-in or the product-wide budget. One connection string and one database role for the application, migrations and operator. No PostgreSQL RLS. The audit log is append-only only through EF (R-4) | **Confirmed** | P1 (roles); P2 (RLS, database-level append-only) | Security register R-19 (and R-4, R-7) |
| 3.1 | Benchmark figures predate the current prompt | ADR-0035 step 2 (#75) changed the prompt. The re-measurement was started by the owner on 2026-11-24; its results are not yet in the repository | **Confirmed**; in progress | P0 (on the exact release candidate) | ADR-0020; open questions |
| 3.2 | Score each stated expected result separately | Benchmark verdicts are per run (`tools/Autestiq.Benchmark`). Stated results are checked and labelled per check (ADR-0034, `ExpectedResults.EvidenceFor`) but not tallied per result | **Confirmed** | P0 | Roadmap, Milestone 5.1 (R5) |
| 3.3 | The export misses the test-account guard | `TypedSecretChecks` runs in the worker only (`src/Autestiq.Worker/TypedSecretChecks.cs`). `PythonExporter` has no equivalent (`src/Autestiq.Export/PythonExporter.cs`) | **Already recorded** (open questions, ADR-0033); **confirmed** still open | P0 (before export is a pilot deliverable) | Open questions (technical debt) |
| 4.1 | Keep the architecture | Dependency rules are enforced by `DependencyRulesTests` | **Already addressed** | — | `docs/architecture.md` §4 |
| 4.2 | `RunDispatcher` size | 1,415 lines (`src/Autestiq.Application/Execution/RunDispatcher.cs`) | **Confirmed**; deferred until its failure paths are covered | P2 | Open questions (technical debt) |
| 4.3 | Query plans for the queue claim, stale-run scans, monthly AI spend, retention | Indexes exist (for example `TestCaseConfiguration.cs:27-28`), none of them measured | **Runtime** | P1 | Open questions (technical debt) |
| 4.4 | Pilot analytics | Usage ledger and cost rows exist (ADR-0027, ADR-0029). No view or aggregate | **Confirmed** absent | P1 (the rehearsal's measures) | Roadmap, Milestone 5.1 (R6) |
| 4.5 | Defer the warehouse, training, billing and the six shell modules | Matches the roadmap and ADR-0036 | **Already addressed** | — | Roadmap |

Accuracy fixes made with this record, because current-state summaries had drifted:
- `README.md` listed downloads and the secret store as not built, and said no outside application was measured.
- `docs/architecture.md` still described a mock-data frontend phase.
- The roadmap inventory's run row still said that no screen asks for a customer target.

## Owner decisions this review raises

1. **Repository visibility**: public, while `LICENSE` says confidential. Make it private, or change the licence.
2. **Hosting**, which decides evidence storage, the key service, backups and real prices. It was already open.
3. **Email provider**, for invitation and reset mail. No deployed host can start without one.
4. **Approve Milestone 5.1** (roadmap) as the pre-pilot milestone, or amend it.

The milestone itself is in [`docs/roadmap.md`](../roadmap.md) §Milestone 5.1.
