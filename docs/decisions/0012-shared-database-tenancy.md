# 0012 — One database, one row per tenant, filters enforced centrally

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Each customer company is an `Organization`. Their data must never be visible to another
customer. That promise is the thing a B2B buyer checks hardest, and a leak would end the
product.

## Decision
One database shared by all organizations. Every entity inside the tenant boundary carries
`OrganizationId`, and isolation is enforced in three layers (`domain-model.md` §4):

1. A global EF Core query filter, so the `WHERE` clause is never written by hand.
2. `OrganizationId` set on creation from the request context, and rejected if changed.
3. `organization_id` first in every index that matters, and part of the foreign keys.

A test fixture runs the same queries as two organizations and fails if either sees the
other's rows. It discovers entities by reflection, so a new entity is covered as soon as
it exists.

## Consequences
- Cheapest to run and simplest to operate: one database, one migration, one backup.
- A schema change applies everywhere at once, which is also the risk.
- Isolation depends on code being correct. That is why it is enforced three times and
  tested, rather than left to discipline.
- A "noisy neighbour" is possible: one large customer can affect others. Watchable through
  per-organization metrics, solvable later by moving that customer out.
- Some enterprise buyers require physical separation. That becomes an offer — a dedicated
  deployment — rather than a change to this model, because the schema is identical either way.

## Alternatives considered
- **A database per customer.** The easiest answer to a security questionnaire, and the
  honest answer for a regulated buyer. Rejected for now: every migration has to run
  everywhere, provisioning becomes a system of its own, and it is a large operational
  burden before the first customer exists. Kept as a future option for enterprise deals.
- **A schema per customer.** Middle ground, but EF Core migrations across many schemas are
  awkward and the isolation gain over a filtered column is small once the filter is central.
