# 0011 — PostgreSQL as the database

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Testo needs persistence. It is sold as a service, so every database instance is a cost we
carry, not something the customer installs. The first market is Germany, where customers
ask where their data physically sits.

## Decision
PostgreSQL, through EF Core with the Npgsql provider.

## Consequences
- No licence cost per instance, which matters when each customer environment is ours to pay for.
- EU hosting is easy and cheap: managed PostgreSQL exists at Hetzner, Scaleway, Azure and
  AWS in German and other EU regions.
- `jsonb` suits the parts of the model that are genuinely variable — capability settings,
  step data, the before/after values in an audit entry — without a table per shape.
- Full-text search is built in, so global search needs no second system at first.
- `xmin` gives optimistic concurrency with no extra column.
- The team is more familiar with SQL Server, so some habits transfer imperfectly: no
  clustered indexes in the same sense, different date and identity handling, `citext` or a
  lower-case index for case-insensitive email lookups.
- Azure-native tooling that assumes SQL Server does not apply.

## Alternatives considered
- **SQL Server / Azure SQL.** The familiar choice for a .NET team, with excellent tooling.
  Rejected on running cost: it is the single largest fixed cost per environment, and nothing
  in this product needs what it offers over PostgreSQL.
- **SQLite.** Fine for tests, not for a multi-tenant service.
