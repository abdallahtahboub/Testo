# 0037 — Planning attempts recorded before the call, a lease, and one planning step

- **Status:** Accepted (the owner approved Milestone 5.1 and R1 on 2026-11-24)
- **Date:** 2026-11-24

## Context
The pilot-readiness review (`docs/reviews/2026-11-24-pilot-readiness.md`, finding 2.3 and 2.4; security register R-17)
confirmed that `PlanningDispatcher` saved a test as Planning and only then called the model, and nothing ever picked
Planning up again: a crash or a restart left the test stuck for good, unable even to take new expected results. The
cost was written only after the call, so a crash after the provider billed lost the spend from the budget. The budget
was read, then spent, with nothing reserved, so two planning instances could spend the same room. And because a re-plan
cleared `PlannedAt` and overwrote `ModelCost`, **every re-plan also removed the earlier plan's spend from the month**
(found while building this).

## Decision
1. **Every planning call is an attempt row written before the model is asked** (`planning_attempts`: organization,
   test, model, ceiling). Its end is a separate row written once (`planning_attempt_outcomes`: answered with the
   provider's cost and tokens, or interrupted at the ceiling). Both are append-only (`IAppendOnly`, refused by the save
   guard); an attempt ends once (unique index); both are tied to the test's organization by composite keys.
2. **The budgets sum attempts, not tests**: an answered attempt at its cost, an attempt without an outcome at its
   ceiling, because it may have been billed. Re-plans count. The migration backfills one answered attempt per planned
   test at its recorded cost and date; spend that earlier re-plans overwrote is unknown and not invented.
3. **A lease** (`Planning:Lease`, 15 minutes, longer than any call with its retries). The planning step checks every
   `Planning:RecoveryInterval` (one minute) for tests in Planning past it, and **fails them** with "planning was
   interrupted", ending their open attempts as interrupted at their ceiling, audited (`test.planning_interrupted`).
   **It never plans them again on its own**: whether the provider billed is unknown, and spending again is the
   person's decision (new expected results, or describing the test again).
4. **An answer arriving after recovery is not kept**: the save fails on the test's row version or on the outcome's
   unique index, the answer is dropped and logged, and the attempt stays interrupted at its ceiling.
5. **One planning step per deployment**: a PostgreSQL session advisory lock (key 5101, scoped to the current
   database), held on its own unpooled connection for as long as the step lives. A second instance does not plan; if
   the holder dies, its connection closes and PostgreSQL releases the lock, and another instance takes over.

## Consequences
- A crash can no longer strand a test or undercount the budget; at worst it **overcounts** by one ceiling ($0.25).
- With the lock, the budgets hold across instances in a deployment, because only one instance spends. They are still
  not transactional reservations: **before more than one planner runs, R-17's remaining fix is needed** (reservations
  in a transaction, reconciliation, concurrent-claim tests).
- An interrupted attempt stays at its ceiling. No reconciliation against the provider's usage report is built.
- `Planning` lasting up to 15 minutes before recovery is visible to the person as "planning…".

## Alternatives considered
- **Retry automatically after the lease.** Rejected: a billed request would be paid twice, silently.
- **Keep the transaction open across the model call** (`SELECT … FOR UPDATE`). Rejected: a connection held for a
  minute per plan, and a crash still leaves the outcome unknown.
- **Mutable attempt rows (status column updated).** Rejected: the owner asked for immutable attempt records; two
  append-only rows give the same answer with the guard's protection.
- **A leader-election service or a queue.** Rejected: new infrastructure for one background step; PostgreSQL's
  advisory lock does it with what exists.
