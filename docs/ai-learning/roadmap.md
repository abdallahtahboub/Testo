# AI learning roadmap (proposed)

> **Proposed, 2026-11-24.** Complements the product roadmap ([`../roadmap.md`](../roadmap.md)) and does not reorder it.
> No dates are committed: each phase starts on the owner's approval and ends at its stop/go gate. Effort is in
> engineer-weeks (estimates); money is in [cost-and-infrastructure.md](cost-and-infrastructure.md).

## 1. Phases

### Phase 0: Strategic foundation (this change)
- **Delivers:** `docs/ai-learning/`, ADR-0036 (proposed), issue drafts, Experiment 0001's design.
- **Gate:** the owner accepts, changes or rejects the strategy and its decisions ([strategy.md](strategy.md)).

### Phase 1: Lab foundation, on the benchmark harness
- **Objectives:** steps L1–L3 of [training-lab.md](training-lab.md): versioned scenario files with splits; a fresh
  container per scenario for external apps; a small set of **environment faults as switches in our own apps** (renamed
  label, duplicated label, overlay, delayed render), enough for Experiment 0001; the re-measurement of the 2026-11-24
  prompt.
- **Dependencies:** none beyond what exists. **Architecture change:** none to the product; benchmark tool only.
- **Effort:** ~2–3 engineer-weeks. **Cost:** under $50 of model calls.
- **Success:** every existing set reproduces its published figures from the new files; each new fault is told apart
  by the hand-written baseline.
- **Security acceptance:** faults exist only in lab apps; a test proves a customer job cannot enable them.
- **Stop/go:** stop if migrating the sets changes any published result.

### Phase 2: Experience collection, and Experiment 0001
- **Objectives:** experience records as files (L4); dataset manifests; deduplication by fault and workflow; tier
  separation; run Experiment 0001.
- **Effort:** ~2–3 engineer-weeks. **Cost:** ~$25–40 for the experiment (two passes), under $100 with repeats.
- **Success:** Experiment 0001 reports, positive or negative, by its pre-registered criteria.
- **Stop/go:** **if retrieval gives no gain on held-out families, stop before Phase 3**, publish the negative result,
  and put the effort into exploration instead.

### Phase 3: Retrieval in the product (only if Phase 2 is positive)
- **Objectives:** tenant-scoped retrieval at planning time; lab-derived shared experience; customer contribution only
  with the opt-in of [security-and-governance.md](security-and-governance.md) (if the owner approves that policy).
- **Architecture change:** new tables in a separate lab schema; pgvector only if structured matching is too coarse
  (needs the owner's approval as a new dependency).
- **Effort:** ~4–6 engineer-weeks. **Cost:** small per plan; storage modest.
- **Success:** the gain reproduces on held-out families through the product path; zero false positives; cost per plan
  within budget.
- **Security acceptance:** tests that no tenant retrieves another's records and that tier 5 is never retrieved.

### Phase 4: Fine-tuning research (only with a frequent, narrow task and enough verified data)
- **Objectives:** one specialist (failure classification first), on an Apache-2.0 base, trained on lab data, compared
  with the foundation model on held-out families.
- **Dependencies:** owner approval of Python tooling in a separate research workspace, and of GPU rental.
- **Effort:** ~4–8 engineer-weeks. **Cost:** a few hundred dollars of GPU and evaluation.
- **Success:** equal or better accuracy than the foundation model on held-out data at clearly lower cost per
  classification. **Stop** otherwise.

### Phase 5: Advanced training (only on evidence)
- **Objectives:** more families; repair planning as a specialist; RL research only if Stage C's conditions hold
  ([learning-and-models.md](learning-and-models.md)).
- **Gate:** a documented plateau under Phases 3–4 and an owner decision with a budget.

### Phase 6: Production learning infrastructure
- **Objectives:** model governance, versioning and rollback, monitoring, continuous evaluation, enterprise privacy
  controls, cost optimisation.
- **Dependencies:** hosting decision; private agent (G-8) and private AI (G-12) for customer-private models.

## 2. Relationship to the product roadmap

- Phases 0–2 use the benchmark tool only and can run beside **pilot readiness** (hosting, G-2's minimised AI mode)
  without competing for product code.
- Phase 3 touches the planning path and therefore follows the same rules as any prompt change: re-measure before
  shipping (ADR-0020, ADR-0035).
- Nothing here moves the testing-journey modules (Milestone 6), which wait until after the first pilot (ADR-0020).

## 3. GitHub issue drafts (not created; awaiting the owner's approval)

The repository's existing issues track the product milestones and carry old numbering. The drafts below avoid
overlapping them. **Proposed parent: "Autestiq: AI Training & Continuous Learning Programme"** (label `ai-learning`),
with these children, each independently reviewable:

| # | Title | Kind | Depends on |
|---|---|---|---|
| A1 | Accept or amend ADR-0036 and the AI learning strategy | Decision | — |
| A2 | Re-measure the unseen sets and PetClinic with the 2026-11-24 prompt | Measurement | — (in progress) |
| A3 | Benchmark scenarios as versioned files with declared splits (L1) | Implementation (tool) | A1 |
| A4 | Environment-fault switches in our three apps for Experiment 0001 | Implementation (lab apps) | A1 |
| A5 | Fresh container per scenario for external apps (L2) | Implementation (tool) | A1 |
| A6 | Experience records and dataset manifests as files (L4) | Implementation (tool) | A3 |
| A7 | Run Experiment 0001 and publish the result | Experiment | A3, A4, A6 |
| A8 | Add RealWorld, InvenTree and Keycloak to the lab with scenarios written from source | Implementation (lab) | A5 |
| A9 | Policy for customer-contributed experience (opt-in design) | Decision + design | A1 |
| A10 | Lab-only fault proxy (L3) | Implementation (tool) | A5 |
| R1 | Research: licence review of candidate apps and datasets by counsel | Research | — |
| R2 | Research: failure-classification specialist feasibility (data volume, base model) | Research | A7 |

Implementation tasks (A3–A8, A10) are separate from research (R1, R2) and decisions (A1, A9).

## 4. Suggested first milestone

**"AI Learning M1: does verified experience help?"** = A2, A3, A4, A6, A7. About 4–6 engineer-weeks and under $100 of
model calls, ending with Experiment 0001's published result and a go/no-go for Phase 3.
