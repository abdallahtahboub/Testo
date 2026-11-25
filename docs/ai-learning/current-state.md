# Current state: what learning can build on

> Verified against `develop` on 2026-11-24. Figures are linked to where they are published; none is restated as
> current performance unless it was measured with the current prompt. **The planner prompt changed on 2026-11-24
> (ADR-0035 step 2); every figure below predates it until the re-measurement is published.**

## 1. How the AI is used today

The design is **deterministic software around one bounded model call** (ADR-0019):

1. **Exploration, no model.** The worker crawls the target in a sealed container behind an egress proxy that refuses by
   default (ADR-0018, ADR-0026). It reads pages, presses controls and fills forms only where allowed (ADR-0021,
   ADR-0022, ADR-0032), and describes each element with a locator *the browser* confirmed finds exactly one element
   (ADR-0024). Output: a `SiteSnapshot`.
2. **Planning, one model call.** `IScenarioPlanner` (`src/Autestiq.Ai/AnthropicPlanner.cs`) sends the tester's
   sentence, the snapshot, secret *names*, and stated expected results to Claude Sonnet 5.5. The answer is a plan in a
   **closed vocabulary of ten actions**: no code, no selectors.
3. **Validation, no model.** `PlanValidator` refuses foreign origins, unknown secrets, ambiguous targets (ADR-0024),
   plans with no checks, and more. A refused plan gets one retry with the reasons.
4. **Labelling and coverage, no model.** Software labels each check observed, stated or assumed (ADR-0034), and makes a
   plan partial when it leaves a stated result unchecked or lists something as not tested (ADR-0035).
5. **Approval, a person.** Steps that may change data are found by software and need an explicit tick (ADR-0030).
6. **Execution, no model.** The worker runs the plan step by step. **Nothing is decided by a model during a run**, and
   a failing check is never "healed" away.

This matters for the strategy: **the model's job is narrow (writing a plan from a description), and most of the
system's reliability comes from the software around it.** Any learning approach has to improve the plan or the
observation it is based on, without moving decisions from software to a model.

## 2. Capability inventory

| Capability | Status | Where |
|---|---|---|
| Provider abstraction for planners | **Implemented** | `IScenarioPlanner`, `IPlannerFactory`, `RecordedPlanner`, `NoPlanner` (`src/Autestiq.Ai`) |
| Closed action vocabulary, plan validator, one retry with reasons | **Implemented** | ADR-0019; `src/Autestiq.Application/Planning/PlanValidator.cs` |
| Software-chosen locators; ambiguous steps refused | **Implemented** | ADR-0024; `src/Autestiq.Worker/Targets.cs`, `SiteExplorer*.cs` |
| Sealed execution per run, egress proxy, origin proofs, kill switch | **Implemented** | ADR-0018, ADR-0026 |
| Exploration that presses and fills, read-only by default on customer sites | **Implemented** | ADR-0021, ADR-0022, ADR-0032 |
| Test accounts, encrypted, never to a model; self-confirming checks guarded | **Implemented** | ADR-0033 |
| Tester-stated expected results; evidence labels | **Implemented** | ADR-0034 |
| Coverage that fails closed; "not tested" list forces partial | **Implemented, unverified** (prompt change not yet measured) | ADR-0035 |
| Three target applications with **21 switchable defects** (7 each) | **Implemented** | `src/Autestiq.DemoShop`, `BookingShop`, `InvoiceDesk`; `Defects.cs` in each |
| Benchmark harness: baselines, ground truth, verdicts incl. "inconclusive" and "detected nothing", flake over repeats | **Implemented** | `tools/Autestiq.Benchmark`; ADR-0020 |
| Held-out and unseen scenario sets written after the prompt was frozen | **Implemented** | `HeldOutScenarios.cs`, `UnseenScenarios.cs` |
| Third-party targets: eShopOnWeb, Spring PetClinic (clean measurement) | **Implemented** | `EshopScenarios.cs`, `PetclinicScenarios.cs` |
| Recorded model answers, replayable for free | **Implemented** | `PlanRecording.cs`, `--recorded` |
| Every exploration before any run, so no exploration sees a run's changes | **Implemented** | PR #72 |
| Exact record of what each planning call sent; cost per plan at price-book prices | **Implemented** | ADR-0029, ADR-0030 |
| Runs, steps, screenshots, retention by policy | **Implemented** | ADR-0031 |
| Exported tests from fixed templates (Python), verified against every benchmark run | **Implemented** | ADR-0023 |
| Fault injection beyond business-logic switches (DOM, network, timing, auth) | **Missing** | [fault-injection.md](fault-injection.md) |
| Structured, versioned experience records and datasets | **Missing** | [data-architecture.md](data-architecture.md) |
| Retrieval of verified prior experience into planning | **Missing** | [learning-and-models.md](learning-and-models.md) |
| Repair planning: a new plan from a failed run's evidence | **Missing** (and constrained: never inside a run) | [experiments/0001](experiments/0001-repair-planning-from-verified-experience.md) |
| Fine-tuned or self-hosted models | **Missing**, not recommended yet | [learning-and-models.md](learning-and-models.md) |
| Automated tests for the benchmark tool itself | **Missing** | `docs/open-questions.md` |
| Object storage, vector store | **Missing**, waits on hosting and evidence | [data-architecture.md](data-architecture.md) |

## 3. What has been measured, and what it says

All in ADR-0020 and `docs/ai-benchmark*.md`, with the prompt before 2026-11-24 and Claude Sonnet 5.5:

| Measurement | Result | What it does not say |
|---|---|---|
| Unseen sets, our 3 apps, full exploration | 95% correct on sound apps, every seeded defect caught where a verdict was reached, **no false positive**; same without test ids | Our own apps; the explorer was built on them |
| Same, explored read-only | 36 of 66 correct (full: 62 of 66), no false positive | Read-only never sees what an action leads to |
| eShopOnWeb, signed in, after changes made on its results | 18 of 22, no false positive | Not a clean measurement |
| **Spring PetClinic, clean** (scenarios from source before any run) | **20 of 22 (91%), no false positive**; 16 of 22 strictly | One small app, no defects to catch, one run |

**What the losses were.** Across these runs, the misses are mostly *observation* problems: states exploration never
reached, unlabelled fields nothing can name, read-only exploration, a guessed address. Plans also sometimes claimed
more coverage than they checked (fixed in software by ADR-0035). **Very few losses were the model lacking QA
knowledge.** That is the central finding for this strategy: a model trained on more QA examples would not have fixed
most of them.

## 4. Assets a learning programme already has

- **Ground truth for free on our apps.** A defect switch says, without a person, whether a plan *should* fail. That is
  the rarest ingredient in learning from experience.
- **Honest verdicts.** Inconclusive, "detected nothing" and partial are first-class outcomes, so a dataset built from
  this harness cannot quietly count a weak pass as a skill.
- **Provenance by construction.** Each plan records exactly what was sent, which model, at what price.
- **A frozen-prompt discipline** and held-out sets, which is what keeps evaluation uncontaminated.

## 5. Gaps a learning programme must fill first

1. **Scale and diversity of practice targets.** Three in-house apps and two third-party ones are too few to tell
   learning from memorising.
2. **Fault variety.** All 21 faults are business-logic switches; none is a DOM, timing, network or session fault.
3. **A structured experience record** that ties scenario, snapshot, plan, run, verdict and ground truth together and can
   be versioned and split.
4. **A way to use experience at planning time** (retrieval), and a measured answer to whether it helps.
