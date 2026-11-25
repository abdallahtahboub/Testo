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
`RequireAuthorization()`. `Autestiq.Web/Identity/AutestiqIdentityServiceCollectionExtensions.cs:83`
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

`Autestiq.Persistence/Interceptors/TenantGuardInterceptor.cs`, the `case EntityState.Added:` branch:
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

`Autestiq.Application/ApplicationServiceCollectionExtensions.cs` registers `LoggingInvitationNotifier`
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

**Partly built (2026-11-14, Milestone 3.3.)** The proxy exists and refuses by default, judging
every resolved address of every connection, including each hop of a redirect chain; 45 tests in
`tests/Autestiq.Worker.Tests/Egress` cover loopback, private ranges, link-local, carrier NAT, IPv4
mapped into IPv6, NAT64 and cloud metadata, over real sockets. A page's own `fetch()` to
`169.254.169.254` is refused. **Not yet built:** the container per run. The worker is a separate
process with no database connection and no cloud role, which is the shape ADR-0018 asks for but
not the boundary — a process is not a sandbox. Only our own demo target is reachable, so nothing
customer-supplied runs yet; the container has to exist before anything does.

### R-2 · No background work, durable run state, idempotency or tenant propagation — **High when it arrives**

A test run outlives the request that started it. Nothing in the codebase supports that: no queue,
no outbox, no job table, no way to carry the tenant identity into a worker, no cancellation that
survives a restart.

**Decided — [ADR-0018](decisions/0018-execution-isolation.md) §"treated as already compromised".**
The worker never writes to the database and cannot name an organization: it returns a result
naming its run, and the application re-reads the organization from the stored row and writes under
`TenantWriteScope`. The queue and the durable state are Milestone 3.3's to build.

**Built (2026-11-14, Milestone 3.3.)** `test_runs` is the queue: `RunDispatcher` claims a row
under optimistic concurrency, so two dispatchers cannot take the same run, and a result that names
a different run than the one dispatched is recorded as an infrastructure error rather than
believed. Cancellation is a flag on the row, so it survives a restart. **Still open:** one
dispatcher per process polls, which is right for one machine and not for several; and a run left
`Running` by a host that died is not yet reaped.

### R-3 · No artifact storage — **High when it arrives**

`docs/domain-model.md` §3 assumes screenshots and traces live in object storage with a row pointing
at them. Neither the storage nor the row exists. Needs tenant-scoped, expiring access, retention and
redaction before the first screenshot is taken, because a screenshot of a customer's staging system
is customer data.

**Decided — [ADR-0018](decisions/0018-execution-isolation.md) §"Evidence".** Keyed by organization,
never public, reached through a short-lived signed URL the application issues after the ordinary
permission check; 90 days, less for traces, which carry request bodies and therefore passwords and
are off unless asked for.

**Partly built (2026-11-14, Milestone 3.3.)** Screenshots are stored under
`{organization}/{run}/`, with an `artifacts` row inside the tenant boundary, and are served only
to a signed-in person holding an unexpired HMAC signature for that one artifact — and then only
through the tenant filter, so another organization's artifact is *not found* rather than refused.
Tested in `ExecutionIsolationTests` and `ArtifactAccessTests`. **Not yet built:** object storage
(the store is a local directory behind `IArtifactStore`), retention and redaction. No traces are
captured yet, which is the one part of the decision that needs nothing further to hold.

**Retention built (2026-11-17, [ADR-0031](decisions/0031-evidence-retention.md)).** A policy per project (90, 30 or
7 days, or none kept), an hourly job that deletes expired screenshots, files first and then rows, audited per
project. A project that keeps none has no screenshot stored. **Still open:** object storage, which is a hosting
decision, and redaction.

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

### R-9 · A hostile page can talk to the planner — **High, and permanent**

The moment a model reads a page to decide what a test should do, the page is in the prompt and can
ask for anything. Threat model §4.3 describes it; it is not a bug to be fixed but a condition of
using a model on input we did not write.

**Bounded — [ADR-0019](decisions/0019-the-planner-boundary.md).** The model plans once, before the
browser opens, so there is no prompt to inject into while a run is under way; its output is a closed
vocabulary with no field in which a selector or a script could be written; and the plan is validated
against what the crawl actually observed. A foreign origin is refused. An invented target is **not**
refused: the validator only notes it, and the run goes ahead, so a guessed identifier reaches the
browser. Found in the 2026-11-15 benchmark, where five plans acted on identifiers the crawl never saw
(see `docs/ai-benchmark.md`). Closing that gap is part of the analysis work, not this register's claim.
prompt also says the page content is data, which is advisory and recorded as such: the things that
hold are the type, the validator and the proxy, none of which depend on the model behaving.

**What remains.** A hostile snapshot can still influence *which* valid plan is chosen — a worse test,
never a different policy. Accepted, and the reason the vocabulary and the snapshot's fields both stay
deliberately short.

### R-10 · Model spend is a denial of service against ourselves — **Medium**

