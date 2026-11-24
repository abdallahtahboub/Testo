# AI learning: Autestiq's long-term QA intelligence

> **Status: strategy, proposed (2026-11-24). Nothing here is built unless a document says so.** The owner asked for a
> strategic foundation, not an implementation. Every engineering step in it waits for the owner's approval
> (ADR-0036, proposed).

Autestiq's long-term aim is specialised QA intelligence: an agent that gets measurably better at real testing work by
learning from verified experience. It is **not** a general-purpose model, and owning a neural network is not the goal.
The advantage this strategy pursues is verified domain experience, the execution and verification infrastructure that
produces it, and evaluation honest enough to tell a real improvement from a flattering number.

## How to read this section

Every statement here is one of:

| Label | Meaning |
|---|---|
| **Implemented** | In the code on `develop`, tested. Linked to where it lives. |
| **Implemented, unverified** | In the code, not measured for this purpose. |
| **Proposed** | A design for later, needing the owner's approval. |
| **Research finding** | What a source says, with the source and the date. |
| **Estimate** | A number derived from stated assumptions, never an observation. |
| **Open question** | Undecided; also listed in [`../open-questions.md`](../open-questions.md). |

## The documents

| Document | What it answers |
|---|---|
| [strategy.md](strategy.md) | The executive summary: what to build, what to postpone, what not to build, and why. Start here. |
| [current-state.md](current-state.md) | What Autestiq already has that learning can build on, classified as implemented, partial, or missing. |
| [training-lab.md](training-lab.md) | The proposed Training Lab: the benchmark harness grown into a practice environment, not a second engine. |
| [fault-injection.md](fault-injection.md) | How controlled, reproducible faults would be created, and which are safe. |
| [data-architecture.md](data-architecture.md) | The experience record, what existing tables it reuses, and the five data tiers. |
| [data-sources.md](data-sources.md) | Applications, public datasets and benchmarks: licence, use, and recommendation for each. |
| [learning-and-models.md](learning-and-models.md) | Retrieval, fine-tuning, reinforcement learning and training from scratch, assessed separately; the model strategy. |
| [evaluation.md](evaluation.md) | How improvement is measured without fooling ourselves. |
| [security-and-governance.md](security-and-governance.md) | Customer data, consent, provenance and the lab's isolation. |
| [cost-and-infrastructure.md](cost-and-infrastructure.md) | GPU, API and labour costs, dated and sourced, with budget scenarios and rent-versus-buy. |
| [roadmap.md](roadmap.md) | Phases 0 to 6, their gates, and the GitHub issue drafts. |
| [experiments/](experiments/README.md) | The experiment log, and the first experiment's design. |

## The rules for this section

- **Authoritative content is linked, not copied.** The benchmark figures live in ADR-0020 and `docs/ai-benchmark*.md`;
  the security rules in [`../secure-onboarding-and-privacy.md`](../secure-onboarding-and-privacy.md).
- **An experiment is recorded whether it succeeds or fails**, with its date, hypothesis, model and data version,
  configuration, results, cost and limits ([experiments/README.md](experiments/README.md)).
- **Estimates are never presented as results.** No document here claims an improvement that a published measurement
  does not show.
- **The product roadmap ([`../roadmap.md`](../roadmap.md)) comes first.** This programme complements it and does not
  reorder it without the owner's decision.
