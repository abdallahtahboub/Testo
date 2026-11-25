# 0028 — A versioned plan catalog, and organizations that keep their version

- **Status:** Accepted by the owner on 2026-11-16, with the choices below
- **Date:** 2026-11-16

## Context
Milestone 3.6 enforced limits read from configuration by a free-text plan name. Before billing, contracts need
plans whose terms can change without changing what existing customers agreed to (gap G9). The audit asks for
versioned plan definitions with effective dates, organizations that refer to a version, quotas read from that
version, and an entitlement check that refuses an over-limit start.

## Decision
1. **Two tables shared by every organization, outside the tenant boundary:** `plans` (a code and a name) and
   `plan_versions` (a number, the date it takes effect, monthly browser minutes, runs at once, maximum projects).
2. **A version is never changed or deleted.** It carries the same append-only guard as the audit log and the usage
   ledger. A change to a plan is a new version.
3. **The catalog is seeded by the migration** with fixed ids (`PlanCatalog`), version 1 of each plan with the
   figures the owner confirmed for 3.6:
   - Developer: 100 minutes, 1 run at once, 1 project. The project limit is the owner's.
   - Starter: 500, 2, 3.
   - Growth: 2,500, 5, 10.
   - Scale: 10,000, 10, no limit.
   No prices until cost is measured (3.8) and billing exists (Phase D).
4. **An organization holds one version** (`organizations.plan_version_id`, a foreign key the database enforces).
   **It keeps that version until an operator moves it** (the owner's choice): a new version of its plan does not
   reach it, so its limits never change under it without a decision. The migration put every existing organization,
   all of them on "trial", on Developer version 1.
5. **New organizations start on `PlanCatalog.StartingVersion`,** a constant changed on purpose when a new Developer
   version is published. A test fails if a newer Developer version exists and the constant still names an older one.
6. **Only operators move an organization between versions before billing exists** (the owner's choice).
   `PlanAssignment` takes a version by number, or the plan's latest version in effect, and refuses one that has not
   taken effect. Every move is audited with the old and new version and the operator's name. No customer permission
   reaches it. The operator command (`tools/Autestiq.Operator`) calls it today, and the billing webhook will in
   Phase D.
7. **Limits are read from the version an organization holds, and nowhere else** (`Entitlements`): runs at once and
   monthly minutes at the claim and at a start, and the usage service, which also reports the plan's name and
   version. The per-minute rate limits stay in configuration: they guard against abuse and are not part of a plan.
8. **The project limit is checked and tested now** (`Entitlements.MayAddProjectAsync`), for Milestone 5, which
   builds project creation.

## Consequences
- **Easier:** a plan can change for new customers without touching existing ones; a contract names a version;
  billing in Phase D only has to move an organization between versions.
- **Harder:** a price or limit change is a new version and a decision about who moves to it, never an edit.
- **Must remember:**
  - Publishing a new Developer version means updating `PlanCatalog.StartingVersion` deliberately.
  - Moving organizations onto a lower version needs their agreement, because the product will not do it for them.

## Alternatives considered
- **Organizations follow the latest version.** Simpler to run, but a lowered limit reaches existing customers
  without their agreement.
- **Owners change their own plan.** Before billing, that is a free upgrade.
- **Keep limits in configuration.** No version history; a change applies to everyone at once.

## Built (2026-11-16)
`Plan`, `PlanVersion` and `PlanCatalog`; migration `PlanCatalog`; `Entitlements`; `PlanAssignment`; the operator
command. Tests (`PlanCatalogTests`):
- the migration seeds the confirmed figures;
- a version cannot be changed (fails without the guard);
- the starting version is the latest Developer version;
- a new organization is on Developer version 1 and its room comes from that version;
- moving to Growth changes the limits and is audited with both versions;
- unknown plans or versions are refused and nothing changes;
- a new version with lower limits leaves organizations on the old one until they are moved;
- a version not yet in effect cannot be moved onto;
- the usage service names the plan and version;
- projects stop at the plan's limit, and Scale has none.

The command was also run end to end against a migrated database.
