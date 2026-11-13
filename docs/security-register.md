# Security register

> Opened 2026-11-13, after the architecture review in
> [issue #26](https://github.com/abdallahtahboub/Testo/issues/26).
> Verified against `8af40e34f9057afc2ad0c8cfa062c92df87401d5`.

Two lists, deliberately separate. **Defects** were reproduced; each one names the test that
reproduced it. **Risks** are judgements about what could go wrong and have not been demonstrated.
Conflating the two is how a register stops being read.

Severity is about the control, not today's blast radius. A missing lock on an empty room is still
a missing lock, and the room does not stay empty.

---

## Confirmed defects

### D-1 · Nothing requires a sign-in — **Critical**

`GET /app` with no cookie answers **200 OK** and renders the page.

There is no `[Authorize]` anywhere in `src`, no fallback authorization policy and no
`RequireAuthorization()`. `Testo.Web/Identity/TestoIdentityServiceCollectionExtensions.cs:83`
calls `AddAuthorization()`, which registers services that nothing then consumes.

**Why it is not an incident today.** `/app` is a placeholder, and the data services fail closed:
`CurrentUser.Can()` is false when there is no role, and `IOrganizationContext.RequireOrganizationId()`
throws rather than guessing. So no tenant data is exposed. The gate is simply absent.

**Why it cannot wait.** Every module page from the testing-journey milestones would be built on
the assumption that the gate exists.

**Fix.** A fallback policy requiring an authenticated user; `[AllowAnonymous]` on the website and
account pages; a test asserting that `/app` redirects a signed-out visitor to sign-in, and that the
account pages stay reachable.

### D-2 · The tenant guard trusts any pre-set `OrganizationId` — **High** (not reachable by a user)

`Testo.Persistence/Interceptors/TenantGuardInterceptor.cs`, the `case EntityState.Added:` branch:
a row that already carries an organization is accepted without comparing it to the current context.

**Reproduced.** Acting as organization A, a `Project` carrying organization B's id saved without
complaint, and B could then read it.

**Why it is not an incident today.** No code path lets a request supply that value. Layer 1 (the
query filter) and layer 3 (the indexes and keys) still hold. This is layer 2 not doing its job.

**Why the obvious fix is wrong.** Two flows legitimately write outside the current context, and a
blanket "must equal the current organization" rule would break both:

- accepting an invitation creates a membership in the organization the invitation names, before
  the person belongs to it (`InvitationService.cs`);
- a password reset writes an audit entry into *every* organization the person belongs to, while
  nobody is signed in (`PasswordResetWorkflow.cs`).

**Fix.** An explicit, scoped permission — the two flows declare "I am deliberately writing to
organization X", and every other pre-set id is refused. Tests: the refusal, and both flows still
working.

### D-3 · Invitation and password-reset tokens are written to the log — **Critical on first deployment**

`Testo.Application/ApplicationServiceCollectionExtensions.cs` registers `LoggingInvitationNotifier`
unconditionally, including in production. It logs the raw token at `Warning`.

Anyone who can read the logs can accept any invitation or reset any password. The severity is
zero while nothing is deployed and total the day something is.

**Fix.** Refuse to start outside Development unless a real notifier is registered. That converts a
silent credential leak into a loud startup failure.

### D-4 · No rate limiting of any kind — **High**

`docs/identity-and-access.md` §5 promises "Rate limit per address **and per IP**". Only the
per-account lockout exists: ten attempts, fifteen minutes.

One address can therefore try one likely password against every account in the product without
friction, which is how credential stuffing actually works. The lockout is also an availability
lever: an attacker who knows an email address can keep that person locked out indefinitely.

**Fix.** ASP.NET Core rate limiting per IP on `/account/**`, and a decision about how to respond to
spraying that does not hand an attacker a denial-of-service primitive.

### D-5 · The documentation asserted things that were false — **Medium** *(fixed by this register's own pull request)*

`CLAUDE.md` described the phase as "frontend only, with mock data behind interfaces" — untrue on
both counts. `docs/architecture.md` marked Domain, Application and Persistence `PLANNED` after they
were built and merged, listed eleven data-service interfaces that do not exist, and referred to
`ITestRunLiveFeed` and `IAppEventBus` as though they were contracts in the code.

This is in the register because it has a security consequence: it misled an independent reviewer
about what was and was not protected, and it would mislead the next person to touch the tenancy
code.

### D-6 · The branch model has been bypassed three times — **Medium** *(process)*

`main` has pulled ahead of `develop` three times, each time because **the repository's default
branch is `main`** and a new pull request therefore aims there. Once this left a half-finished
Milestone 2 on `main`.

**Fix.** Set the default branch to `develop`. Only a human with repository settings access can do
it; the API proxy refuses.

---

## Architectural risks

### R-1 · Browser execution is the largest attack surface this product will ever have — **Critical when it arrives**

Driving a browser against a URL a customer supplies invites: SSRF to cloud metadata endpoints and
internal networks, DNS rebinding, redirect chains that leave the allowlist, prompt injection from
a hostile page into any model reading it, malicious downloads, and unbounded model spend.

A Playwright `BrowserContext` is a cookie jar, **not an isolation boundary**. It shares the
process, the file system, the network stack and any ambient cloud credentials.

**Position.** This is a hard gate, not a recommendation: no execution against a customer-controlled
URL until the worker is a separate process in its own container, with its own egress allowlist, no
ambient credentials and no route to the application database. ADR required before any code opens a
browser (see ADR-0017).

### R-2 · No background work, durable run state, idempotency or tenant propagation — **High when it arrives**

A test run outlives the request that started it. Nothing in the codebase supports that: no queue,
no outbox, no job table, no way to carry the tenant identity into a worker, no cancellation that
survives a restart.

### R-3 · No artifact storage — **High when it arrives**

`docs/domain-model.md` §3 assumes screenshots and traces live in object storage with a row pointing
at them. Neither the storage nor the row exists. Needs tenant-scoped, expiring access, retention and
redaction before the first screenshot is taken, because a screenshot of a customer's staging system
is customer data.

### R-4 · The audit log is append-only only through Entity Framework — **Medium**

`TenantGuardInterceptor` refuses to update or delete an `AuditEntry`, and that is tested. Raw SQL,
a migration or anyone with database access can still rewrite it. `docs/identity-and-access.md` §4
says "no edit, no delete, not even by an owner", which is true of the application and not of the
database.

**Fix.** Either soften the claim to what is enforced, or make it true with a hash chain and periodic
verification. A security questionnaire will ask which.

### R-5 · `Security:AllowInsecureSessionCookie` downgrades the session cookie — **Medium**

Correct and necessary for the browser tests, which run the real application on a loopback port with
no certificate. Nothing prevents it being set in a deployed environment, and nothing warns if it is.

**Fix.** Log loudly at startup when it is on, and consider refusing it outside Development once the
test host can be told apart another way.

### R-6 · `DevelopmentData.EnsureSeededAsync` is public and seeds known credentials — **Low**

The only caller checks the environment; the method does not. A second caller added later would not
have to.

### R-7 · One database for every tenant — **Accepted, documented**

ADR-0012 chose a shared database with filtered access and listed the consequences, including the
noisy-neighbour problem and buyers who require physical separation. No action; recorded so the
register is a complete picture.
