# 0001 — Repair planning from verified experience

- **Status:** Designed, awaiting the owner's approval. Not run. Nothing below is a result.
- **Designed:** 2026-11-24

## Why this experiment, and how it differs from the brief

The owner suggested *learning to recover from controlled browser interaction failures*. In Autestiq a model never acts
during a run (ADR-0019), and a failing check is never healed away, so in-run recovery is the wrong shape. The
equivalent that respects the architecture is **repair planning**: after a run fails, the planner is asked for a new
plan, given the failed plan and the run's evidence. A person still approves the new plan, and it is judged like any
other. The question is whether **verified prior repairs**, retrieved and shown to the planner, make those repairs
better, **without teaching it to "repair" tests that correctly caught a defect**.

## Hypothesis (pre-registered)

On held-out scenarios, supplying up to three retrieved verified repairs from other application families **raises the
share of correct repaired plans** compared with repairing without retrieval, **with zero false positives and no loss
of defect detection.**

## Conditions

| Condition | What the planner gets |
|---|---|
| **A. No repair** | The first plan only (the existing behaviour) |
| **B. Repair** | The task, the snapshot, the failed plan, the failed step and its message |
| **C. Repair + retrieval** | As B, plus up to 3 verified repairs from training families, selected by fault kind and action kinds (structured matching, no embeddings) |

## Data

- **Applications:** demo shop and clinic for the retrieval corpus (training families); **invoice desk held out**.
- **Faults:**
  - **environment faults** added as switches in our apps (issue A4): renamed button text, a duplicated label, a
    transient overlay, a delayed render. The first plan should fail; a good repair should pass the sound app;
  - **product defects**: the existing 21 switches. The first plan should fail; a "repair" that passes the broken app
    is a **false positive**.
- **Scenarios:** the existing unseen sets, each paired with the faults that apply to it. Roughly 20–30
  scenario-and-fault instances in the held-out family. Splits are fixed in the manifest before any run.
- **Retrieval corpus:** repairs verified on the training families: the repaired plan passes the sound variant and
  fails every product-defect variant it was paired with. Nothing from the invoice desk, PetClinic or eShopOnWeb.

## Procedure

1. For every instance, plan once (condition A) and run it.
2. For each failed run, repair under B and under C, three times each (the model varies), and run each repaired plan
   against the same variant, plus the sound variant where the fault was a product defect.
3. Record each attempt as an experience record ([data-architecture.md](../data-architecture.md)).

## Verification

Ground truth comes from the switches, decided before any run: an environment fault means the sound behaviour is
correct and a repair should pass; a product defect means the test must keep failing. No human judgement is needed for
the verdicts.

## Metrics and thresholds (pre-registered)

| Metric | Threshold for "positive" |
|---|---|
| Correct repairs on environment faults, held-out family, C vs B | C higher by at least 3 instances out of every 20, across all three repeats |
| **False positives** (a repair passing a broken variant), C and B | **Zero.** Any false positive in C fails the experiment |
| Defect detection kept (repairs still failing product defects) | C no lower than A |
| Inconclusive / partial | Reported; never counted as correct |
| Cost per correct repair | Reported for B and C |

**Failure conditions:** no gain on the held-out family; any false positive in C; a gain only in-family.

## Estimated cost

About 60–90 instances × (1 first plan + 3 repairs × 2 conditions) ≈ 400–600 planning calls at ~$0.03–0.05
≈ **$12–30**. Browser runs are local. One repeat of the whole experiment roughly doubles it.

## Configuration to record

Model id; `PlanPrompt.System` hash and the repair prompt's hash; explorer and validator commit; corpus manifest hash;
retrieval rule; fault catalogue version.

## Security requirements

Lab apps only, on loopback; the repair prompt keeps retrieved repairs in the data section; repaired plans go through
the same validator; no customer data anywhere; the held-out family is checked to be absent from the corpus before the
run.

## Engineering needed (each needs approval, issues A3, A4, A6)

Environment-fault switches in our apps; a repair mode in the benchmark harness; file-based experience records and a
corpus manifest; structured retrieval in the harness. **No change to the product.**

## Limitations, stated in advance

- One held-out family of our own apps: a positive result is evidence for *repair planning on similar apps*, not for
  general QA intelligence, and not for customer sites.
- Environment faults as switches are clean versions of real faults; real ones are messier.
- Selector-like failures matter less in Autestiq than elsewhere, because locators are chosen by software (ADR-0024).
