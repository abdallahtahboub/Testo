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

## Part 2, built (2026-11-17)
- **The AI permission, per project, off by default (G-1).** Someone with `ManageProjects` allows it on the tests
  page (`/app/p/{key}/tests`) by ticking an acknowledgement under a notice that says what is sent (the
  description; page addresses, titles, headings, and the names and visible texts of elements, which on a real
  site can include personal data) and what never is (secrets, screenshots, recordings, traces). Allowing and
  withdrawing are audited. **`TestCaseService.DescribeAsync` refuses without it, and `PlanningDispatcher` checks it
  again just before the call**, so a permission withdrawn while a site is being explored stops the call. Both
  have negative tests.
- **Describing a test** (`ManageTestCases`: Owner, QA lead, QA engineer) creates a test case and an exploration
  run, after the same gates a run has, in this order: permission to describe, a description of 1 to 1,000
  characters, the project, the AI permission, the environment, production authorization, the kill switch, the
  dispatcher's own `MayTargetAsync` (proof, container mode), browser minutes, the AI budgets, and the start rate
  limit. The exploration is an ordinary run (`Scenarios.Exploration`) with its own budget, 10 minutes and 24
  pages, so the quotas, cost rows and reaping of 3.5 to 3.8 apply to it unchanged.
- **Planning** (`PlanningDispatcher`, a background service) takes a test whose exploration finished, hands its
  snapshot to `PlanningService` (the measured planner and validator of ADR-0019/0020), and stores the plan, its
  notes, its cost and what was sent. A call's cost limit is the smaller of $0.25 and what is left of the month's
  budgets. Token prices come from the price book (ADR-0029); without them, or without a key
  (`Planner__ApiKey`, server configuration only), the test fails with "AI planning is not set up on this server"
  and nothing is called.
- **What was sent is recorded (G-2):** the exact text about the application sent on each attempt
  (`PlanPrompt.User`), shown on the test's page under the model's name and the cost. The system prompt beside it
  is fixed text with nothing of the customer's. **The minimized mode G-2 recommends is not built**: masking values
  can lower accuracy and must be measured first.
- **Notes kept apart by source (G-7):** "Checked by Autestiq" for what the validator found, and "The AI's notes:
  unverified" for everything the model wrote, in a separate dashed box that says to treat them as things to
  check. What the tester asked for is shown on its own. The AI's notes are **not** split further into "observed"
  and "assumed", because that split would rest on the model labelling itself; all of them are unverified.
- **The plan's Python export** is previewed, from the fixed templates (ADR-0023). Downloading waits for approval
  (parts 3 and 4).
- **An environment a test was explored against cannot be removed**, so a test never loses where it was planned.

**Limits and gaps, stated:**
- **A customer's site is explored read-only.** The worker's exploration policy presses buttons and submits forms
  only on our own loopback applications; on a customer's site it follows links. This was kept deliberately,
  because a submitted form can change data before anyone has approved anything (G-4). **Read-only exploration
  has not been measured on the benchmark**, so plans from it may be weaker than ADR-0020's figures, which come
  from full exploration of our applications.
- Customer sites still run only in container mode with a verified proof (ADR-0026). Only our own applications
  can be explored on a developer machine.
- Nothing runs a plan yet. Approval, the "may change data" flag and running come in part 3.

## Part 3, built (2026-11-17)
- **What may change data is decided by software (G-4)** (`PlanEffects`, in `Contracts` so the approval and the
  screen agree). Going to an address, waiting, checking and typing read. Clicking something exploring saw only as a
  link reads. Every other action may change data: clicking a button or anything not seen as a link, pressing a key,
  choosing an option, and clicking a target exploring never saw. The model is never asked. The test page marks each
  such step "May change data" and names the steps above the approval.
- **Approval** (`ApproveTestArtifacts`: Owner and QA lead) records who, when, and the SHA-256 of the exact stored
  plan. Refused for a partial plan, for a plan that types a secret, and for a plan with any step that may change data
  unless the approver ticks "I approve this plan to change data on …". Approving and withdrawing are audited.
- **Running** (`RunTests`: Owner, QA lead, QA engineer) is refused unless the plan is approved and still has the
  approved fingerprint. It then passes the same gates as any run: production authorization, the kill switch, the
  dispatcher's `MayTargetAsync`, minutes, and the start rate limit. **The dispatcher checks the approval again**
  (`RunDispatcher.ApprovedPlanAsync`) before any worker starts, so a withdrawn approval or a changed plan stops a
  queued run. The run carries out the plan through the worker's existing planned-run path, as a sealed container on
  a customer's site.
- **A read-only approval holds in the browser** (`ReadOnlyGuard` in the worker). The egress proxy sees only an
  encrypted tunnel for an HTTPS site, so the guard sits in the browser context: every request other than GET, HEAD
  and OPTIONS is aborted before it leaves, WebSockets are closed before they connect, and service workers are
  blocked. A read-only run that fails after something was blocked ends **inconclusive**, with what was blocked,
  because the block may be the cause.
- **No sign-in secrets are stored (G-6).** A plan that types one cannot be approved; a secret store is its own step.

**Limits, stated:**
- A link that changes data when followed is counted as reading, and the guard lets a GET through. HTTP says such
  links should not exist; some sites have them anyway. The approval text says so.
- A site that reads through POST requests (some GraphQL sites) gets inconclusive read-only runs. Approving such a
  plan to change data is the way through, and the approver is told what that allows.
- The rule leans towards "may change data", so most plans that press anything need the tick. That is deliberate.
- A run's result is shown on the run's own page. Downloading the approved test is part 4.

## Later, recorded with this flow
- **A live view of a run inside the product** (the owner's request of 2026-11-17). The customer watches the
  browser's run in Autestiq step by step, never in a window on their machine. First a picture per step as it
  ends, later a continuous stream. Bound by the evidence rules of the secure onboarding requirements. Planned
  with Milestone 7 (`docs/roadmap.md`).

## Consequences
- **Easier:** a customer can set up what a run needs, and the shell finally shows where things are.
- **Harder:** the product calls a model with our key from part 2 on, so the budget checks hold before any call
  and are tested, for the organization and for the product.
- **Must remember:** customer origins are still refused outside container mode (ADR-0026); an environment
  can be added and proved in any mode, but run against only from a sealed container.

## Alternatives considered
- **Each organization's own model key.** More setup for a customer and a secret of theirs to keep; reconsidered
  when there are paying customers.
- **Recorded answers only.** Free, but nobody could try a scenario of their own, which is the point.
