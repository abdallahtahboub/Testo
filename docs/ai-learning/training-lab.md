# The Training Lab (proposed)

> **Proposed, not built.** A controlled place where Autestiq practises QA tasks repeatedly and produces verified
> experience. It is designed as a growth of what exists, so that the lab and the product never diverge.

## 1. Principle: one execution engine

The product's worker, explorer, planner, validator and verdict logic are what the lab exercises. A second engine built
for "training" would measure something customers never run, and its results would not transfer. So:

- **The lab is the benchmark harness, generalised** (`tools/Autestiq.Benchmark`): it already hosts applications, sets
  defects, explores, plans, runs repeatedly, and judges against ground truth with honest verdicts.
- **The worker is the product's worker**, with the same sealed container and egress proxy (ADR-0026).
- **New pieces sit around it**: more applications, a fault catalogue, a scenario catalogue, an experience recorder, and
  dataset tooling.

## 2. The cycle

```
Application ─▶ Scenario ─▶ Fault (or none) ─▶ Exploration ─▶ Plan ─▶ Validation ─▶ Run(s)
      ▲                                                                             │
      │                                                                             ▼
  Evaluation ◀── Learning experiment ◀── Dataset version ◀── Experience record ◀── Verdict vs ground truth
```

Every arrow already exists for our three apps except the last four boxes, which [data-architecture.md](data-architecture.md)
and [learning-and-models.md](learning-and-models.md) describe.

## 3. Components

| Component | Exists? | Proposal |
|---|---|---|
| **Applications** | 3 in-process (resettable), 2 external (not resettable) | Add 2–3 resettable third-party apps in containers with a reset endpoint or a fresh container per scenario ([data-sources.md](data-sources.md) §2) |
| **Deterministic state** | In-process apps reset (`Clinic.Reset`, `Ledger.Reset`); external apps do not | Per-scenario fresh container from a fixed image and seed (the sealed-run machinery already starts containers); a run never depends on a previous one |
| **Scenario catalogue** | C# lists per target, frozen by convention | Versioned scenario files with id, application version, task, baseline, ground truth, split (train / validation / held-out), and author date |
| **Faults** | 21 business-logic switches | A fault catalogue with two mechanisms: switches inside our apps, and a lab-only fault proxy for any app ([fault-injection.md](fault-injection.md)) |
| **Execution** | Worker, sealed container, egress proxy | Unchanged |
| **Verification** | Verdict from the defect switch and the baseline | Unchanged in principle; every fault declares its ground truth and how it is checked |
| **Experience recorder** | Reports and recordings as files | Structured, versioned records ([data-architecture.md](data-architecture.md)) |
| **Dataset tooling** | None | Split, deduplicate, sanitise, version; held-out sets locked before collection |

## 4. What the lab must never do

- **Point at anything but lab applications.** The fault proxy and the lab's acting exploration run only against
  applications started by the lab, on loopback or a private lab network, enforced by the existing `ExplorationPolicy`
  checks and a lab flag that a customer job can never carry.
- **Share a store with customer evidence.** Lab experience and customer evidence live apart (different schemas or
  databases), so no query can join them by accident.
- **Change a verdict to make a number look better.** Inconclusive stays inconclusive; a fault's ground truth is set
  before the run.

## 5. Building it in steps

| Step | Delivers | Size |
|---|---|---|
| L1 | Scenario catalogue as versioned files; splits declared; the existing sets migrated without changing a word | Small |
| L2 | Fresh-container-per-scenario for external apps; PetClinic and eShopOnWeb become resettable | Medium |
| L3 | Lab-only fault proxy: HTTP status, delay, body mutation on named routes | Medium |
| L4 | Experience recorder writing structured records from every lab run | Medium |
| L5 | 2–3 more apps, each with scenarios written from its source before any run (as PetClinic was) | Medium, mostly writing |

Each step is independently useful for the benchmark even if the learning programme stops, which keeps the risk low.
