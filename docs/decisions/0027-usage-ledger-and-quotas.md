# 0027 — A usage ledger, and quotas that hold under parallel starts

- **Status:** Accepted by the owner on 2026-11-16, with the choices below
- **Date:** 2026-11-16

## Context
Milestone 3.6 closes four gaps from the commercial readiness audit before any paid plan or customer with a cost
ceiling: no usage record (G6), no per-organization concurrency limit (G7, R-12), no reservation so that parallel
starts cannot overspend a quota (G8), and no rate limit on the expensive paths (G14). Billing itself, the plan
catalog and per-run cost records are later milestones (3.7, 3.8, Phase D).

## Decision
1. **An append-only ledger, `usage_events`, inside the tenant boundary.** One row per measured use, never changed
   or deleted (the same guard as the audit log, now on an `IAppendOnly` marker). Each event has an idempotency key
   unique within the organization, so the same use recorded twice is stored once, by the database.
2. **What a run records, in the same save as its result:** one `Run`, its `BrowserSeconds` (exact, from the
   worker's own start and finish, rounded up to the second), and its `ArtifactBytes`. `ModelTokens` exists for when
   the product calls a model.
3. **What is billable: decision B** (commercial readiness §9). A run is billable when it reached the customer's
   application, which is the proxy having relayed something from the one origin the run may reach, and did not end
   by our fault: an infrastructure error or the operator's kill switch. A failure of the application, the test or
   the customer's configuration is billable. Non-billable use is still recorded.
4. **Minutes are rounded once a month** (the owner's choice): exact seconds are added up, and the month's total is
   rounded up to the next whole minute.
5. **A hard stop at the quota for now** (the owner's choice): when a month's billable minutes and the time reserved
   by running runs reach the plan's allowance, new runs are refused until the next month or a plan change. Overage
   waits for billing.
6. **Limits from configuration until the plan catalog exists** (`Usage:Plans`, confirmed by the owner, not
   published): Developer 100 minutes a month and 1 run at once, Starter 500 and 2, Growth 2,500 and 5, Scale 10,000
   and 10. Every organization's plan is "trial" today, which is the Developer plan. An unknown plan name gets the
   smallest plan, never an unlimited one.
7. **Concurrency and quota are checked when a run is claimed, under a lock on the organization's row**, so they
   hold across dispatchers and restarts rather than inside one process.
8. **A reservation is a column on the run** (`reserved_seconds`): its whole time budget, set when it is claimed and
   released when its result is written back. Only running runs count, so a run that is reaped or cancelled releases
   its reservation with nothing more to do, and a crash between reserving and finishing needs no separate repair.
9. **Rate limits are counted in the database**, per organization and per person, on starting and re-running runs
   and on asking for proof checks, so they hold across servers.

## Consequences
- **Easier:** a bill can be drawn from the ledger alone; a quota cannot be beaten by starting runs in parallel; one
  organization cannot take every worker.
- **Harder:** every claim takes a row lock on its organization. Runs of one organization are claimed one at a time,
  which is the point; runs of different organizations are not held up by each other.
- **Must remember:** the "reached" test is the proxy having relayed bytes. A target that answers nothing at all is
  not billable, even if the customer's test was wrong. The ledger stores months in UTC.

## Alternatives considered
- **Count usage from the runs table when needed.** No idempotency, no record of what was billed, and a run edited
  later would change a past bill.
- **A separate reservations table.** It needs its own reconciliation after a crash; a column counted only on
  running runs needs none.
- **In-memory rate limiting, like sign-in's.** It holds per process only, which is exactly R-12's problem.

## Built (2026-11-16)
- **Step 1, the ledger:** `UsageEvent`, `UsageMeter` (the rules), `IUsageService` (the current month, rounded).
  The dispatcher records a run's usage with its result. Tests: the same key is refused by the database; an event
  cannot be changed; a run's usage is recorded once and billable as the rule says (fails if the dispatcher records
  nothing); the month adds up billable seconds only and rounds once.
- **Steps 2 and 3, concurrency and quota at the claim:** `TryClaimAsync` locks the organization's row with
  `SELECT … FOR UPDATE` inside the claim's transaction, then reads `RoomForAsync`:
  - its plan's limits, less its running runs, its billable seconds this month and what its running runs reserve;
  - at its limit, a run waits in the queue, and the oldest run of each other organization is tried instead;
  - with the month used up it is refused, the hard stop;
  - otherwise it reserves its whole budget or what is left, whichever is less, and the worker is given exactly that
    much time.

  Starting or re-running a run is refused with `Execution_Error_MinutesUsedUp` once the month is used up.
  Tests (`ClaimLimitsTests`):
  - an organization at its limit waits while another's run starts;
  - two dispatchers racing never exceed one organization's limit;
  - a run reserves no more than what is left;
  - a used-up month refuses a queued run and a new start;
  - two runs cannot overspend the last 90 seconds;
  - a finished run, and a lost one, hold nothing.

  The first two fail without the limit, and the next two without the cap on the reservation. The row lock is not
  provable that way: without it the race test makes a double claim likely, not certain.
