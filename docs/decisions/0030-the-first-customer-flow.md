# 0030 — The first customer flow: projects, environments, plans that a person approves, and runs

- **Status:** Accepted by the owner on 2026-11-17, with the choices below; built in four parts
- **Date:** 2026-11-17

## Context
The planner meets the ADR-0020 gate's figures on our three applications, with and without test ids. Nobody
outside can try it: there is no screen where a customer adds a site, describes a test and sees the result.
Milestone 5 was narrowed on 2026-11-15 to projects, environments, generated test cases and suites, run history
and reports. This ADR is the narrow first flow, built on what 3.5 to 3.8 made safe: proof of site ownership,
sealed containers, quotas, cost per run.

## Decision
**The flow:** project → environment, with its site proved → a scenario in a sentence → the AI's plan, shown with
its notes and its Python export → approved by a person → run → result, step by step, with the reason for an
inconclusive one → the approved test downloaded.

**Built in four parts, each reviewed by the owner before the next:**
1. Projects and environments (this record's first build).
2. Describing a scenario, and the plan the AI writes for it.
3. Approving a plan, running it, and its result.
4. Downloading the approved test.

**The owner's choices (2026-11-17):**
- **Whose model key:** ours, configured on the server. Every planning call is checked first against a monthly
  budget for the whole product, **$10**, and a monthly cap per organization, **$2** (about 40 to 100 plans).
  Both are configuration, not code. At either limit planning stops with a message, never a surprise bill.
  Each plan's cost is recorded at the price book's prices (ADR-0029).
- **The AI's notes** are shown beside the plan as "Noticed while exploring", marked unverified, never as a
  found defect. Only a test's own verdict is a result.
- **Who approves a plan:** **Owner and QA lead**, as the permission table already says
  (`docs/identity-and-access.md` §2, `ApproveTestArtifacts`). The owner first chose "Owner, Admin, QA engineer"
  from a question that did not offer the QA lead role, and confirmed the table on 2026-11-17 once it was shown.

**Amended on 2026-11-17 by the owner's secure onboarding and privacy requirements**
(`docs/secure-onboarding-and-privacy.md`, gap analysis in `docs/secure-onboarding-gap-analysis.md`):
- **Part 1** also gives each environment a **kind**, test or production. **Production is refused** for runs until
  an Owner authorizes it with an explicit acknowledgement that tests can change real data (G-3).
- **Part 2** adds, before any planning call: a **per-project AI data-processing permission, off by default**,
  enforced in the service with a negative test (G-1); a **record of exactly what each planning call sent** (G-2);
  and the plan's notes **labelled by source**: observed by the crawl, given by the tester, or assumed and needing
  verification (G-7).
- **Part 3** flags plans that **may change data** (a form submitted, or a non-GET request seen by the proxy) and
  requires explicit approval of them (G-4). **No sign-in secrets are stored in part 3**: an encrypted secret
  store is its own later step, with its own review (G-6).

## Part 1, built (2026-11-17)
- **Projects** (`/app/projects`): the organization's projects, and a form to add one within the plan's
  project limit (Developer: one). A key is 2 to 10 letters and digits, starting with a letter, stored in
  capitals, and never reused in the organization. Creating needs `ManageProjects` and is audited.
- **Environments** (`/app/p/{key}/execution/environments`): one address of the application a project tests,
  normalized to its origin. Each shows where the organization's proof of that origin stands, and offers to
  ask for a proof and to check it (the 3.5 service). The proof belongs to the organization and the origin,
  not to the environment, so two projects testing one site share it, and removing an environment keeps it.
  An environment is tied to its project through the project's (organization, id) key, so it cannot belong to
  another organization's project.
- **The app's navigation**, drawn from what modules register (`NavigationRegistry`, `AppNav`), grouped by
  stage and filtered by feature flag. Until now the registry had no implementation and the menu was a
  placeholder, so the new pages could not be reached.
- **Production off by default:** an environment is test unless added as production. The dispatcher refuses a
  run aimed at an origin the organization lists as an unauthorized production environment, whatever its proof
  says (`RunDispatcher.MayTargetAsync`). Only an Owner can authorize it, only with the acknowledgement ticked,
  and can withdraw it; both are audited.
- **Forms stay disabled until the page is interactive:** typing into a prerendered page is lost when the
  circuit replaces it.

## Consequences
- **Easier:** a customer can set up what a run needs, and the shell finally shows where things are.
- **Harder:** the product will call a model with our key from part 2 on, so the budget checks must hold
  before any call, and are tested.
- **Must remember:** customer origins are still refused outside container mode (ADR-0026); an environment
  can be added and proved in any mode, but run against only from a sealed container.

## Alternatives considered
- **Each organization's own model key.** More setup for a customer and a secret of theirs to keep; reconsidered
  when there are paying customers.
- **Recorded answers only.** Free, but nobody could try a scenario of their own, which is the point.
