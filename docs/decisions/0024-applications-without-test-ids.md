# 0024 — Applications without test ids

- **Status:** Accepted by the owner on 2026-11-15, with an addition: the software finds the best way to reach each element itself
- **Date:** 2026-11-15

## Context
The explorer presses only controls that have a `data-testid` (ADR-0021), and it describes plain text
only when it has one. The planner's targets are a test id, or one of seven roles with a name (ADR-0019).
All three of our applications have test ids everywhere. Microsoft's eShopOnWeb, the independent
application chosen for a clean measurement, has none, and nor do most applications a customer will
bring. Requiring customers to add them before a first run is friction at the point the business
directive names as the riskiest: activation.

Measured on 2026-11-15, with no model, by serving our own applications with their test ids removed
(`dotnet run --project tools/Autestiq.Benchmark -- [--target …] --without-test-ids --explore`):

| Application | With test ids | Without |
|---|---|---|
| Demo shop | 18 pages, 154 elements, 47 visits pressing | 6 pages, 25 elements, nothing pressed |
| Booking clinic | 27 pages, 353 elements, 67 visits | 17 pages, 178 elements, nothing pressed |
| Invoice desk | 23 pages, 647 elements, 77 visits | 11 pages, 175 elements, nothing pressed |

What disappears is mostly what a test checks: prices, totals, statuses, messages. What is left is links,
headings and buttons, which a plan can already name by role and name, except where several share a name:
the demo shop's four "Add to basket" buttons, and eShopOnWeb's one per product.

## Proposal
Test ids stay preferred: they survive a change of wording. Without them:

1. **The explorer describes text without a test id**: table cells, list items and short paragraphs,
   within the existing limits on elements per page and length.
2. **The vocabulary gains a text target** (an amendment to ADR-0019): an element whose visible text is
   exactly a given value. It is located by Playwright's text matching, never as a selector, so nothing a
   model writes is ever treated as markup or code.
3. **A target may be scoped** with an optional "within": the nearest row, list item, form or section
   containing a given text. "The *Add to basket* button within the item that says *Cast iron teapot*."
   The container kinds are a fixed list of ours, and the text is matched as text.
4. **The explorer presses by role and name** when a control has no test id: by name when it is unique on
   its page, scoped by its container's text when it is not. Every rule of ADR-0021 and ADR-0022 still
   applies: our own applications only, never the controls named for paying, ordering, deleting,
   cancelling or signing out.
5. **A plan says when it relies on wording**, so a customer knows which tests a copy change will break,
   and the product can suggest where a test id would help.

## Consequences
- **Easier:** a customer's first run needs nothing added to their application.
- **Harder:** the vocabulary grows from two kinds of target to three, plus scoping, and every addition is
  attack surface for a hostile page (ADR-0019 §5). Each needs the same tests the existing kinds have:
  no field in which a selector or a script could be written.
- **Plans are less stable** where they name elements by wording.
- **Measurement:** developed against our three applications with their test ids removed, so that
  eShopOnWeb stays unseen until it is measured.

## Decision (2026-11-15)
The owner approved points 2 and 3, and asked that elements be tested whether or not they have test ids,
with the software finding the best way to reach each one itself. That choice is made by our code, never
by the model: the explorer works out, for every element, the most robust target in a fixed order, and the
planner is shown it.

1. **The order:** a test id; else role and accessible name, when unique on the page; else exact visible
   text; and for a repeat, the same scoped by the text of the row, item, form or section it sits in
   (`within`), looked for up to three containers out. A repeat nothing tells apart is not offered, and a
   target that matches more than one element fails its step rather than picking one.
2. **One place decides how a target becomes an element** (`Targets.Locate` in the worker), for planned
   runs and exploration alike. Every way it reaches an element is one Playwright expresses directly, with
   values passed as values: no selector is ever built from a plan or a page.
3. **The vocabulary** (amending ADR-0019): `TargetKind.Text`, and `Within` on a target. A target's text
   and its `within` are at most 200 characters each; a longer one is refused.
4. **Kinds of page are judged by structure.** Plain text without a test id counts by being there, not by
   what it says, so a page for another date or another invoice is not a new kind. Pages reached by
   pressing are kept apart by what they say as well, so two different refusals are both shown.
5. **Every exploration starts from a fresh application**, the crawl included, not only the visits that
   press.

## Measured, with no model (2026-11-15)
Coverage of the baseline plans, with test ids and with them removed (`--without-test-ids --coverage`,
which compares each element the baselines name by test id with what the copy without them shows):

| Set | With test ids | Without test ids |
|---|---|---|
| Demo shop, first twelve | 64 of 65 (98%) | 63 of 65 (97%) |
| Demo shop, held-out twenty | 106 of 106 (100%) | 104 of 106 (98%) |
| Booking clinic | 167 of 168 (99%) | 167 of 168 (99%) |
| Invoice desk | 100 of 102 (98%) | 88 of 102 (86%) |

Every gap without test ids that is not also a gap with them is a whole row or basket line, which is not
described as one element because it holds others. Its link and its cells are described, so a plan can
check it by those; the measure cannot see that equivalence. Describing text makes every prompt larger:
the invoice desk shows 726 elements with test ids, up from 647.
