# R9, first findings: OWASP Juice Shop (2026-11-25)

Milestone 5.1 R9 measures the product on applications nobody here wrote. This is the free part on the first of them,
with no model and no API credit: the scenarios, the baselines, and how much of what the baselines need each kind of
exploration shows. **No planner was run.**

## The applications chosen

| Application | Why | Role in R9 | Status |
|---|---|---|---|
| **OWASP Juice Shop 17.1.1** (MIT), Docker image `bkimminich/juice-shop:v17.1.1`, source commit `39b4686` | A client-side Angular application: pages drawn in the browser, addresses in the fragment (`/#/about`), navigation by buttons as much as links. The kind of application guarded pressing (ADR-0041) is for, and unlike any measured before | **Tuning**: gaps found here may be fixed in code, re-measured on the lab sets. Never a clean measurement | Scenarios, baselines and coverage done |
| **Gitea 1.22.6** (MIT), source commit `8eefa1f` | A real, large application, server-rendered with client-side controls | **Clean**, as PetClinic was: scenarios written from source before any run, nothing tuned on it before the first planner run | Not started: Docker Hub refused the image (rate limit, 429), and fetching Gitea's own binary needs the owner's permission in this environment |

## Method

The method follows PetClinic's protocol (ADR-0020):

- **Scenarios before any run.** The scenarios were written from Juice Shop's source, read and not run, and committed
  before the application was first started (`tools/Autestiq.Benchmark/JuiceShopScenarios.cs`, commit `f9f21ae`).
- **Baselines corrected only where they fail the application as it is**, found by a run with no model. Each correction
  is reported in the file. No scenario's words changed.
- **The coverage check**, `--target juiceshop --coverage`, gives the share of the baselines' steps whose page or
  element the exploration shows. It runs on a fresh container for each mode.

## What the first runs found

### Two defects of ours, fixed in this change

1. **The egress proxy rewrote only the first request on each connection.** The browser keeps a connection to the proxy
   open, so its later requests reached the application in proxy form, with the whole address on the first line.
   - Kestrel (our applications) and Tomcat (PetClinic) read such a request by its path, which is why it never showed.
   - Node's server does not: Juice Shop answered its own scripts and styles with its HTML page, and nothing rendered.
     All ten baselines failed.
   - Those later requests were also never judged by the egress policy. They could only go to the server already
     approved, so this was no escape, but it was a check skipped.
   - **Fixed:** the proxy now asks the far end to close the connection after each answer, unless the request is a
     WebSocket upgrade. Every request then has a connection of its own, and each is rewritten and judged.
   - **Tests:** the far end receives a kept-alive request in origin form, with `Connection: close` and no proxy
     headers; an upgrade keeps its `Connection` header.
2. **A refused WebSocket counted as an unreachable site.** Chromium opens a `ws://` connection through a proxy as a
   tunnel, which the proxy reports as `https://host:port` and refuses, because the run may reach only the site's http
   origin.
   - The unreachable rule (ADR-0026 amendment) counted any refused tunnel to the site's own host. Five runs that had
     loaded the shop ended "could not be reached".
   - **Fixed:** only a refusal with the site's own scheme counts.
   - **Test:** an http site with a refused tunnel to itself is not unreachable.

### Baselines corrected (reported in the scenario file)

- **The welcome dialog hides the page from role queries.** Angular Material marks everything outside an open dialog
  as hidden to assistive technology, so a heading is not found by its role until the dialog is dismissed. The About
  Us, contact and login baselines dismiss it first, as a person would.
- **The language radio's label takes the click.** The label "Deutsch" is pressed by its text instead.
- **One scenario cannot be written and was removed: the account menu.** Its entry has the role `menuitem`, which the
  vocabulary does not have.

After these corrections, **all 9 baselines pass** on the sound application.

### Coverage: what exploring shows of what the baselines need (34 steps)

| Exploration | Shown | Notes |
|---|---|---|
| Acting (our own address) | 0 of 34 (0%) | **Ran out of its 10-minute budget** before describing anything |
| Read-only, links only | 8 of 34 (24%) | 1 page described |
| Read-only, guarded pressing | 8 of 34 (24%) | 1 page described, nothing pressed |

**The explorer barely sees a client-side application.** The gaps, in order of what they cost:

1. **Addresses in the fragment are not told apart.**
   - `/#/about`, `/#/contact` and `/#/search` are all the page `/`, so the whole shop is one page.
   - No `/#/...` step was ever shown.
2. **The page is read when it has loaded, not when it has been drawn.**
   - Angular draws the products after it has fetched them, after the load event.
   - The crawl read a page without them, and so found no control to press. That is why guarded pressing added
     nothing here.
3. **Acting exploration does not finish in its budget** on an application this size.
   - Every press is a fresh page load.
   - Its controls are many, and repeated per product.
4. **The vocabulary has no `menuitem` or `tab` role, and nothing can type into a password field** (no ARIA role).
   - Adding roles changes the planner's prompt. Every planner figure we have was measured on the current prompt, so
     the change waits until it can be re-measured (`docs/open-questions.md`).

## The explorer fixed, the same day

Gaps 1 and 2 were fixed, with gap 3's worst part: an exploration that ran out of time ended with nothing, and now stops
at three quarters of its budget and returns what it found (ADR-0021 amendment). Guarded pressing was also corrected: a
request the page sends by itself (Juice Shop's live updates post every few seconds) no longer withholds a press, and its
browser guard was found to leak while a page navigated, so the egress proxy now refuses writes too (ADR-0041 amendment,
R-23).

| Exploration | Before | After |
|---|---|---|
| Acting | 0% (out of time) | **74%**, stopping at its budget |
| Read-only, links only | 24% | **68%** |
| Read-only, guarded pressing | 24% | **74%** |

Our three applications kept every figure, in all three modes, and explored faster (ADR-0021 amendment has the table).
Still not shown on Juice Shop: the search route, reached only by buttons, and product names past the explorer's
60-element limit per page.

## What is next (not started)

1. **Done the same day** (above): gaps 1 and 2, and an exploration that stops in time.
2. **Still open:** acting and guarded exploration of Juice Shop stop at their budget; the search route; the element
   limit per page.
3. **Gitea:** once the image or binary can be fetched, write its scenarios from source, then run its baselines and
   coverage. Do not tune anything on it.
4. **Planner runs** on both, which need API credit and the owner's agreement.
