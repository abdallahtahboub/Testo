# 0036 — An AI learning programme: verified experience and retrieval first, no model trained from scratch

- **Status:** Proposed
- **Date:** 2026-11-24

## Context
The owner's brief of 2026-11-24 sets a long-term aim: specialised QA intelligence that improves measurably by learning
from verified testing experience. It asks for a strategy that challenges its own assumptions, with no engineering,
training or paid infrastructure until the owner approves. The strategy is in `docs/ai-learning/`.

What the repository shows (`docs/ai-learning/current-state.md`):
- Autestiq is **deterministic software around one bounded model call** (ADR-0019). The model writes a plan in a closed
  vocabulary; software explores, chooses locators, validates, labels evidence, decides coverage, enforces approval and
  runs.
- The measured losses (ADR-0020) are **mostly observation problems**: states not explored, unlabelled fields,
  read-only exploration. They are seldom gaps in the model's QA knowledge.
- The benchmark harness already provides **ground truth** (21 defect switches, hand-written baselines), honest verdicts
  (inconclusive, "detected nothing", partial), held-out and unseen sets, recorded answers and costs.

## Decision (proposed)
1. **Order of work.** Grow the benchmark harness into a Training Lab; collect verified experience on resettable
   applications; measure **retrieval of verified experience** first; fine-tune a small open-weight specialist only for a
   narrow, frequent, checkable task once retrieval and data justify it; treat reinforcement learning as research gated on
   evidence.
2. **No foundation model trained from scratch.** Nothing in Autestiq's problem needs one, and it would spend resources
   where open models are free and large labs unbeatable. Revisited only with new evidence and an owner decision.
3. **One execution engine.** The lab exercises the product's explorer, planner, validator and worker. No separate
   "training" engine.
4. **The model stays a proposer.** Retrieval and fine-tuning change what a model proposes, never what software enforces
   (ADR-0019): vocabulary, validation, approvals, guards and verdicts stay deterministic.
5. **Customer data is never shared training or retrieval data by default.** Contribution, if the owner adopts it, is an
   explicit, per-tenant, revocable, audited opt-in, limited to sanitised records and named uses.
6. **Evaluation is pre-registered and held out at application-family level.** The existing held-out, unseen and clean
   sets are evaluation-only forever. A gain counts only on held-out families with zero false positives and no rise in
   inconclusive.
7. **Every phase has a stop/go gate**, and no paid resource, training job or new infrastructure starts without the
   owner's approval.

## Consequences
- **Easier:** improvements stay measurable and reversible; the first phases reuse the harness and cost little; any
  foundation model can benefit from retrieval.
- **Harder:** volume targets give way to verified diversity, so dataset counts grow slower; a negative result from
  Experiment 0001 would redirect effort to exploration rather than to learning.
- **Risks:** memorising lab apps instead of learning; contaminated evaluation; data poisoning; distraction from pilot
  readiness. Mitigations in `docs/ai-learning/strategy.md` and `evaluation.md`.

## Alternatives considered
- **Start with fine-tuning.** Rejected for now: there is no verified dataset yet, the measured losses are not the kind
  fine-tuning fixes, and it adds Python tooling and GPU spend before evidence.
- **Collect tens of thousands of examples first.** Rejected as a first step: public sources rarely contain verified UI
  failures, human verification would cost tens of thousands of dollars, and volume without diversity teaches little.
- **Adopt a public agent environment (BrowserGym, WebArena) as the engine.** Rejected: a second engine measures what
  customers never run. Their tasks may serve as evaluation only.
- **Train a proprietary model from scratch.** Rejected (decision 2).
