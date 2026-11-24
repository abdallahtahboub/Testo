# Security and governance for learning

> **Binding rules come from [`../secure-onboarding-and-privacy.md`](../secure-onboarding-and-privacy.md) and CLAUDE.md.
> This document applies them to learning; it does not relax any of them.**

## 1. Customer data

- **Customer data never becomes shared training or retrieval data automatically.** The default is: a tenant's runs
  inform nobody else's plans, and nothing of theirs enters a shared dataset.
- **Contributing is an explicit, per-tenant, revocable opt-in** by an Owner, audited, scoped to what is contributed
  (proposed: sanitised experience records only, never raw screenshots, DOM or credentials), and limited to approved
  uses (evaluation, retrieval, training each named). Withdrawal removes the tenant's records from future dataset
  versions; already-trained model versions are recorded as having used them.
- **A tenant's own experience may inform its own plans** (tenant-scoped retrieval) under the project's existing AI
  permission, because it goes to the same provider under the same consent.
- **Credentials never go to a model**, in training data or retrieval. Values are already redacted from snapshots
  (ADR-0033); records inherit that, and a sanitiser re-checks them before tier 2.
- **Personal and regulated data** in snapshots of customer applications is never in a shared tier. The lab uses
  synthetic data only, including for healthcare or finance applications.
- **No source code is required**, and the learning programme does not change that: experience comes from execution,
  not repositories.

## 2. Separation

| Boundary | Enforced by |
|---|---|
| Lab apps vs customer sites | Lab runs only against lab-started origins on loopback or a lab network; the fault proxy and lab flags cannot be set by the product's dispatcher; `ExplorationPolicy` checks |
| Lab data vs customer data | Separate storage (schema or database); no shared tables, so no accidental join |
| Held-out vs training | Dataset manifests and an automated check that no tier-5 id appears in a training or retrieval set |
| Model proposals vs permissions | ADR-0019: plans stay in the closed vocabulary; validator, approvals and the worker's guards decide. Retrieval and fine-tuning change proposals, never enforcement |

## 3. Data poisoning and prompt injection

- Pages already address the planner sometimes (R-9). A learning loop can **amplify** that: a poisoned page in a
  training set teaches the injection to a model.
- Mitigations: lab content is ours or pinned; consented customer records are sanitised and reviewed before tier 3; any
  record whose snapshot contains text aimed at an automated reader (the planner already flags this in notes) is
  rejected from training; dataset versions are immutable and attributable, so a bad version can be withdrawn.

## 4. Governance

- **Every dataset version** has a manifest: sources, licences, consent basis, split, creator, date, hash.
- **Every model version**, if one ever exists, records the dataset versions it was trained on, its evaluation, and who
  approved its use. Rollback means reverting to the previous version or to the foundation model.
- **No paid resource, training job or new infrastructure** starts without the owner's explicit approval (CLAUDE.md;
  the brief's rules).
- **Deletion** follows ADR-0031 for evidence; records derived from deleted evidence are removed from future dataset
  versions.

## 5. Claims

- **Private execution is not private AI processing.** A lab or a specialist model hosted by us is still our
  infrastructure. A customer-private model needs the private agent (G-8) and private AI (G-12), and no material
  claims otherwise.
- **No claim of improved autonomy without a published measurement** that meets [evaluation.md](evaluation.md) §4.
