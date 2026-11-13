# 0018 — Browsers run out of process, in a container, behind a proxy that refuses by default

- **Status:** Accepted
- **Date:** 2026-11-13
- **Threat model:** [`docs/execution-threat-model.md`](../execution-threat-model.md)

## Context

Testo's purpose is to open a browser against a web application and act on it. A browser executes
content written by somebody else, by design; the page under test is therefore hostile input, and
so is anything it links to, redirects to, embeds or fetches.

The two outcomes to prevent, in order: becoming the thing that attacks a third party from our
address, and exposing one customer's evidence or credentials to another. The threat model lists the
routes to both. This decision is what closes them, and it is taken before any code opens a browser
because that is the only time it is cheap.

## Decision

### The worker is a separate container, treated as already compromised

Browser automation runs in a worker that is:

- **a separate process in its own container**, one run per container, destroyed afterwards;
- **non-root**, with seccomp defaults and no capabilities it does not use;
- **without a database connection, a cloud role or any credential** beyond the ones its one run
  needs;
- **unable to name an organization.** It receives a job (run id, scenario, allowed origins,
  budgets, credential references) and returns a result naming the run. The application re-reads the
  organization from the stored run and writes under `TenantWriteScope` (D-2). A worker that lies
  about its run id is refused; it has nothing else to lie with.

A `BrowserContext` is a cookie jar, not a boundary. It shares the process, the file system, the
network and every ambient credential. This is the point the review made and the one this ADR makes
binding.

### Every connection leaves through an egress proxy that refuses by default

The worker's network namespace has one route out: a proxy that

- **allows only the origins the run names**, and for Milestone 3.3 that is a demo application we
  ship;
- **checks the resolved address of every connection**, not the URL. Private, loopback, link-local,
  carrier-grade NAT, cloud metadata and their IPv6 and IPv4-mapped forms are refused whatever name
  led to them. This is what defeats DNS rebinding: the address is checked at connection time, not at
  lookup time;
- **re-checks every redirect hop**;
- **covers everything the browser does** — navigation, `fetch`, WebSockets, service workers,
  prefetch — because it is the only route, not a hook on one of them;
- **permits `http` and `https` and nothing else.**

### A target must be proved before it may be one

Before an organization may run against an origin it did not get from us, it proves control of it:
a DNS record or a file at a well-known path, fetched by us, re-checked periodically. Without this
the product is a free way to send a browser at somebody else's website from our IP range.

Milestone 3.3 ships its own target and so needs none of this. That is deliberate: the slice proves
the execution engine against something safe, and the proof-of-control flow is its own piece of work.

### Evidence is stored by organization and reached only through the application

Screenshots, traces and downloaded files go to object storage under a key that begins with the
organization. Nothing is public. A person reaches a file through a short-lived signed URL the
application issues after the same permission check as any other read. Retention is 90 days as
already decided (`domain-model.md` §7); traces, which carry request bodies and therefore passwords,
are off unless a run asks for them and are kept for less time.

### Every run has budgets, and exceeding one is an outcome

Wall clock, steps, bytes downloaded, model tokens and money, per run; concurrent runs and monthly
spend, per organization. A run that exceeds a budget ends with a status that says which one
(`TimedOut`, `BudgetExceeded`), never silently and never as a pass.

### A model, when there is one, plans inside a policy it cannot change

Deferred to Milestone 3.4 but decided now, because the worker's shape depends on it:

- the model never chooses origins, credentials, budgets or the verdict;
- its output is a plan validated against a schema, from a fixed vocabulary of browser actions;
- **no model output is ever executed as code**, in the browser or anywhere else;
- an assertion that fails is a failure. The model may suggest; it may not "heal".

## Consequences

- **Milestone 3.3 is larger than a Playwright script.** The worker, the proxy, the artifact store
  and the result path all have to exist before the first real run. That is the cost of not building
  the demo first and the boundary afterwards, and it is accepted.
- **Running costs are higher.** A container per run and an object store per customer are not free.
  They are also what the security questionnaire will ask about first.
- **No third-party browser grid.** Sending a customer's staging system to an external processor is
  a data-residency question `identity-and-access.md` §6 promised not to raise. Rejected, not
  deferred.
- **The proxy is a single point of enforcement.** A bug in it is a full bypass. It is therefore
  kept small, default-deny, and tested with every address shape the threat model lists.
- **Milestone 3.3's acceptance tests include the refusals**: a private address, a redirect to
  loopback, an in-page `fetch` of an internal host, decimal and IPv4-mapped spellings, both budgets,
  a cross-organization artifact request, and a worker result naming another organization's run.
- `security-register.md` R-1, R-2 and R-3 now point here.

## Alternatives considered

- **Playwright in the web process.** Fastest to build. Rejected: shares the database connection,
  the file system and any cloud role with the browser, so a page that escapes the browser has
  everything.
- **A separate process on the same host, no container.** Better, and not enough: it still shares
  the host's network and file system, and a network allowlist enforced in the process can be
  bypassed by the process.
- **A microVM or gVisor per run.** The strongest isolation against a browser escape. Deferred, not
  rejected: it is the next step the moment a customer-controlled target exists, and the container
  boundary built now is where it slots in. Building it before there is anything to run would be
  building it blind.
- **A hosted browser grid.** Outsources the isolation and the operations. Rejected on data
  residency and on the processor list, as above.
- **An allowlist checked on the URL, in the test code.** What every first implementation does.
  Rejected because the threat model lists eight ways round it, and the proxy closes all eight at
  once.
