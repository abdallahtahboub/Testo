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

### D-1 · Nothing requires a sign-in — **Critical** · *Fixed 2026-11-13, Milestone 3.1*

`GET /app` with no cookie answers **200 OK** and renders the page.

There is no `[Authorize]` anywhere in `src`, no fallback authorization policy and no
`RequireAuthorization()`. `Testo.Web/Identity/TestoIdentityServiceCollectionExtensions.cs:83`
calls `AddAuthorization()`, which registers services that nothing then consumes.

**Why it is not an incident today.** `/app` is a placeholder, and the data services fail closed:
`CurrentUser.Can()` is false when there is no role, and `IOrganizationContext.RequireOrganizationId()`
throws rather than guessing. So no tenant data is exposed. The gate is simply absent.

**Why it cannot wait.** Every module page from the testing-journey milestones would be built on
the assumption that the gate exists.

**Fixed.** Not with a fallback policy, which was the plan and does not fit a Blazor Web App:
`MapRazorComponents` registers a catch-all endpoint so the router can render the not-found page,
and a fallback challenges that too — a plain 404 on the public website became a redirect to
sign-in. Measured, not assumed.

Instead:

- `[Authorize]` on the app pages and `[AllowAnonymous]` on the website, the account pages and the
  showcase, each declared once in an `_Imports.razor`;
- `PageAuthorizationTests` fails the build for any routable page that declares neither, and for any
  page under `/app` that is not protected. Removing the attribute was tried; the test named the
  page;
- `AuthorizeRouteView` in the router, because an interactive session navigates inside the circuit
  without touching the server's routing, so the endpoint check alone would not run;
- `AuthorizationTests` asserts the redirect, that a session reaches `/app`, that the public pages
  and the theme switch stay open, and that an unknown address is still a 404.

### D-2 · The tenant guard trusts any pre-set `OrganizationId` — **High** · *Fixed 2026-11-13, Milestone 3.1*

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

**Fixed.** `TenantWriteScope`. A row whose organization matches the request passes as before; one
that does not is refused unless a flow has declared it with `AllowWritingTo`, which lasts until the
block ends.

Five places genuinely need it, and each now says so in a line of code a reviewer can see: signing
in (success and failure both write an audit entry into the organization being entered), accepting
an invitation, both halves of a password reset, and seeding a development database.

Tests: the write is refused; the other organization gains nothing, which is the assertion that
matters; a declared write succeeds; and the permission stops working once its block ends — a
permission that outlived its block would be worse than no guard, because it would read like one.

### D-3 · Invitation and password-reset tokens are written to the log — **Critical** · *Fixed 2026-11-13, Milestone 3.1*

`Testo.Application/ApplicationServiceCollectionExtensions.cs` registers `LoggingInvitationNotifier`
unconditionally, including in production. It logs the raw token at `Warning`.

Anyone who can read the logs can accept any invitation or reset any password. The severity is
zero while nothing is deployed and total the day something is.

**Fixed.** `NotifierStartup.EnsureLinksReachPeople` refuses to start outside development unless a
real sender is registered, and says why. A silent credential leak is now a loud startup failure.

The browser tests run the application as a deployed site, so they had to register a sender of their
own — which is the check proving itself rather than a workaround.

### D-4 · No rate limiting of any kind — **High** · *Fixed 2026-11-13, Milestone 3.1*

`docs/identity-and-access.md` §5 promises "Rate limit per address **and per IP**". Only the
per-account lockout exists: ten attempts, fifteen minutes.

One address can therefore try one likely password against every account in the product without
friction, which is how credential stuffing actually works. The lockout is also an availability
lever: an attacker who knows an email address can keep that person locked out indefinitely.

**Fixed.** `AccountRateLimiting`: a sliding window of thirty posts a minute per address on
`/account/**`, in six segments so the allowance returns gradually rather than all at once on a
boundary somebody could wait for. A refusal carries `Retry-After`.

Only unsafe methods are limited. Reading the sign-in page is somebody arriving, and an office
behind one address arrives all morning; posting is an attempt. Limiting the former would have made
the product unusable for a customer whose staff share an address, which is the denial-of-service
primitive the fix had to avoid rather than create.

The limiter runs before authentication, so a refused attempt costs a dictionary lookup instead of a
password hash and a database read.

**Still required at deployment:** behind a reverse proxy the partition is the proxy's address
unless `UseForwardedHeaders` is configured with the known proxies. The limit still works, as one
bucket for everybody — safe, and much blunter than intended. Recorded as R-8.

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

**Decided — [ADR-0018](decisions/0018-execution-isolation.md), threat model in
[`execution-threat-model.md`](execution-threat-model.md).** A container per run, non-root, no
database, no cloud role; every connection through a proxy that refuses by default and checks the
resolved address rather than the URL, which is what closes DNS rebinding and redirect chains; a
target must be proved before it may be one. Milestone 3.3 builds it and carries the refusals as
tests.

### R-2 · No background work, durable run state, idempotency or tenant propagation — **High when it arrives**

A test run outlives the request that started it. Nothing in the codebase supports that: no queue,
no outbox, no job table, no way to carry the tenant identity into a worker, no cancellation that
survives a restart.

**Decided — [ADR-0018](decisions/0018-execution-isolation.md) §"treated as already compromised".**
The worker never writes to the database and cannot name an organization: it returns a result
naming its run, and the application re-reads the organization from the stored row and writes under
`TenantWriteScope`. The queue and the durable state are Milestone 3.3's to build.

### R-3 · No artifact storage — **High when it arrives**

`docs/domain-model.md` §3 assumes screenshots and traces live in object storage with a row pointing
at them. Neither the storage nor the row exists. Needs tenant-scoped, expiring access, retention and
redaction before the first screenshot is taken, because a screenshot of a customer's staging system
is customer data.

**Decided — [ADR-0018](decisions/0018-execution-isolation.md) §"Evidence".** Keyed by organization,
never public, reached through a short-lived signed URL the application issues after the ordinary
permission check; 90 days, less for traces, which carry request bodies and therefore passwords and
are off unless asked for.

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

### R-8 · Rate limiting needs forwarded headers behind a proxy — **Medium when deployed**

`AccountRateLimiting` partitions on the connection's remote address. Behind a load balancer or a
CDN every request arrives from the same place, so the per-address limit becomes one global bucket:
still a limit, no longer per address.

**Fix at deployment.** `UseForwardedHeaders` with `KnownProxies` or `KnownNetworks` set to the
actual infrastructure. Deliberately not configured blind: trusting `X-Forwarded-For` from an
unknown source lets anybody claim any address and escape the limit entirely, which is worse than
the blunt version.

### R-7 · One database for every tenant — **Accepted, documented**

ADR-0012 chose a shared database with filtered access and listed the consequences, including the
noisy-neighbour problem and buyers who require physical separation. No action; recorded so the
register is a complete picture.
