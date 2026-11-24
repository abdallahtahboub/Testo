# 0039 — Migrations run by an operator, and liveness and readiness endpoints

- **Status:** Accepted (the owner approved Milestone 5.1, and asked for R2 without email, on 2026-11-24)
- **Date:** 2026-11-24

## Context
The pilot-readiness review (finding 1.1) found that migrations ran only inside the development seed and that nothing
told a host whether the application could serve. A deployed host could neither be given its schema nor be checked.
Hosting is deferred to the go-live stage (owner, 2026-11-24), so the mechanism must not depend on a provider.

## Decision
1. **The web host never migrates a deployed database.** An operator runs `tools/Autestiq.Operator`:
   - `migrations` lists applied and pending migrations;
   - `migrate` applies every pending one;
   - `migrate --to <migration>` or `--to 0` steps back, **refused without `--yes`**, because stepping back drops what
     later migrations added, data included.
   Development still migrates at start, as before.
2. **`/health/live`** answers 200 while the process answers. **`/health/ready`** answers 200 only when the database
   answers with no pending migration, the key ring encrypts and reads back, and, in container mode, the worker image is
   present; otherwise 503, naming only the failed checks. Both are anonymous (a load balancer has no account) and
   `no-store`; the reasons, which can name servers and directories, go to the log only.
3. **No new dependency**: the checks live in `Autestiq.Application.Operations`, the endpoints in the web host, without
   the ASP.NET Core health-check framework or a PostgreSQL health-check package.

## Consequences
- A release is: back up, `migrate`, start the new version, wait for `/health/ready`. The runbook (R3) writes this
  down once hosting is chosen.
- A new version started before `migrate` is not ready, rather than running against an old schema.
- Rolling back the schema is possible but deliberate; the runbook prefers restoring the backup taken before.

## Alternatives considered
- **Migrate at web start in every environment.** Rejected: several instances would race, a failed migration would take
  the site down mid-start, and the application's role would need DDL rights (R-19 wants it without).
- **An EF migration bundle or SQL scripts.** Possible later for a host without .NET; the operator command uses the same
  migrations and is already how operators work.
- **The ASP.NET Core health-check framework and community checks.** Rejected for now: three checks do not justify new
  packages; the endpoints can move to it if checks multiply.
