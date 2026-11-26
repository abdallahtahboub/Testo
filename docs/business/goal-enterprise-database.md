# Goal: an enterprise-grade database

> **Status: a goal, not built.** The owner asked for it on 2026-11-26: the database should eventually look like a
> real enterprise database, with fast queries, sound pagination and indexing, strong security and data integrity.
> Nothing below exists unless [`../database-schema.md`](../database-schema.md) says so. Each step is planned in detail
> and approved before it starts, as every milestone is. It sits in the Enterprise programme as **E11**
> (`../roadmap.md`).

## Where we start

The schema is sound for a product of its age (`database-schema.md`):
- UUID v7 keys;
- the tenant boundary in every row;
- composite foreign keys for the main parents;
- unique keys where the product relies on them;
- idempotent ledgers and optimistic concurrency;
- secrets that are never stored readable.

What an enterprise review would still flag (`database-schema.md` §8):
- **Integrity in the application only.** Several rules live in the application, not the database: the values of a
  status, append-only, and tenancy for every reference.
- **One all-powerful role, and no row-level security.**
- **Unscaled reads.** Offset pagination with full counts, and indexes never checked against a real query plan.

## What "enterprise-grade" means here

Each area has an outcome, the steps that reach it, and a test that proves it. The test is part of the step: a rule
that can be checked by a test is checked by one (CLAUDE.md).

### D1 — Integrity enforced by the database

**Outcome:** a bad write is refused by PostgreSQL even if it comes from outside the application: an operator's SQL,
a future service, or a bug.

| Step | Proof |
|---|---|
| A CHECK constraint on every enumeration column (statuses, roles, kinds, categories), generated from the C# enum so the two cannot drift | A test inserts each wrong value through raw SQL and is refused; a test fails when an enum gains a value without a migration |
| Composite `(organization_id, …)` foreign keys for every reference inside the boundary: environments from tests and secrets (R-18), test case and rerun from runs, the screenshot from a step | A test writes a reference to another organization's row through raw SQL and is refused |
| An ADR that records the references left without a foreign key on purpose (the ledgers and the audit log outlive what they count) and why | The ADR, and a test listing every reference column without a foreign key against that ADR's list |
| CHECK constraints for the rules a value must keep: amounts and quantities not negative, `finished_at ≥ started_at`, an outcome per attempt | Raw-SQL tests as above |
| Every column's nullability reviewed against the domain | The review, recorded in this document |

### D2 — Security in depth

**Outcome:** a leaked application credential, or a bug that forgets the tenant filter, still cannot read another
customer's data or rewrite history.

| Step | Proof |
|---|---|
| Three roles (R-19, `data-architecture.md` §9): **migration** owns the schema; **application** reads and writes data only, without DDL and without `UPDATE` or `DELETE` on the append-only tables; **operator** reads, plus its own commands. The application never connects as a superuser | A test connects as the application role and is refused DDL and changes to the ledgers |
| **Row-level security** as a second tenant boundary under the EF filter: a policy on every table inside the boundary, keyed to a per-transaction setting. The background jobs that cross organizations on purpose get their own role. Needs the owner's approval (an architecture change) | A test runs a query with the EF filter removed and gets only its own organization's rows |
| Append-only also in the database: no `UPDATE`/`DELETE` grant, and a trigger as a backstop | A raw-SQL update of an audit entry fails |
| TLS required for every connection; encryption at rest by the provider; application-level encryption of test-account values kept (ADR-0033) | The connection string refuses to work without TLS outside development |
| Database activity logged for the migration and operator roles; failed sign-ins to the database alerted | Waits for hosting (E9) |
| A rotation procedure for every database credential | Rehearsed in the runbook |

### D3 — Fast queries, measured

**Outcome:** every query the product makes on a hot path has a time budget, and a test or a measurement shows it is
met on data of realistic size.

