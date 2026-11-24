# 0038 — Data architecture: one system of record, a class and a lifecycle for every piece of data

- **Status:** Accepted (the owner accepted it on 2026-11-24)
- **Date:** 2026-11-24

## Context
The owner noted on 2026-11-24 that there was no architectural design for data: databases, warehouses, backups and
large volumes. The pieces existed in separate decisions (ADR-0011 PostgreSQL, ADR-0012 shared tenancy, ADR-0027 usage,
ADR-0029 cost, ADR-0031 retention, ADR-0033 secrets) and in registers (G-5, G16, R-4, R-7, R-19), but nothing tied
them together, and two kinds of customer content (what exploring saw, what was sent to the model) had no retention
or deletion at all. The owner deferred hosting to the go-live stage and asked for EU residency to be studied before it
is decided, so the design must not depend on either.

## Decision
The design is `docs/data-architecture.md`. Its decisions:
1. **PostgreSQL stays the one system of record** at every size estimated (to ~1,000 customers). No warehouse, second
   database or sharding is proposed; each change has a measured trigger instead (partitioning, moving blobs out of
   rows, a read replica, a warehouse).
2. **Every piece of customer data has a class, a retention and a deletion path** before it is stored (the inventory,
   §3). What exploring saw and what was sent to the model follow the project's evidence policy, like screenshots.
3. **Analytics never carries content**: views first (Milestone 5.1 R6), a replica when load shows, a warehouse only for
   stated reasons, fed only from the content-free views.
4. **Backups**: point-in-time recovery, RPO ≤ 15 minutes in production, RTO ≤ 4 hours, 35 days' retention; the key ring
   backed up with the database but separately encrypted, and restored together; a restore rehearsed before the pilot
   and quarterly.
5. **Provider-neutral**: only kinds of store every hosting candidate offers (managed PostgreSQL, S3-compatible object
   storage, a key service, structured logs), so the hosting and residency decisions stay open.

## Consequences
- **Easier:** a new kind of data has a place in the inventory before it is built; deletion and residency questions
  have a list to answer against; the hosting choice becomes a mapping of kinds to products.
- **Harder:** clearing snapshots means a test whose snapshot is gone explores again before it is re-planned.
- **Open:** retention of the audit log and of accounting records needs counsel; the residency study may add a region
  per customer, which multiplies each store.

## Alternatives considered
- **A warehouse now.** Rejected: no analytics need it before the pilot, and a second copy of the data is a second place
  to delete from and to secure.
- **Keeping snapshots and payloads forever** for re-planning and the learning programme. Rejected: they are customer
  application content, possibly personal; the owner's rules require retention by policy. The learning programme uses
  lab data, not customer data (ADR-0036).
- **Choosing a provider's services now** (for example a managed blob store and key vault by name). Rejected: the owner
  deferred hosting; the design names properties instead.
- **A database per tenant.** Rejected again for now (ADR-0012); kept as the answer for a customer who needs physical
  separation.
