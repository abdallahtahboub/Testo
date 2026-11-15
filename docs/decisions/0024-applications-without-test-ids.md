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

## The best locator, found and checked by the software (2026-11-15)
The owner's addition, made a standing rule: **when an element has no test id, the software, not the
model, looks for the best way to reach it, and checks it in the browser before a plan may use it.** A
locator that matches two elements, or none, fails a step for a reason that has nothing to do with the
application, so this decides more of a run's outcome than anything the model writes.

For every element the explorer describes, the candidates are tried in this order, and the first one the
browser finds exactly one element for is kept, through the same `Targets.Locate` a planned run uses:

1. its **test id**, when it has one;
2. its **role and accessible name**, as the browser computes them (read from Playwright's ARIA snapshot):
   a field labelled "Email" is `textbox "Email"` whatever its `name` attribute says, and a search field is
   a `searchbox`, the role the browser gives it; matched exactly, so "Password" is not "Confirm password";
3. its **exact text**, as the page wrote it rather than as a stylesheet draws it;
4. each of those **narrowed to a container** (`tr`, `li`, `article`, `section`, `form`, `fieldset`, `dl`,
   `p`), trying each single cell or label of the nearest three containers, then each whole line, until
   one is unique. The container used is the innermost one that holds both that text and the element. The first cell is not always the one that tells it apart: two clinic services cost the
   same, and only their names differ. A line is taken whole, never with the element's own text cut out
   of it, which once turned "£11.25" into "£.25".

The choice is recorded on the element as `NamedBy` (and `Within`), the prompt tells the model which form
to use (`use=testId|roleAndName|text|none`), and pressing during exploration uses the same choice. An
element nothing reaches uniquely is described as `use=none`, and a plan does not lean on it. The test
`EveryElement_HasAWayToBeReached_ThatTheBrowserConfirmed` holds this on the demo shop without test ids.

Measured on all three applications served without test ids (`--without-test-ids --explore`): before this
change 20 elements on the demo shop and 13 on the invoice desk had no unique locator; after it, none on
any of the three (demo shop 147 elements: 81 by role and name, 66 by text; invoice desk 660: 300 and 360;
clinic 401: 311 and 90). Coverage of the baselines is unchanged, because it counts what is shown, not
whether it can be reached.

## eShopOnWeb, the first application that is not ours (2026-11-15)
Microsoft's eShopOnWeb sample (commit `4da8212`, MIT licence) was built and run here as the independent
application this ADR asked for. It has no test ids at all. It is not part of this repository and is not
redistributed; the benchmark reaches it at an address given with `--origin` (target `eshop`).

**How it was run.** It targets .NET 8 and this machine has only .NET 10, so its `global.json` was moved
aside and it was run on the .NET 10 runtime with `DOTNET_ROLL_FORWARD=Major` and its own in-memory
database (`UseOnlyInMemoryDatabase=true`). Its six client libraries are fetched by LibMan from cdnjs, which
this environment's network does not allow; the same versions were taken from npm and put where its pages
look for them, and it was built with `-p:LibraryRestore=false`. Its own code was not changed.

**What the explorer made of it, before any change.** 18 pages described, 85 pressing visits, and 193 of 211
elements (91%) with a locator the browser found exactly one element for. The 18 it could not reach showed
three faults that none of our three applications has:

1. *Text drawn in capitals.* The catalogue shows "Roslyn Red Sheet" as ROSLYN RED SHEET with a stylesheet.
   The explorer read the drawn text and offered it as an exact-text target, which the browser compares with
   the text as written, so it never matched. Text is now read as the page wrote it (`textContent`), which is
   also what Playwright's text assertions compare.
2. *A name that is part of another.* A role and name were matched loosely, so the textbox "Password" also
   found "Confirm password" and was never unique. Names are now matched exactly; they are the browser's own
   accessible names, so an exact match is the one meant.
3. *A container that holds only the name.* "Within" took the innermost container with the text, which can be
   the cell that shows only a product's name, with the price in the next cell. It now takes the innermost
   container that holds both the text and the element.

After the three fixes: **211 of 211**, and still every element on our three applications served without test
ids (demo shop 147, invoice desk 660, clinic 401).

**What that does to the measurement.** The eleven eShopOnWeb scenarios and their baselines were written and
committed before these fixes, from what the pages say, and the baselines pass 11 of 11 on two runs. But the
explorer has now been changed because of a result on this application, so by the freeze rule eShopOnWeb is
no longer a clean set for the explorer: when the planner is run on it, the result is to be reported as
"scenarios frozen beforehand; explorer adjusted on this application's locator results". A fully clean claim
needs a fifth application.

**What the vocabulary cannot do here**, found while writing the scenarios and left out of them on purpose:
choosing an option from a list (there is no select action, so the brand and type filters cannot be used),
changing a quantity (the field has no label, so nothing names it), and the basket's total (shown twice with
nothing to tell the two apart). Each is a reason a real customer's test would fail that has nothing to do
with the model. A fourth scenario, about a refused registration, was dropped before the fixes because its
baseline failed: the message is shown both in the form's summary and beside the field. After the fixes the
explorer does tell the two apart (one of them is the only copy inside a container that also says
"Password"), but the scenario stays dropped: the set was frozen without it.

**Exact names, and a plan's own words.** What the explorer offers a plan is checked with exact names. A
plan's step is matched exactly first as well; only when nothing has the exact name within two seconds is a
looser match tried, so a name a person or a model shortened ("Basket" for the link "Basket (2)", whose count
changes as the test runs) still finds its element, and an exact match is never passed over for a looser one
(`Targets.LocateForStepAsync`).