| Step | Proof |
|---|---|
| **A query budget per hot path:** list pages p95 ≤ 100 ms; the queue claim, the stale-run reap and the retention sweep ≤ 50 ms each; the monthly usage sum ≤ 50 ms | The budgets written in this document, and measured as below |
| **A seeded data set at the "100 customers" size** of `data-architecture.md` §6 (millions of runs, steps and usage events), generated deterministically | A tool command that builds it |
| **Query plans checked** (`EXPLAIN (ANALYZE, BUFFERS)`) for every hot query on that data set; the plans kept beside this document | A report, regenerated when a hot query changes |
| `pg_stat_statements` on in every environment, and the slowest queries reviewed each release | The release checklist |
| **An index policy:** every foreign key indexed; every list's filter and order covered by one index; unused indexes removed after a measured period; indexes created `CONCURRENTLY` on large tables | A test that every foreign key column leads an index; the review |
| No N+1 queries: a page makes a bounded number of round trips whatever its size | A test that counts the commands a page's service sends |
| A statement timeout for the application role, so one bad query cannot hold the database | Configuration, and a test that a long query is cancelled |
| Connection pooling sized and measured (Npgsql's pool now; a pooler such as PgBouncer only when several machines serve the product, an infrastructure decision for the owner) | The load test of E9 |

### D4 — Pagination as a standard

**Outcome:** every list pages the same way, with a hard upper limit, and the long lists stay fast at any depth.

| Step | Proof |
|---|---|
| A maximum page size on every service that pages, enforced by the service, not the page (today only the audit log clamps) | A test per service: a page size of 100,000 comes back clamped |
| **Keyset (cursor) pagination for lists that only grow:** the audit log, runs, usage, and later defects and results. "The next 50 after this row", by an index on `(organization_id, at DESC, id DESC)`, so page 1,000 costs what page 1 costs. A `CursorPage<T>` contract beside `PagedResult<T>`, with an opaque cursor that cannot be used to read another organization | A test: the same row never appears on two pages while rows are added between requests; a cursor from one organization is refused in another |
| Offset pagination kept for short, bounded lists (members, projects, environments) | The list of which is which, in this document |
| Counts that do not read the whole table: exact up to a cap ("more than 10,000"), or the planner's estimate where an exact figure is not needed | A test of the capped count |

### D5 — Scale without a rewrite

**Outcome:** growth is met by planned, measured steps. Each is triggered by a figure, never by a guess
(`data-architecture.md` §6).

| Step | Trigger |
|---|---|
| Monthly partitions for `test_run_steps`, `usage_events`, `run_costs` and `audit_entries` (native PostgreSQL partitioning), with old partitions archived or dropped by retention | A table past ~50 GB, or its monthly query past its budget |
| Large documents (`snapshot`, `plan`, `sent_to_model`) moved from `test_cases` to the artifact store, with a key in the row | Those columns past ~20% of the database |
| A read replica for analytics and the operator's reports | Analytics load visible on the primary |
| A database of its own for an organization that needs physical separation | A customer's contract (R-7) |

### D6 — Operations

**Outcome:** the database is run the way an enterprise customer's security review expects.

| Step | Proof |
|---|---|
| Backups with point-in-time recovery and a quarterly rehearsed restore, the key ring with them (`data-architecture.md` §8) | The restore rehearsal's record |
| Migrations reviewed for locking: no table rewrite or blocking index build on a large table; expand-then-contract for renames | A checklist in `CONTRIBUTING.md`, and a test that flags blocking DDL in a new migration |
| Monitoring of slow queries, locks, bloat, autovacuum, replication lag and connections | Waits for hosting (E9) |
| A data dictionary generated from the schema: every table and column with its purpose and data class (`data-architecture.md` §3) | A test that fails when a column has no entry |

## Order, and what comes first

1. **D1 and D4's first step together** (integrity constraints, the missing foreign keys, the ADR on references left
   open, a maximum page size everywhere). Small, migrations only, no new infrastructure. Recommended before the first
   pilot, because constraints are cheapest to add while the tables are small.
2. **D3's measurement** (the seeded data set, the query plans, the budgets). It tells us which indexes and which
   lists need work, instead of guessing.
3. **D2's roles**, then **row-level security** with the owner's approval. Before real customer data (the readiness
   decision in `../roadmap.md`).
4. **D4's keyset pagination** for the audit log and runs, when D3 shows the need or the lists pass a set size.
5. **D5 and D6** at the go-live stage, with hosting (E9).

## Rules every step keeps

- **No new infrastructure dependency without the owner's approval** (a pooler, a replica, a warehouse, an extension
  the host may not offer).
- **The tenant boundary is never weakened** to make a query faster.
- **Measured, not guessed.** An index or a partition is added because a plan or a figure says so, and the
  measurement is kept.
- **Working features are preserved.** Every constraint is added with a migration that checks the existing data
  first, and a test.
