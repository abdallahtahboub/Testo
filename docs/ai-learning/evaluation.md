# Evaluation: measuring improvement without fooling ourselves

> **Builds on the benchmark's existing philosophy (ADR-0020), which stays authoritative.** This adds what a learning
> programme needs on top: splits, generalisation tests, repeats, and gates.

## 1. What is measured

| Metric | Source today | Note |
|---|---|---|
| Correct verdicts on sound applications | Benchmark | — |
| **Defects detected** on broken variants | Benchmark (defect switches) | The point of testing |
| **False positives** (passed a broken application) | Benchmark | **Gate: zero**, as today |
| False negatives (failed a sound application) | Benchmark | Costly, visible |
| Inconclusive; "passed only the part it planned" | Benchmark (ADR-0035) | **Never counted as success** |
| "Detected nothing" (fails sound and broken alike) | Benchmark | Not a detection |
| Flake across repeated executions | Benchmark | Describes the app and runner |
| Variation across repeated *plans* | **New** | The model's nondeterminism; needs repeats |
| Robustness to environment faults (recovered, inconclusive, or wrongly failed) | **New**, needs fault injection | — |
| Repair success: a failed plan re-planned into a correct one | **New** (Experiment 0001) | — |
| Cross-application and cross-family generalisation | Partly (eShopOnWeb, PetClinic) | Reported first, ahead of in-family results |
| Cost: model, browser, total per verified task; time to verdict | Cost ledger, benchmark | — |

## 2. Splits

- **Fixed before collection,** at application-family level, written in the dataset manifest.
- **Training** and **validation** come from lab families.
- **Held-out:** scenarios written after the system under test was frozen (the unseen sets), and **whole applications
  never trained or retrieved on** (PetClinic today; one of each new family later).
- **The existing held-out, unseen and clean sets are tier 5 permanently** ([data-architecture.md](data-architecture.md)).

## 3. Rules

1. **The comparison is pre-registered.** Before an experiment runs, its document states the hypothesis, the baseline,
   the metrics, the success threshold and the failure conditions ([experiments/README.md](experiments/README.md)).
2. **A gain must hold on held-out families.** In-family gains alone are reported as "in-family only".
3. **No false positive is ever traded for a higher pass rate.** Any increase in false positives fails the experiment,
   whatever else improved.
4. **Inconclusive is not success; partial is not full.** Assertions are never weakened to make a number move.
5. **Repeats where the model varies.** At least three plans per scenario and condition when comparing configurations,
   with results reported per run and as a range, not just a mean.
6. **Small samples are said to be small.** With tens of scenarios, a few points of difference is noise. Report counts,
   not only percentages, and an interval where the sample allows (for proportions, a Wilson interval).
7. **Regression gate:** any change to the prompt, the explorer, the validator, retrieval or a model re-runs the unseen
   sets and the clean set before it ships, with figures published as they come (as for ADR-0035 step 2).
8. **Versions are recorded:** model id, prompt version (a hash of `PlanPrompt.System`), explorer and validator commit,
   dataset version, retrieval configuration.

## 4. What would count as "Autestiq got better"

A change counts as an improvement only if, against the same frozen held-out scenarios and applications, it:
- raises correct verdicts or detections **on held-out families**,
- keeps false positives at zero,
- does not raise inconclusive or "detected nothing" to get there,
- and, for cost claims, lowers total cost per verified task at equal quality.

Anything less is reported as what it is: in-family, or cheaper but worse, or inconclusive.
