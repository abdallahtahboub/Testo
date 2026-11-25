# 0031 — Evidence kept by policy, and deleted when its time is up

- **Status:** Accepted
- **Date:** 2026-11-17

## Context
A run keeps a screenshot of every step. On a customer's site, a screenshot shows whatever the site showed: names,
emails, orders. The owner's requirements treat evidence as customer data, retained and deleted by policy
(`docs/secure-onboarding-and-privacy.md`; gap analysis G-5; commercial readiness G12). Until now screenshots were
kept on disk indefinitely. The domain model already decided 90 days by default, with run results kept for good
(`docs/domain-model.md` §7). It was the last thing blocking a pilot with real data.

## Decision
- **A policy per project:** keep screenshots for 90 days (the default), 30, 7, or not at all
  (`Project.EvidenceRetentionDays`; `EvidenceRetention.AllowedDays`). Nothing is kept longer than 90 days. Per
  project rather than per organization, because a project is one product with one kind of data: a public demo and a
  system full of patient records belong to the same customer. Changing it needs `ManageProjects` (Owner, Admin, QA
  lead) and is audited with the before and after (`project.evidence_retention_changed`). The project settings page
  (`/app/p/{key}/settings`) offers it.
- **A retention job** (`EvidenceRetentionService`, hourly) deletes the screenshots of every run whose screenshots
  were captured longer ago than its project keeps them. A shorter policy therefore reaches screenshots already
  kept, within the hour.
  - **Files first, then rows.** A file that cannot be deleted keeps its row, and the next sweep tries again. A row
    deleted first would leave a file nothing points to. A row whose file is gone serves nothing: the artifact
    endpoint answers "not found".
  - Each step's link to its screenshot is cleared. **The run, its result and its steps are kept.**
  - **One audit entry per project and sweep** (`evidence.expired`) records how many runs' and screenshots' evidence
    went under which policy, so deletion can be shown as well as done.
  - A sweep handles at most 200 runs, so it is never one long transaction; a full sweep is followed by another at
    once.
- **"Keep no screenshots" stores none.** The dispatcher does not store a screenshot for a run of such a project. The
  worker's files go with its workspace, which is deleted after every run.
- **The run page says so** where a step that ran has no screenshot: deleted under the project's policy, or never
  kept.

## Consequences
- **Easier:** a customer can be told, and shown, how long their screenshots are kept and that they are deleted.
- **Must remember:** the job runs with the dispatcher. A deployment that does not run the dispatcher keeps
  screenshots until one does.
- **Deferred, stated:**
  - **Object storage.** The store is still a local directory behind `IArtifactStore`. Choosing a store is a hosting
    decision and needs the owner's approval. The policy and the job work through the interface and do not change
    when it does.
  - **Deleting evidence when a project or an organization is deleted.** Neither can be deleted yet; the deletion,
    when built, must remove the evidence through the same store.
  - **Redaction** of screenshots, and a policy for traces, which are not captured yet.
  - **The cost report** still prices storage as one month per run; it can use each project's policy instead.

## Alternatives considered
- **A policy per organization**, as the domain model first said. Simpler, but one customer's projects can differ in
  how sensitive their data is, and the project is where the person deciding works.
- **A free number of days.** A short list is easier to reason about, to explain in a contract, and to test.
- **Deleting rows and files in one step by database cascade.** The files are not in the database. A cascade would
  delete the rows and leave the files.
