# Pilot runbook

> **Status:** written 2026-11-25 for Milestone 5.1 R3 and **rehearsed once, locally** (record:
> [`rehearsals/2026-11-25-local.md`](rehearsals/2026-11-25-local.md)). **Provider-neutral**: hosting is deferred to the
> go-live stage and no provider is named. Where a step depends on the host, it says so. Every release follows these
> steps for one exact commit and one exact worker-image digest, written into the release notes.

## 1. What a deployment consists of

| Part | What it is | Kept where | Backed up |
|---|---|---|---|
| Web host | `Autestiq.Web`, published (`dotnet publish -c Release`) | The host's application directory | No: rebuilt from the commit |
| Worker image | `src/Autestiq.Worker/Dockerfile`, by digest | The host's container registry | No: rebuilt from the commit |
| Database | PostgreSQL 16 | Managed PostgreSQL | **Yes**, continuously (§6) |
| Key ring | ASP.NET Core Data Protection keys (ADR-0033) | `DataProtection:KeyDirectory`, outside the database | **Yes, with the database, encrypted separately** |
| Evidence | Screenshots (ADR-0031) | `Artifacts:Directory` until object storage | Yes, with the database |
| Operator tool | `tools/Autestiq.Operator`, published | Wherever operators run it | No |

## 2. Configuration (environment variables; secrets only from the host's secret store)

| Setting | Meaning | Required |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | Production behaviour: refusals below, HSTS, error page | Yes |
| `ConnectionStrings__Autestiq` | The database (a role without DDL rights once R-19 is built) | Yes |
| `DataProtection__KeyDirectory` | The key ring's directory, readable only by the application | Yes: refuses to start without |
| `Artifacts__Directory` | Where screenshots are kept | Yes: refuses to start without |
| `Email__Smtp__Host`, `__Port` (587), `__User`, `__Password` (secret), `__From`, `__PublicBaseUrl` (https) | The provider's SMTP relay (ADR-0040) | Yes: refuses to start without email |
| `Execution__Isolation=Container`, `Execution__WorkerImage=<image@digest>` | Sealed runs (ADR-0026) | Yes for customer sites |
| `Planning__*`, `Planner__ApiKey` (secret) | AI planning and its budgets (ADR-0030) | Only where AI planning is offered |
| `Security__AllowInsecureSessionCookie` | **Never in a deployment.** Only for a local rehearsal over plain http (R-5) | No |

TLS ends at the host's load balancer or reverse proxy; forwarded headers must be configured with it (R-8).

**Start the web host from its own directory** (`cd <app dir> && dotnet Autestiq.Web.dll`), or pass its directory as the
content root: started from elsewhere, ASP.NET Core looks for the static files in the wrong place and serves them empty
(found in the rehearsal).

## 3. A release

1. **Back up** (§6) and note the backup's time.
2. **Migrate**: `dotnet Autestiq.Operator.dll migrations`, then `migrate`. The web host never migrates a deployed
   database (ADR-0039).
3. **Start** the new web host with the new worker image.
4. **Wait for `/health/ready` = 200.** It checks the database and schema, the key ring, and in container mode the
   worker image. `/health/live` only says the process answers.
5. **Smoke**: sign in, open a project, start a run on a proved test site, see its verdict and screenshot.
6. Record the commit, the image digest and the result in the release notes.

**Rollback**: stop the new host; prefer **restoring the backup taken in step 1** and starting the previous version. A
schema-only rollback is `migrate --to <previous migration> --yes`, which drops what later migrations added, data
included; the newer host then reports `not ready: database`, as it must.

## 4. The first organization of an instance

There is no sign-up. An operator runs, with the email settings in the environment:

```bash
dotnet Autestiq.Operator.dll create-organization <slug> "<name>" --owner <email> --by "<operator>"
```

The owner gets an invitation by email (valid seven days), sets a name and password, and is the organization's Owner.
No password or link is ever printed. Without email configured the command refuses.

## 5. The kill switch

`Execution:Paused=true` stops every new run; `Execution:PausedOrganizations` stops some organizations only. Both are
read live from configuration: set them in a configuration file the host reloads (or the host's configuration
service), not only in an environment variable, which needs a restart. Starting a run then says "Runs are paused".

## 6. Backup and restore (targets: `docs/data-architecture.md` §8)

**Back up together:** the database (the provider's point-in-time recovery, plus a daily full dump), **the key ring**
and the evidence directory. The key ring is useless without the database and the database's test-account values are
unreadable without the key ring.

**Restore**, all three together:
1. Stop the web host.
2. Restore the database to a new database (`pg_restore`), the key ring directory and the evidence directory.
3. Start the web host; wait for `/health/ready`.
4. **Check the key ring matches**: an environment's test-account values must still be usable. *Readiness does not yet
   detect a key ring that does not match the database* (R-20): restored without its key ring, the host creates a new
   one, reports ready, and every stored test-account value is silently unreadable.
5. Sign in, open an old run, and check its screenshot opens or is reported deleted by retention.

## 7. Health, alerts and recovery

- Monitor `/health/ready` from outside; alert when it is not 200 for two minutes. Alerting itself waits for hosting.
- A stuck test in Planning recovers by itself after 15 minutes (ADR-0037); a stuck run is reaped (ADR-0026).
- Before migration, the background jobs log an error every second until the schema exists; migrate first.

## 8. Not covered yet

TLS and forwarded headers on a real host (R-8); a real SMTP provider (only configuration); a public, proved test site
for an end-to-end passing run in container mode; alerting; log collection and retention; object storage; a key
service for the key ring (keys are plain files on disk today, R-16); least-privilege database roles (R-19).
