# Strategy: executive summary

> **Proposed, 2026-11-24.** The owner's brief asked for a strategy that challenges its own assumptions. This document
> does. Decisions it needs are at the end, and in ADR-0036 (proposed).

## The recommendation in one paragraph

Build a **Training Lab by growing the benchmark harness we already have**, not a second execution engine. Use it first
to create **verified experience** (scenarios on resettable applications with known faults, where software knows the
right verdict) and to **measure whether retrieving that experience improves planning**. Fine-tune a small open-weight
model only for a narrow, high-volume task, and only once retrieval has shown which experience helps and there are
enough verified examples. Defer reinforcement learning until a fine-tuned specialist exists and the lab can produce
rewards that cannot be gamed. **Do not train a foundation model from scratch**: nothing in Autestiq's problem needs it,
and it would spend the company's runway on the one part where large labs are unbeatable.

## Why this order

1. **The measured losses are mostly about observation, not knowledge** ([current-state.md](current-state.md) §3). The
   cheapest gains are still in the explorer and the software around the model. Learning work should not crowd that out.
2. **Verification is the expensive ingredient, and we already have it for our own apps.** A defect switch is free ground
   truth. The lab's first job is to multiply scenarios and faults on applications where truth is known.
3. **Retrieval is reversible and cheap to measure.** It changes what the model is told, not the model. If it does not
   help, nothing was trained, nothing has to be hosted, and the experiment is still a publishable negative.
4. **Fine-tuning pays off for narrow, frequent, checkable tasks** (failure classification, choosing among candidate
   targets), where a small model could be cheaper and as good. It does not pay off for open-ended planning, where the
   foundation model is far ahead.
5. **Every step keeps the model's output a proposal that software checks** (ADR-0019). No approach here gives a model
   authority over permissions, execution or verdicts.

## What to do first, later, and not at all

| Now (Phase 0–1, small budget) | Later (Phase 2–4, evidence-gated) | Not worth building now |
|---|---|---|
| This strategy; ADR-0036 | Experience store with provenance and splits | Training a model from scratch |
| Lab = benchmark harness + more apps + proxy-level faults | Retrieval in the product, tenant-scoped | A 100,000-record dataset as a goal in itself |
| Experiment 0001: repair planning with retrieved verified experience | Fine-tuning a small specialist on verified data | Buying GPUs (prices in Nov 2026 make renting far cheaper) |
| Two or three more resettable third-party apps | Larger, more diverse lab; unseen-family tests | In-run "self-healing" by a model (it would break ADR-0019) |
| Re-measure with the 2026-11-24 prompt (pending) | Reinforcement learning research, only if gates are met | Mining GitHub issues at scale before the lab proves value |

## The owner's data targets, assessed

The brief's targets (10,000 verified UI failures and fixes, 5,000 selector problems, 3,000 auth problems, 5,000
requirement-to-test examples, 100,000+ experiences) are **useful as a direction and misleading as a plan**:

- **Count is the wrong unit.** A thousand variants of one selector fault teach one skill. The unit that matters is
  *distinct, verified skill instances across application families*, and the lab should report that.
- **Public data rarely contains verified UI failures.** GitHub issues describe symptoms; a reproduced failure with a
  verified fix is the exception. Human verification would dominate cost (estimated $2–13 per example, so $20,000–
  $130,000 for 10,000; [cost-and-infrastructure.md](cost-and-infrastructure.md) §4).
- **Synthetic faults on resettable apps scale at near-zero marginal cost with automatic verification**, but they risk
  teaching the lab's apps rather than testing. Diversity of applications matters more than volume of faults.
- **"Selector problems" are partly already solved by design.** Autestiq's locators are chosen and checked by software
  (ADR-0024), and plans are not repaired during runs. The equivalent skill here is *choosing a robust target and
  re-planning after a failure*, which the first experiment measures.

**Proposed reframing:** the target for Phase 1–3 is "enough verified, diverse experience to measure whether learning
helps", in the hundreds to low thousands, not tens of thousands. Volume targets come after the first positive result.

## Risks that could sink the programme

| Risk | Why it is real | Mitigation |
|---|---|---|
| **Memorisation mistaken for learning** | Synthetic faults on a few apps are easy to memorise | Held-out apps and families; report cross-app results first ([evaluation.md](evaluation.md)) |
| **Contaminated evaluation** | Training or retrieval sees held-out scenarios | Splits fixed before collection; the benchmark's unseen and clean sets never enter the store |
| **A learned shortcut that passes broken apps** | Optimising for "pass" rewards weak checks | Defect detection and false positives gate every result; inconclusive is never success |
| **Customer data leaking into shared learning** | The most valuable data is the most sensitive | Off by default, explicit per-tenant opt-in, never automatic ([security-and-governance.md](security-and-governance.md)) |
| **Spending on infrastructure before evidence** | GPUs and stores are easy to buy and hard to justify | Every phase has a stop/go gate; no paid resources without the owner's approval |
| **Distraction from the product** | The pilot needs hosting and the minimised AI mode | The programme's first phases reuse the benchmark harness and fit beside pilot readiness |

## Decisions the owner needs to make

1. **Accept or change the order:** lab and retrieval first, fine-tuning only after evidence, no training from scratch
   (ADR-0036).
2. **Approve Experiment 0001** as designed, at its estimated cost of **about $25–40 of model calls (two passes, corrected before any run)**
   ([experiments/0001](experiments/0001-repair-planning-from-verified-experience.md)).
3. **Approve adding two or three more self-hostable applications** to the lab (candidates in
   [data-sources.md](data-sources.md) §2).
4. **Decide the policy for customer-derived experience:** never, or per-tenant explicit opt-in with the rules in
   [security-and-governance.md](security-and-governance.md).
5. **Approve creating the GitHub issues** drafted in [roadmap.md](roadmap.md) §3, or keep them as drafts.
6. **Set a research budget ceiling** for Phase 1–3. This document recommends **$500** until Experiment 0001 reports.
