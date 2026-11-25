# 0042 — Stronger locators: fields by label, menus and tabs, landmarks and dialogs, and proof that it is the element

- **Status:** Accepted (the owner asked for it on 2026-11-25)
- **Date:** 2026-11-25

## Context
ADR-0024 settled how an element is named without a test id. The explorer tries, in order:
- role and accessible name;
- exact text;
- each of those narrowed to the row, item, form or section the element sits in.

It keeps the first the browser finds exactly one element for. The model never chooses a locator, and nothing weak is
used: no XPath or CSS from a plan, no positions, no partial text.

The owner asked how the product handles elements with no test id, or with a locator that is not unique. The answer
named gaps found on applications nobody here wrote (R9):
- **Missing roles.** Menus (`menuitem`) and tabs (`tab`) were not in the vocabulary. One OWASP Juice Shop scenario
  could not be written.
- **Fields with no nameable role.** Fields whose role is not in the vocabulary, a number (`spinbutton`), could not be
  named.
- **Too few containers.** Containers were only `tr, li, article, section, form, fieldset, dl, p`. The same control in a
  dialog and outside it, or in the header and the footer, could not be told apart.

## Decision
1. **A field may be named by its label** (`TargetKind.Label`, `{"kind": "label"}`): the label tied to it, or its
   accessible label, matched exactly.
   - The explorer tries it after role and name, before text, for fields only.
   - The fields the crawl describes now include `tel`, `url`, `number`, `date` and `time` inputs.
   - Playwright gives most of them the textbox role. A number is a spinbutton, and is reached by its label.
2. **`menuitem` and `tab` are roles a target may name.** The explorer describes them, and pressing treats them as
   controls. Only a button is asked what form it sends. A button that is a tab is described once, as a tab.
3. **Containers are one list in `Contracts`** (`TargetScopes`), read by the worker, the explorer and the Python export:
   - the earlier eight;
   - the landmarks `main`, `nav`, `aside`, `header` and `footer`;
   - `dialog`, and elements with the roles `dialog`, `alertdialog`, `region`, `tabpanel`, `menu` and `group`.

   A bare `div` is not one: it says nothing about what it holds. The explorer's XPath of ancestors and the exported
   tests' `CONTAINERS` are written from that list, so they cannot drift apart.
4. **A chosen locator must find this element, not merely one element.** The explorer now checks that the locator and
   the element's own locator together still find it.
   - Before, a second "Close" button outside any container could be told to use "Close within We use cookies".
   - Through the new, wider containers that finds the dialog's "Close" exactly once: the wrong element, uniquely.
   - The check holds for every kind, not only the new ones.
5. **The planner's prompt names the new kinds and roles.** It is changed in the same commit, and its hash changes.

## Measurement
The coverage check, before and after this change. It gives the share of the hand-written baselines' steps whose page
or element the exploration shows, on the unseen sets and on OWASP Juice Shop. The figures, and "elements reachable"
from `--explore` without test ids, are in `docs/reviews/2026-11-25-r9-juice-shop.md`.

**Gitea, R9's clean application, is not measured with this change.** Its explorer, prompt and validator are frozen at
the commit that added its scenarios. Its first planner run uses that commit, `44ebe97` on `develop`.

## Consequences
- **Easier:**
  - number fields, menus and tabs become reachable;
  - a control repeated in a dialog, a navigation or a footer is told apart by it;
  - a locator is proven to be the element's own.
- **Harder:**
  - **every planner figure was measured with the earlier prompt.** The unseen sets, PetClinic and the release
    candidate are re-measured before they are quoted for this one, which needs API credit (ADR-0020);
  - the explorer asks the browser one more question per element it names.
- **Corrected:** password fields were said, in R9's first findings, to have no role. Playwright gives them the textbox
  role, and they were always reachable. A test now shows it.

## Alternatives considered
- **Weak locators (XPath, CSS paths, positions, partial text).** Rejected again: they break silently when the page
  changes, and they would let a model's string become a selector (ADR-0019).
- **Any `div` as a container.** Rejected: every page would be one container deep, and "within" would stop meaning
  anything.
- **A container introduced by a heading** ("the README section"). This would help on Gitea, where it was found. It is
  not done here, because Gitea is frozen. It may be measured later on a tuning application.
- **Keeping the prompt unchanged until credit allows a re-measurement.** Rejected by the owner's request to build the
  locators now; the re-measurement is recorded as owed.
