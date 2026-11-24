# 0001 — Repair planning from verified experience

- **Status:** Approved by the owner on 2026-11-24; built; **not run**. Nothing below is a result.
- **Designed:** 2026-11-24. **Corrected before any run:** 2026-11-24 (below).

## Correction before any run (2026-11-24)

Building it showed a flaw in the first design. The benchmark explores the application *with* its fault switched on, so
a renamed control would simply be seen by the explorer and planned around: the first plan would never fail. The
realistic case, and the one now built, is **an application that changes after its test was written**:

- **The first plan is made against the version the test was written for** (no change, no defect).
- **It then runs against a changed version** (`tools/Autestiq.Benchmark/AppChanges.cs`), with the scenario's defect
  off and, where it has one, on. Two changes, applied by the benchmark's in-process host as it serves each page, so our
  applications need no code of their own:
  - **retag**: every `data-testid` gets a new value, as when a team renames its test ids;
  - **relabel**: buttons and links get new words for the same actions, from a list per application. Only whole control
    texts change, never a value a test checks.

  These replace the first design's overlay, duplicated label and delayed render. They are environment changes in the
  sense of [fault-injection.md](../fault-injection.md), but of the kind a version upgrade brings. Overlays and timing
  faults need the lab-only fault proxy (L3) and are left for a later experiment.
- **Retrieval uses only what a real repair could observe**: the kind of step that failed, how it named its target, and
  the kind of message. Never the change's name.
- **Each plan runs once per variant** (the benchmark's repeat executions measure flake, which this experiment doesn't
  study). The model's variation is covered by repeating each repair three times.
- **Two passes, with and without test ids.** A free dry run (hand-written plans standing in for the model, 2026-11-24)
  showed that with test ids, *relabel* never breaks a plan, because plans name controls by test id; *retag* breaks most
  of them. So the experiment runs twice: with test ids (measuring repairs after a test-id change) and without them
  (`--without-test-ids`, where plans name controls by role and words, so a relabel bites). The estimate rises to
  **about $25–40** for both passes.

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
≈ **$12–30** per pass; **about $25–40 for the two passes** (correction above). Browser runs are local.

## Configuration to record

Model id; `PlanPrompt.System` hash and the repair prompt's hash; explorer and validator commit; corpus manifest hash;
retrieval rule; fault catalogue version.

## Security requirements

Lab apps only, on loopback; the repair prompt keeps retrieved repairs in the data section; repaired plans go through
the same validator; no customer data anywhere; the held-out family is checked to be absent from the corpus before the
run.

## What was built (2026-11-24)

- `AppChanges` (retag, relabel) applied by the in-process host; unit tests that retag renames only test ids and relabel
  only whole control texts.
- `PlanRepair` on the planning request: the plan that ran, the failed step and the runner's message (inside data
  markers, on one line), and the verified examples (inside their own markers). **A request without it is byte for byte
  what it was**, which the existing golden test holds; the system prompt is unchanged. The product never sets it.
- `Experiment0001` in the benchmark tool: corpus and evaluate phases, the three conditions, experience records as JSON
  Lines, a corpus of verified repairs, structured retrieval, and a refusal to evaluate on an application the corpus
  contains.
- Not built, and not needed for this experiment: versioned scenario files (A3) and the fault proxy (L3).

## How to run it (the owner, on their machine, with a model key)

```bash
dotnet run --project tools/Autestiq.Benchmark -- --target demo --experiment 0001 --phase corpus --corpus docs/ai-learning/experiments/0001-corpus.jsonl --records docs/ai-learning/experiments/0001-corpus-demo.jsonl --max-total-usd 10
dotnet run --project tools/Autestiq.Benchmark -- --target booking --experiment 0001 --phase corpus --corpus docs/ai-learning/experiments/0001-corpus.jsonl --records docs/ai-learning/experiments/0001-corpus-booking.jsonl --max-total-usd 10
dotnet run --project tools/Autestiq.Benchmark -- --target invoices --experiment 0001 --phase evaluate --corpus docs/ai-learning/experiments/0001-corpus.jsonl --records docs/ai-learning/experiments/0001-evaluate-invoices.jsonl --max-total-usd 15
```

Then the same three commands again with `--without-test-ids`, and `-no-ids` in the corpus and records file names
(the evaluate phase reads the matching corpus). Each phase prints its tallies per condition; the records are the raw
results.

## Limitations, stated in advance

- One held-out family of our own apps: a positive result is evidence for *repair planning on similar apps*, not for
  general QA intelligence, and not for customer sites.
- Environment faults as switches are clean versions of real faults; real ones are messier.
- Selector-like failures matter less in Autestiq than elsewhere, because locators are chosen by software (ADR-0024).
