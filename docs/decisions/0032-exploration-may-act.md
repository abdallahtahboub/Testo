# 0032 — Exploration that may act on a customer's site, with the customer's permission

- **Status:** Proposed (waiting for the owner's approval)
- **Date:** 2026-11-17

## Context
A customer's site is explored read-only (ADR-0030, part 2): links followed, no control pressed, no form sent. The
read-only benchmark (ADR-0020, 2026-11-17) measured what that costs. **36 of 66 runs were correct, against 62 of 66
with full exploration, and there was no false positive in either.** Every lost scenario is a flow whose result
appears only after an action: the basket after "Add", the confirmation after "Confirm booking". Read-only
exploration never sees those pages, so the planner either guesses and fails, or honestly plans only part of the
test.

The owner's rules allow acting on a customer's site only with explicit approval (`secure-onboarding-and-privacy.md`
§5: state-changing steps need explicit approval; production is off by default). Plans already have such an approval
(ADR-0030, part 3). Exploration has none, so it may not act at all.

## Decision (proposed)
**A per-environment permission, "exploration may act", off by default.**

1. **Who and how.**
   - Only an **Owner** may turn it on, on the Environments page, after ticking an acknowledgement in words: "Exploring
     this site may press its buttons and submit its forms with test values. It can create records, such as test
     bookings or orders, that you may need to remove."
   - It is audited when given and when withdrawn (`environment.exploration_acting_allowed` / `_withdrawn`), and can
     be withdrawn at any time.
2. **Never on production.**
   - Refused for an environment of kind Production, even one authorized for runs. Exploring is broader than running
     an approved plan, so production authorization does not extend to it.
   - The service refuses it, and so does the dispatcher.
3. **Held by software, in three places.**
   - **The service** stores the permission and refuses a production environment.
   - **The dispatcher** sets `ExploreStates` and `SubmitForms` on an exploration job for a customer's origin only
     when that environment's permission is given and still holds when the run starts. The check is the same kind
     as the approval re-check in part 3.
   - **The worker's `ExplorationPolicy`** acts on a customer's origin only when the job carries a new, explicit
     flag, `CustomerAllowsActing`, which the dispatcher sets only from the stored permission. Without it, the policy
     stays exactly as today: read-only unless the target is our own loopback application.
4. **What acting means, unchanged from what our own applications get:**
   - forms are filled only with fixed test values (`FormValues`: `test@example.test`, a fake password, placeholder
     numbers), never a secret and never anything the AI wrote;
   - controls named for spending, destroying or leaving (`order`, `pay`, `purchase`, `place`, `delete`, `cancel`,
     sign out) are never pressed, on any site;
   - the page and step budgets, the sealed container, the egress allowlist and the ownership proof all apply as
     before.
5. **What exploring did is shown and kept.** The test's page lists the controls pressed and the forms sent while
   exploring, from the snapshot's `ReachedBy` records, so the customer can see and remove anything it created.
6. **Measured before it is claimed.**
   - First the free `--coverage` check, then a paid benchmark run of the unseen set with acting allowed, with the
     owner's yes and the price stated first.
   - Until that run reproduces the full-exploration figures, the product makes no accuracy claim for customer sites.

## Consequences
- **Easier:** planning on a customer's staging site can see what its actions lead to, as on our own applications,
  where it measured 62 of 66.
- **Harder:** exploring can now create data on a customer's site. The acknowledgement says so, the never-pressed
  list and the test values limit it, and the record of what was done lets the customer undo it.
- **Risks, stated:**
  - The never-pressed list works on names. A destructive button called something else ("Clear", "Archive") would be
    pressed. The acknowledgement and the staging-only rule are what hold there.
  - A staging site that sends real emails or charges a sandbox would do so for test values. The acknowledgement
    names this.
- **Deferred:**
  - a customer-editable list of controls never to press;
  - per-page allow and deny rules (the discovery-scope review of gap G-13).

## Alternatives considered
- **Repair while running:** re-look at the page and re-plan when a step can't find its element. It is more
  model calls per run, an AI in the loop at run time, and it needs its own measurement. It may come later, alongside
  this.
- **Telling the planner that a click's result is unknown.** Cheap, but it changes the prompt frozen for the unseen
  set, so the measurement would stop being clean. And the result still can't be seen, only guessed more carefully.
- **Leaving it read-only.** Safe, and measured at 55%. That isn't enough to sell planning on customer sites, and
  the docs say so either way.
