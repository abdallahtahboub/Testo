# 0021 — Exploring by pressing controls: opt-in, bounded, fresh-start

- **Status:** Accepted. Sections 1, 5 and 7 of the decision are superseded by [ADR-0022](0022-filling-forms-while-exploring.md)
- **Date:** 2026-11-15

## Context
The crawl described an application by reading it: it followed links and the sitemap and never acted
(ADR-0019 §5). The 2026-11-15 benchmark showed the cost. Five plans failed on a sound shop because
the crawl had only ever seen pages reached by navigation. "Add to basket" leads to `/basket`, and the
basket with products in it was never described, so plans that kept acting on the catalogue failed at
their second action. The planner's guesses about identifiers were mostly right. What was missing was
what each action leads to.

A plan that knows the consequences of its actions needs the crawl to press controls. That is an
action on the application, and on a customer's system an action can be a purchase, a deletion or a
sign-out. So it must be an explicit choice, not a default.

## Decision
1. **Exploration is opt-in.** `WorkerJob.ExploreStates` is off unless the caller sets it. The default
   crawl still only reads. Nothing in the product sets it yet; the benchmark sets it for its own shop.
2. **Only controls with a test id are pressed**, by test id through Playwright's own escaping, never
   by a selector built from application text.
3. **Controls whose names say they change something are never pressed:** order, pay, purchase, place,
   delete, sign-out, log-out, submit, confirm, send. A control that places an order is never pressed,
   and a test holds that.
4. **Every path starts from a fresh visit.** Cookies are cleared and the start page is loaded again,
   so a path cannot inherit what an earlier one put in the basket.
5. **The search is bounded and fixed.** Breadth first, at most three actions per path, at most eight
   controls per state, at most 300 visits and 60 distinct states. The count is fixed, not a time
   limit, so the same application is explored the same way every run (ADR-0019 §5 still applies).
6. **Each state records the path that reached it** (`PageSnapshot.ReachedBy`), and the planner is shown
   it as `reached by: click add-pot, then click to-catalogue`. A plan is then written against what the
   application does, not against what the crawl happened to see.
7. **Every path is followed, but the snapshot is kept free of repeats.** Two paths can look the same on
   the page and differ in what is hidden: the catalogue reads the same with a teapot or a cup in the
   basket. Only the output is deduplicated.

## Consequences
- **Easier:** a plan can know that a control leads somewhere, and that the basket then holds a product.
- **Harder:** exploration costs visits, roughly 200 in the demo shop, about one minute, and a larger
  prompt. The benchmark's planning cost rises with the prompt and must be measured again. Expect a
  noticeably higher cost per planned run than the $0.024 recorded on 2026-11-15, and a longer benchmark.
- **Not solved:** the search is bounded breadth-first, not a search of the application's whole state
  space. A state needing more than three actions, or a control that the bounded search does not reach,
  is still invisible to the plan, and the snapshot says it was truncated.
- **Must remember:** pressing controls on a customer's application is an action on their system and
  needs their approval and a per-target policy before it is ever enabled. This ADR does not grant that.
- **Must remember:** the rule that the model never chooses where to look still holds. The paths are
  chosen by the fixed search, not by the model.

## Alternatives considered
- **Keep the crawl read-only and describe transitions from forms.** Rejected: a form's destination is
  an attribute, which the snapshot deliberately excludes as untrusted input, and it would not show what
  a click does to the basket.
- **Let the model choose which controls to press.** Rejected: ADR-0019 §5. A page could lead the model
  somewhere, and a run would no longer be reproducible.
- **Explore every control of every page, at any depth.** Rejected: the number of paths is exponential in
  depth. A bounded search that is honest about its limit is better than an unbounded one that times out.
- **Exercise customer applications by default.** Rejected: an action on someone else's system needs
  their approval, which is a product decision, not a default.

## Amendment, 2026-11-15
The search described above did not last the day. The clinic showed that following every combination of
presses spends the budget before a booking form is reached, and adding forms showed 253 visits for 28
pages. [ADR-0022](0022-filling-forms-while-exploring.md) replaces the search (decision 5 and 7 here), and
makes "opt-in" (decision 1) a check in code: pressing is refused on any application we do not run
ourselves. Decisions 2, 3, 4 and 6 stand, with "cancel" added to the controls never pressed.

## Amendment (2026-11-25, R9): client-side applications

OWASP Juice Shop, the first client-side application measured, showed three things the explorer did not handle.
Each is changed in `Autestiq.Worker.SiteExplorer`:

1. **A fragment that is a route is part of the address.** A fragment that starts with `#/` or `#!/` is kept, so `/#/about`
   and `/#/contact` are different pages; any other fragment (`#main`) is a place on a page and is dropped. A page whose
   address has a fragment opens as a new document, from a blank page, so it does not inherit what the page before did.
2. **A page is read once it has been drawn.** After loading, and after every press, the explorer waits for the network
   to be quiet for half a second (at most 1.5 s). A page whose network never goes quiet, because it polls, is then
   watched until its element count holds for three looks (at most 3 s), by locator: the worker runs no script.
3. **Exploring stops in time.** At three quarters of the run's wall clock it stops opening and pressing and returns what it
   found, marked cut short. It used to run until the run's own limit ended it, with nothing.

Measured with the coverage check (the share of the hand-written baselines' steps the exploration shows), on the unseen
sets for our applications and the nine Juice Shop scenarios, each on a fresh container:

| Application | Acting | Read-only, links | Read-only, guarded pressing |
|---|---|---|---|
| Demo shop | 100% (before 100%) | 85% (85%) | 85% (85%) |
| Clinic | 98% (98%) | 90% (90%) | 90% (90%) |
| Invoice desk | 94% (94%) | 94% (94%) | 94% (94%) |
| **Juice Shop** | **74% (0%)** | **68% (24%)** | **74% (24%)** |

Nothing fell on our applications, and their explorations got faster, not slower: acting on the invoice desk took
213 s, against 400 s with the first version of these changes, which watched every page. Juice Shop's acting and
guarded explorations stop at the budget: what is left is mostly the search route, reached only by buttons, and product
names past the 60-element limit per page.

