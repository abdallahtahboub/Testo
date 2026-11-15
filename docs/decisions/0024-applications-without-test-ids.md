# 0024 — Applications without test ids

- **Status:** Proposed
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

## Decision needed from the owner
Approve the vocabulary amendment (points 2 and 3), or keep the closed vocabulary as it is and require
test ids from customers.
