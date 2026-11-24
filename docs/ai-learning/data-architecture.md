# Data architecture: the experience record (proposed)

> **Proposed, not built.** It reuses what exists before adding anything, and adds no infrastructure dependency without
> the owner's approval.

## 1. One experience, end to end

An **experience** is one attempt at one task, with everything needed to judge it again later:

| Part | Content | Exists today as |
|---|---|---|
| Task | Scenario id and version, task sentence, stated expected results, split | Benchmark scenario (C#); `TestCase.Description`, `ExpectedResults` |
| Environment | Application and version, fault id and version, seed | Benchmark target + defect switch |
| Observation | The `SiteSnapshot` exactly as sent, with secret values already redacted | `TestCase.Snapshot`; `SentToModel` |
| Agent | Planner model id, prompt version, temperature, retrieval configuration | `TestCase.Model`; recordings |
| Plan | Actions, coverage, not-tested list, notes by source, evidence labels | `TestCase.Plan`, `Notes`; ADR-0034/0035 |
| Execution | Steps, statuses, timings, failure messages, screenshots (by policy) | `TestRun`, steps, screenshots |
| Outcome | Verdict (correct, false positive, false negative, inconclusive, detected nothing), flake across repeats | Benchmark `Measurement`, `Verdict` |
| Verification | Ground truth source (switch, baseline, human), who or what verified, when | Implicit in the benchmark |
| Cost | Tokens, model cost, run time | Cost ledger (ADR-0029); `PlanningCost` |
| Provenance | Origin (lab app, public dataset, customer with consent), licence, usage permission, training eligibility | **Missing** |

Most of this is already captured by the product or the harness. What is missing is **one record that ties it together,
versioned, with provenance and eligibility**, and a way to group records into immutable dataset versions.

## 2. Five tiers, kept apart

| Tier | What | Who may read | Retention |
|---|---|---|---|
| 1. Raw evidence | Snapshots, screenshots, run logs | The owning tenant; the lab for lab runs | By policy (ADR-0031) |
| 2. Sanitised experience | The record above, secrets and personal data removed | Lab tooling | Until its dataset versions are retired |
| 3. Approved samples | Sanitised records that passed review and deduplication, with eligibility = training | Training jobs | With the dataset version |
| 4. Rejected samples | Records that failed review, with the reason | Analysts | Kept: negative findings are data |
| 5. Held-out evaluation | Scenarios and records reserved for evaluation | Evaluation only; **never** retrieval or training | Permanent, frozen |

Tier 5 is the guarantee against contamination: the existing held-out, unseen and clean (PetClinic) sets belong here
and never move to tiers 2–3.

## 3. Entities: reuse first

| Proposed entity | Decision |
|---|---|
| TrainingEnvironment | Reuse the benchmark target plus an application version string; no table until the lab hosts apps as containers |
| Scenario | Versioned files in the repo (reviewable, diffable), not a table |
| FaultDefinition | Versioned files beside scenarios |
| AgentExecution / AgentExecutionStep | **Reuse** `TestRun` and its steps for product runs; the harness's `Measurement`/`Attempt` for lab runs |
| RecoveryAttempt | Not a separate entity: a re-plan is another experience linked to its predecessor |
| VerificationResult | A field set on the experience (source, verifier, time) |
| **ExperienceRecord** | **New**, the one genuinely missing piece |
| **DatasetVersion** | **New**: an immutable manifest (ids and hashes of approved records, split, creation date, licence summary) |
| ModelVersion / EvaluationRun | Files in `docs/ai-learning/experiments/` until there is a model to version; the benchmark reports already act as evaluation runs |

## 4. Storage

- **Phase 1–2: files.** JSON Lines records and dataset manifests in a lab directory, versioned by hash. Enough for
  hundreds to low thousands of records, reviewable, no new infrastructure.
- **Phase 3, if retrieval proves useful:** PostgreSQL tables for experience records in a **lab schema or database
  separate from tenant data**. Embedding search with **pgvector** (PostgreSQL licence, runs in the existing database)
  is the first choice, because it adds an extension, not a service. A separate vector database is not justified at
  this scale.
- **Raw evidence at scale** needs object storage, which already waits on the hosting decision (ADR-0031).

## 5. Provenance and permission are fields, not promises

Every record carries `origin`, `licence`, `usage_permission` (evaluation-only / retrieval / training) and
`training_eligible`, set by software from where the record came from, never by the model and never by default to the
most permissive value. A customer-derived record cannot exist without the consent described in
[security-and-governance.md](security-and-governance.md).