A planner that loops, a retry that retries forever, a bill nobody reads. Threat model §4.7 asks for
token and money budgets per run and per organization.

**Partly addressed — ADR-0019 §6.** The per-run token and money ceilings end a run as
`BudgetExceeded`, which is deliberately not `Failed`. **Not built:** per-organization monthly spend
limits, which need billing to exist first.

### R-8 · Rate limiting needs forwarded headers behind a proxy — **Medium when deployed**

`AccountRateLimiting` partitions on the connection's remote address. Behind a load balancer or a
CDN every request arrives from the same place, so the per-address limit becomes one global bucket:
still a limit, no longer per address.

**Fix at deployment.** `UseForwardedHeaders` with `KnownProxies` or `KnownNetworks` set to the
actual infrastructure. Deliberately not configured blind: trusting `X-Forwarded-For` from an
unknown source lets anybody claim any address and escape the limit entirely, which is worse than
the blunt version.

### R-11 · The loopback exception would apply to a stored target — **Fixed (2026-11-16, Milestone 3.5)**

**Fixed.** `RunDispatcher.JobFor` takes the trusted-local origins from `ExecutionOptions` only: the configured
`LocalOrigins`, or the one configured `TargetOrigin`. A run's stored target is never added.
`TrustedLocalOriginsTests` writes a loopback origin to a run and expects it not to be trusted; it fails with
the old code. What follows is the finding as it was recorded.

`EgressPolicy.Check` lets a loopback address through when the origin is in `TrustedLocalOrigins`
(`src/Autestiq.Worker/Egress/EgressPolicy.cs`, the `isTrustedLocal && reach == Reach.Loopback` branch).
`RunDispatcher` sets that list from the run's own `TargetOrigin` (`src/Autestiq.Application/Execution/RunDispatcher.cs:140`).
Today `TargetOrigin` comes from configuration (`ExecutionService.cs:231,287`), so the exception only
ever admits our demo target. The first customer-supplied URL would turn it into a route to whatever
listens on the worker host's loopback interface.

**It now guards a second thing.** Whether an exploration may press an application's controls and fill
its forms is decided from the same list ([ADR-0022](decisions/0022-filling-forms-while-exploring.md)).
The policy also requires a loopback address written as a number, so a customer's application on the
internet is refused whatever the list says. A customer-supplied loopback address would pass both.

**Fix before any customer target.** Trusted-local origins come from configuration only, never from a
stored run. A regression test writes a loopback origin to a run row and expects a refusal.
Tracked as gap G2 in [`commercial-readiness.md`](commercial-readiness.md).

### R-12 · Concurrency is a per-process limit, not a per-organization one — **Fixed (2026-11-16, Milestone 3.6)**

**Fixed.** The claim counts the organization's running runs under a lock on its row, inside the claim's
transaction, against its plan's limit (ADR-0027). `MaxConcurrentRuns` remains as a per-process ceiling that
protects the machine, not the tenants. `TwoDispatchersRacing_NeverRunMoreThanTheOrganizationsLimit` covers it.
What follows is the finding as it was recorded.

`MaxConcurrentRuns = 2` is enforced by a `SemaphoreSlim` inside each process
(`RunDispatcher.cs:41`). Two processes allow four runs; one organization can take all of them.
Nothing in the database counts an organization's active runs. **Fix:** a count under a row lock in
the claim transaction (gap G7 in [`commercial-readiness.md`](commercial-readiness.md)).

### R-13 · Customer page content reaching an AI provider — **High; mitigated in Milestone 5 part 2, minimized mode open**
The crawl's snapshot holds page titles, element names and visible text, which on a customer's staging site can be
names, emails and orders. Planning sends it to the model provider. **Mitigation, required before part 2 ships:**
a per-project AI data-processing permission, off by default and enforced in the service; a record of what each
call sent; later a measured minimized mode (gap analysis G-1, G-2). **Built 2026-11-17:** the permission and the
record (ADR-0030 part 2). The permission is checked when a test is described and again just before each call.
**Open:** the minimized mode; until it exists, a project that allows AI planning sends visible page text as found.

### R-14 · An approved plan changing or destroying customer data — **High; mitigated in Milestone 5 part 3**
A plan may place an order, book a slot or cancel something. **Mitigation:** production refused until an owner
authorizes it (built, G-3); plans that may change data flagged and explicitly approved (G-4); ownership proofs.
**Built 2026-11-17:** `PlanEffects`, approval with a plan fingerprint checked again by the dispatcher, and a
read-only guard in the browser for plans approved without the acknowledgement (ADR-0030 part 3). **Open:** a link
that changes data when followed is neither flagged nor blocked.

### R-15 · Misrepresenting where processing happens — **Medium, permanent**
Our sealed container runs on our infrastructure; it is not private execution, and private execution is not
private AI processing. **Mitigation:** wording reviewed against `docs/secure-onboarding-and-privacy.md` §3–4.

