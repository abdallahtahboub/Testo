# 0029 — What each run costs us, from a price book that never rewrites history

- **Status:** Accepted by the owner on 2026-11-16, with the choices below
- **Date:** 2026-11-16

## Context
Plan prices cannot be set from evidence while nothing records what a run costs us (gap G10), and the only prices in
the code were the planner's token prices, written as constants (gap G11). They were also out of date: $3 and $15 per
million tokens are Claude Sonnet 4.6's list prices, while the planner asks Claude Sonnet 5.5, at $2 and $10
(Anthropic's price list, checked 2026-11-16). The audit asks for cost rows per run that add up to the run's total, and
price versions with effective dates whose change never rewrites a cost already recorded.

Hosting is not chosen yet, so machine time and storage have no real price.

## Decision
1. **A price book, shared by every organization, outside the tenant boundary:** `price_versions` (a number, the
   instant it takes effect, the currency, who published it and why) and `unit_prices` (an item, a price, and the
   number of units it is for, such as $0.10 per 3,600 browser-seconds, and whether it is an estimate). Prices are kept
   in the unit they are quoted in, so none is rounded on its way in.
2. **Written once.** Versions and their prices carry the append-only guard of the audit log, the usage ledger and the
   plan catalog. A new version starts as a copy of the latest with the changes applied. It may take effect now or
   later, **never earlier**, so the book can never contradict a cost already recorded.
3. **US dollars** (the owner's choice), the currency the providers charge in. The cost report can show euros at a rate
   given when it is run; the rate is never stored.
4. **The first version is seeded by the migration** (`PriceCatalog`, fixed ids):
   - a browser-hour at **$0.10** and a gigabyte-month of storage at **$0.023**, **marked as estimates** (the owner's
     choice: typical cloud list prices, until hosting is chosen);
   - Claude Sonnet 5.5 at $2 / $10, Claude Opus 5.5 at $4 / $20, Claude Haiku 5.5 at $0.10 / $0.50 per million input /
     output tokens: list prices, not estimates.
5. **A cost row per run and item** (`run_costs`), written when the run's result is written back, in the same save as
   its usage: the usage quantity, the price, the version it came from, the amount to a millionth, whether the price was
   an estimate, and whether the run is billable. Its key names the run and the item, unique in the database, so a
   result written back twice records nothing twice. **A run that is not billable is costed all the same**: a run that
   fails by our fault costs us, and the report says how much of the month that was.
6. **The price applied is the one in force when the run finished**, by the worker's own clock, not when its result
   was written back.
7. **A run's total** (`test_runs.cost`) is the sum of its stored rows. Rows already stored are never recomputed.
8. **Storage is priced for one month** of keeping a run's evidence, until retention (G12) decides how long it is kept.
9. **The planner takes its token prices from the price book.** `PlannerOptions` has no prices of its own; without
   them it is not configured and asks nothing, because the planning budget's cost limit could not be kept. The
   benchmark fills them from the first version, and refuses a model the book has no price for.
10. **Operator commands** (`tools/Autestiq.Operator`): `set-prices` publishes a version; `cost-report [yyyy-MM]` gives,
    per organization and in total, runs, billable runs, billable minutes, cost, cost per run, cost per billable minute,
    and the share spent on our own failures, and says when any of it rests on estimates.

## Consequences
- **Easier:** the margin target can be checked against measured cost per billable minute before a plan price is
  published; a price change is a command, traceable, and does not touch history.
- **Harder:** a wrong price can only be corrected by a new version, and costs already recorded under it stay as they
  were. That is the point, and the report shows which version each row used.
- **Must remember:**
  - Until real machine and storage prices are published, every cost is part estimate. The report says so every time.
  - Model tokens are priced in the book but not yet recorded per run: the product does not call a model. When it
    does, its usage must say which model and whether a token was input or output, and be priced here.
  - Machine time is the browser's time only; the dispatcher's, the database's and the outer proxy's are not in it.
    A real hosting bill is the check on whether that is close enough.

## Alternatives considered
- **Prices in configuration.** Simpler, but a change is a deploy, history is whatever the configuration said last,
  and nothing proves which price a cost used. Rejected for the same reasons as plans in configuration (ADR-0028).
- **Computing cost when the report runs**, from usage and the price book. No rows to store, but a price version
  published with a mistake would change past months. Rows fix the amount when it is incurred.
- **Euros as the ledger's currency.** Matches the plan prices, but every amount would depend on an exchange rate
  chosen when it was recorded. The owner chose dollars.
