# Long-term goal: understand the customer's application, then build the whole test basis

> **Status: a goal set by the owner on 2026-11-25, with engineering's assessment beside it. Nothing here is built
> beyond what §3 lists as implemented.** It gives direction to Milestone 6 and later; it does not reorder the approved
> roadmap ([`../roadmap.md`](../roadmap.md)). The timeline in §5 is an **estimate**, not a commitment.

## 1. The goal, in the owner's words

> "I want to reach a point where the software can crawl the client's software and understand it thoroughly (with the
> context of course: business model, user stories, stakeholder input etc.; the repo code is optional). After that my
> solution will extract all information needed and build the test scenarios, aggregate tests together into test
> suites, write the test plan and strategy etc."

In ISTQB terms ([`../testing-journey.md`](../testing-journey.md)) this is stages 1, 3, 4 and 5 done from evidence:
test planning, test analysis (the test basis and its conditions), test design (scenarios and cases) and test
implementation (suites), on top of the execution Autestiq already has.

## 2. What "understand it thoroughly" has to mean to be checkable

An understanding is useful only if it can be shown and checked. So it is **a stored application model**, not a long
prompt:

| Part | What it holds | Where it comes from |
|---|---|---|
| **Observed model** | Pages and their states, forms and fields, controls, navigation, roles, the entities shown (orders, invoices, patients), the flows between pages | Exploring the site (read-only by default; acting only with ADR-0032's permission) |
| **Documented model** | Requirements, user stories, business rules, acceptance criteria, risks and priorities | Documents and people the customer provides: specifications, stories, stakeholder input, the business model |
| **Optional source model** | Routes, validations, roles, feature flags | The repository, only if the customer grants it (least privilege, never required) |
| **Links** | Which requirement is shown on which page, which flow realises which story, which rule has no observed behaviour, and the reverse | Software proposes; a person confirms |

The binding rule already in force applies to every part: **observed, documented and assumed are never mixed**
(`../secure-onboarding-and-privacy.md`). An inferred business rule is shown as an assumption until a document or a
person confirms it. Every scenario, suite and plan section traces back to the evidence it rests on, with its
uncertainty stated.

From that model the deliverables follow, each one traceable to the model:

1. **Test conditions** per requirement and per observed feature, with gaps on both sides: documented but never
   observed, and observed but documented nowhere.
2. **Test scenarios and cases**: the ones Autestiq can run end to end (as today), plus manual ones where automation
   cannot reach.
3. **Suites**: smoke, regression, per feature, per risk, per role, built from the links, not by hand.
4. **Test plan and strategy**: scope, approach per risk, environments, entry and exit criteria, schedule inputs,
   following ISTQB and IEEE 29119-3 structure, with every claim pointing at the model.

## 3. Where Autestiq stands today (2026-11-25)

**Implemented:**
- Exploring a site, read-only on a customer's site, and acting on our own apps or with an Owner's permission
  (ADR-0032), signed in with a stored test account (ADR-0033).
- Locators chosen and checked by software, never by the model (ADR-0024).
- Planning one test from a sentence, with the tester's stated results and every check labelled observed, stated or
  assumed (ADR-0034); a plan lists what it does not test (ADR-0035).
- Approval, running in a sealed container, verdicts that never pass a broken application in the measurements, and
  export to Python.

**Not built:**
- Any stored application model: each test explores on its own and keeps only its own snapshot.
- Reading documents of any kind: requirements, stories, business model.
- Requirements, traceability, case library, suites, test plans: Milestone 6's six shell modules.

**Measured limits:**
- Read-only exploration, the default on a customer's site, is the weakest part: 36 of 66 runs correct, against 62
  of 66 when acting (ADR-0020).
- On third-party applications the explorer, not the model, caused most losses.

## 4. Assessment

### Is it possible?

**Yes, for typical web applications, with a person confirming the understanding.** Every piece exists in research or
in products today, and Autestiq already has the hardest one to retrofit: deterministic software that verifies what the
model proposes. Each piece is a different kind of problem:

1. **Exploring deeply enough is the main technical risk.**
   - **Why it is hard:** a crawler sees what it can reach. Many states sit behind forms that change data, roles,
     particular data or long flows. On a customer's site, safety rules limit what exploring may do, and they must.
   - **What helps:** guarded pressing (next), signed-in exploration per role, the customer's own test data, and later
     the private agent.
   - **What remains:** some applications will stay partly understood. The model must say which parts.
2. **Understanding what the application *should* do cannot come from crawling.**
   - **Why:** this is the test-oracle problem. A crawl shows behaviour, not intent, so a bug looks like a feature.
   - **What helps:** the documents and stakeholder input. That makes the context essential, not a nice-to-have.
   - **What remains:** where there is no document, the output is an assumption, labelled as one.
3. **Scale and cost.**
   - **Why:** a real application has hundreds of pages and states. Sending all of it to a model is costly and
     exceeds what one call can hold.
   - **What helps:** a stored model, explored incrementally, summarised by software, and sent in pieces within the
     AI budgets (ADR-0027, ADR-0029).
4. **Writing the scenarios, suites, plan and strategy is the easiest part for current models.** It is also the most
   dangerous: a model writes a convincing plan from a weak understanding. The value is not in the documents, which
   any chat assistant can draft, but in their being **true, traced and executable**.
5. **Keeping up with change.**
   - **Why:** the application changes every release.
   - **What helps:** re-exploring and comparing the models, which shows what changed and which tests and suites it
     affects.
   - **Why it matters:** this is where a stored model pays for itself, and where customers feel the value week after
     week.

### Is it viable as a business?

**Yes, and it fits the thesis** ([`BUSINESS_MODEL.md`](BUSINESS_MODEL.md)): QA teams spend most of their time on
analysis, design and maintenance, not on running tests.

**The risk is that the market is crowded.** Many tools now generate tests or test documents with AI.

**The defensible position is Autestiq's existing principles**, which few competitors hold together:
1. A staging URL alone is enough; source code is optional.
2. Every artifact is traced to its evidence, and observed and assumed are kept apart.
3. Verdicts never pass a broken application.
4. The whole ISTQB journey sits in one place.

Selling "it writes your test plan" competes on price. Selling "it shows what it saw, what your documents say, where
they disagree, and tests the difference" competes on trust.

### How long would it take? (estimate)

The estimate assumes the current way of working: the owner with AI assistants, milestone by milestone, with review
stops. Stage A runs before the pilot; B onwards starts after it, because the pilot decides which part earns investment first. Each stage ends with a
measurement on the lab applications and at least one third-party application, as today.

| Stage | What it delivers | Estimate | Builds on |
|---|---|---|---|
| A. Deeper exploration | Guarded pressing; per-role signed-in exploration; measured on third-party apps | 1–2 months (in progress before the pilot) | ADR-0032, ADR-0033 |
| B. Application model | Pages, states, forms, flows and roles stored per environment; explored incrementally; shown to the person, who can correct it | 2–3 months | A |
| C. Context ingestion | Upload of specifications, stories and stakeholder notes; requirements with provenance; links to the model, proposed by AI and confirmed by a person | 2–3 months | B; Milestone 6.2 (Analysis) |
| D. Scenarios and suites | Conditions and scenarios from the model and requirements; gaps both ways; suites by feature, risk and role; runnable ones planned as today | 2–3 months | C; Milestones 6.3–6.4 |
| E. Test plan and strategy | Documents generated from the model, every section traced | 1–2 months | D; Milestone 6.1 |
| F. Change | Re-explore, compare, and show affected tests and suites | 2 months, then ongoing | B–D |

**Total:** a credible first version for small and medium web applications in roughly **10–15 months** of focused work
from now, stage A included. "Thorough understanding of any customer's software" is never finished: complex enterprise
applications will stay partly modelled, and a person confirming the understanding is permanent, not a temporary
crutch. The AI learning programme (ADR-0036, proposed) may shorten stages B–D once there is verified experience; it is
not a precondition.

### How we will know it works

These are checkable, with the same honesty as ADR-0020:
- **Discovery:** the share of each lab application's known pages, flows and roles the model finds.
- **Requirements:** extraction precision and recall against hand-written requirements, for documents we wrote for the
  lab applications.
- **Links:** precision of proposed requirement-to-page links against a hand-made traceability matrix.
- **Scenarios:** the existing benchmark: verdicts correct, no false pass, figures kept with their limits.
- **Documents:** a reviewer's corrections per plan section, measured as R6 measures corrections today.

## 5. General opinion (engineering, 2026-11-25)

It is the right long-term goal, and the architecture already points at it: the module shells are its stages, and the
evidence rules are what make it trustworthy. Three cautions:

1. **Build in the order of the evidence, not of the demo.**
   - Exploration depth and the application model come first, because they are the measured bottleneck.
   - Generated plans and strategies come last.
   - A beautiful test strategy on top of a shallow crawl is the fastest way to lose a customer's trust.
2. **Make context ingestion a first-class product, not an attachment field.**
   - Most of the "understanding" a QA engineer has comes from talking to people and reading documents, not from
     clicking.
   - Customer documents are confidential: they need the AI permission, the minimized mode (G-2) and retention by
     policy, like everything sent to a model today.
3. **Keep a person confirming the model.** Not because the AI is weak, but because the application model is the
   customer's statement of what their product is. Software proposes it; the customer owns it.