### R-16 · Holding customers' test-account credentials — **High; mitigated 2026-11-18 (ADR-0033)**
We hold a customer's test sign-in values, so our servers and the key ring together would reveal them.
**Mitigation:** one test account per test environment, never production; write-only, audited by name; encrypted with
Data Protection under a purpose naming the organization and environment, with the key ring outside the database and
required outside development; decrypted only when a permitted run starts; never to a model, enforced by the validator
(names only) and by the worker replacing every value in what exploring describes. **Open:** the key ring is a
directory until hosting brings a key service; a user name is visible in screenshots; the interim ends with a private
agent (G-8).

### R-17 · AI planning can stick, undercount and overspend — **Medium; mitigated 2026-11-24 (ADR-0037); reservations open**
`PlanningDispatcher` saves a test as Planning and only then calls the provider (`src/Autestiq.Application/Testing/PlanningDispatcher.cs:87-98`).
Nothing selects Planning again, so a crash or restart leaves the test stuck for good, and its expected results can no
longer be edited (`TestCaseService.cs:174`). The cost is written only after the call (`:162`), so a crash after the
provider billed **loses the spend from the budget**. The budget is read and then spent with no reservation
(`AiBudget.LeftAsync`), so two planning instances can spend the same room. One instance plans one test at a time,
so a single instance overspends by at most one plan's ceiling.
**Fix (Milestone 5.1, R1):** an attempt row written before the call and counted at its ceiling until completed; a
lease after which an interrupted test fails visibly and is never silently re-planned; one planner per deployment by
advisory lock. **Before more than one planner:** transactional reservations for the product and organization budgets,
with reconciliation and concurrent-claim tests. Until then the budgets are **not** hard guarantees across instances.
**Built (2026-11-24, ADR-0037):** attempts recorded before the call and counted at their ceiling until answered;
re-plans counted (they had been dropping out of the month, found while building); a 15-minute lease after which the
test fails visibly and is never re-planned on its own; an answer after recovery not kept; one planning step per
deployment by advisory lock. Tested for a crash before the call, after a possibly billed call, and during the save,
and for a second instance. **Open:** transactional reservations before more than one planner; reconciliation of
interrupted attempts with the provider's usage.

### R-18 · A test's environment is not tied to its organization by the schema — **Low; found 2026-11-24**
The test → environment foreign key is on `EnvironmentId` alone (`src/Autestiq.Persistence/Configurations/TestCaseConfiguration.cs:38-41`),
while test → project is composite on `(OrganizationId, ProjectId)`. Creation checks the environment belongs to the
project under the tenant filter (`TestCaseService.cs:249-251`), and no reachable path that breaks it was found, so
this is a schema invariant, not a live bug. **Fix (before real customer data):** audit existing rows for an
environment of another organization or project, then a composite key `(OrganizationId, ProjectId, EnvironmentId)` to
the environment's alternate key, with a migration and PostgreSQL integration tests that a mismatched row is refused.

### R-19 · Tenant controls live in the application, with one database role — **Medium; found 2026-11-24**
Tenant isolation is the EF query filter and save guard. 49 `IgnoreQueryFilters()` calls in 15 files cross it on
purpose (background jobs, sign-in, the product-wide budget), each with a comment. The application, migrations and
operator commands use **one connection string and one role**, which owns the schema. PostgreSQL enforces foreign keys
and unique keys, and nothing else about tenancy: no RLS, and no database-level append-only audit (R-4).
**Fix, before real customer data:** separate roles for migration (owns the schema), application (data only, no DDL,
no `DELETE` on audit and usage tables) and operator; a review list of every `IgnoreQueryFilters()` with its reason.
**Evaluated separately, later:** native RLS, which needs a session variable per connection and a plan for the
background jobs that cross organizations, and database-level append-only protection (R-4).

### R-20 · A key ring that does not match the database goes unnoticed — **Fixed 2026-11-25**
Restored without its key ring, the host creates a new one, reports ready, and every stored test-account value is
silently unreadable until a run needs one and is refused (rehearsal step 10). **Fix:** a canary value written with the
key ring when the database is first used, stored in the database, and checked by `/health/ready`: a key ring that cannot
read it is not ready. Until then the runbook (§6) restores the database and the key ring together and checks by hand.
**Fixed (2026-11-25):** `key_ring_canaries` holds one value encrypted with the key ring, written by the first readiness
check on a database without one (one row, by primary key, whichever instance is first); `/health/ready` answers
`not ready: key-ring` when the key ring cannot read it, and never overwrites it. The operator's `key-ring-reset --by
<name> --yes` accepts a key ring lost for good, saying how many stored values become unusable. Tested with fakes and
with the real host: the restore without the key ring now reports not ready.

### R-7 · One database for every tenant — **Accepted, documented**

ADR-0012 chose a shared database with filtered access and listed the consequences, including the
noisy-neighbour problem and buyers who require physical separation. No action; recorded so the
register is a complete picture.
