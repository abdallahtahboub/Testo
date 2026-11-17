# AUTESTIQ

**Autonomous Testing. Intelligent Quality.**

Autestiq is an AI-assisted QA platform for software companies. It covers the whole software
testing journey, from requirements to release sign-off, following the
[ISTQB test process](docs/testing-journey.md).

## The problem it is meant to solve

Testing work is spread across tools that do not know about each other: requirements in one place,
test cases in a second, automation in a third, defects in a fourth, and a report assembled by hand
for whoever has to sign the release off. Nobody can answer "what have we actually tested, and what
does that tell us about shipping this?" without a person spending a day joining it up.

Autestiq is one place for that journey, with automated execution underneath it and an AI planner
that turns a scenario written in a person's words into a plan a browser carries out.

## What is actually built today

This is a young codebase and the list is short on purpose. Nothing below is aspirational.

| Built and working | Not built |
|---|---|
| Accounts, organizations, memberships, roles, invitations, audit log — on PostgreSQL, inside a tenant boundary | Test plans, requirements, a test case library, suites, defects, reporting |
| The design system: tokens, components, light and dark themes, WCAG 2.2 AA | A store for sign-in secrets |
| Test runs, for three scripted scenarios against our own demo shop, with screenshots per step | Downloading a test (part 4) |
| Projects and environments, with proof of site ownership and production off by default | A deployment of any kind |
| Describing a test in a sentence and getting the AI's plan for it, only once the project allows AI processing | |
| Approving a plan (an explicit tick when it may change data), running it read-only unless approved otherwise, and downloading it as a Python test | |
| The public website: 29 routes in English and German | |

**The AI planner is measured on our own applications.** On 20 scenarios written after its prompt was
frozen, it was correct 95% of the time on sound applications, caught every seeded defect where it
reached a verdict, and never passed a broken application; the same held with every test id removed
(ADR-0020, [`docs/ai-benchmark.md`](docs/ai-benchmark.md)). **Applications we did not build are not
measured yet**, and neither is the read-only exploration used on a customer's site, so no claim reaches
beyond our applications. The tagline is the company's, not a description of what the software does today.

[`docs/roadmap.md`](docs/roadmap.md) §Inventory is the authoritative list of what is built, and
[`docs/security-register.md`](docs/security-register.md) is the honest list of what is not yet safe.

## Technology

- **.NET 10**, C#, ASP.NET Core
- **Blazor Web App** — static SSR for the public site and the account pages, InteractiveServer for the app
- **PostgreSQL 16** with EF Core, one database, row-level tenant isolation
- **Playwright** for test execution and for our own browser tests
- **xUnit v3** on Microsoft.Testing.Platform, **bUnit** for components
- The Anthropic API for planning, behind a provider-agnostic interface

Nothing else: no JavaScript framework, no CSS framework, no container runtime, no cloud
dependency. The one piece of JavaScript is a small ES module for the static header.

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and a
**PostgreSQL 16** to point it at. The product keeps accounts, organizations and roles in a
database, so it will not start without one. If it cannot use the database it says which server
it tried and which of three things went wrong, rather than printing a connection error: no
server answered, the server answered but the database does not exist, or the server refused the
user and password. The last one usually means another PostgreSQL already holds the port, with a
password of its own.

The quickest way to get one:

```bash
docker run --name autestiq-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

Or install PostgreSQL locally. Note that Homebrew and apt create a superuser named after your
account rather than `postgres`, so the defaults will not fit until you add one:

```bash
brew install postgresql@16 && brew services start postgresql@16
```

```bash
createuser -s postgres
```

```bash
psql -d postgres -c "ALTER USER postgres WITH PASSWORD 'postgres';"
```

(`postgresql@16` is keg-only, so add `/opt/homebrew/opt/postgresql@16/bin` to your `PATH` —
`/usr/local/...` on an Intel Mac.)

To use a different server or account instead, override the connection string:

```bash
export ConnectionStrings__Autestiq="Host=localhost;Database=autestiq_dev;Username=me;Password=secret"
```

**Run every command below from the repository root**, the folder containing `Autestiq.slnx`.
From inside a project folder, `dotnet` only sees that one project: `dotnet test` then
reports "No test projects were found".

```bash
dotnet run --project src/Autestiq.Web
```

On the first run in development it creates the database (`autestiq_dev`, from
`appsettings.Development.json`) if the server does not have it yet, applies the migrations and
seeds an organization, so there is somebody to sign in as. Outside development a missing
database is refused instead: creating one is the deployment's job. The log says who to sign in as:

```
Seeded the development database with Northwind Software.
Sign in as mara@northwind.example with the password autestiq-development-password.
```

The site is served at `http://localhost:5080`:

| Address | What it is |
|---|---|
| `/en`, `/de` | Public website (static, fast, indexable per language) |
| `/account/sign-in` | Signing in (static, posts a plain form — ADR-0016) |
| `/app` | Product app (interactive, behind the app shell) |
| `/app/runs` | Test runs: start a scripted scenario, watch it, stop it, run it again |

### Running a test run

A run needs two more things than the web application: something to run against, and the worker
that drives the browser. Start the demo shop in one terminal and leave it there:

```bash
dotnet run --project src/Autestiq.DemoShop --urls http://127.0.0.1:5090
```

Build the worker once, so the dispatcher has an executable to launch:

```bash
dotnet build src/Autestiq.Worker
```

`appsettings.Development.json` points at both. Then start a run from `/app/runs`.
`demo-shop-checkout-total` is the one that fails: the shop charges for the first item in the
basket and forgets the rest, and the step that fails keeps the screenshot that shows it.

The worker drives Chromium through an egress proxy of its own that refuses every address but the
target. It needs the same browser the tests use, so install that first (below), or point
`AUTESTIQ_BROWSER_PATH` at a Chromium you already have.

### Describing a test

The tests page of a project (`/app/p/SHOP/tests`) asks for the project's permission to send what
exploring finds to the AI provider, and only then offers to describe a test. Planning needs a model
key in the server's configuration, never in a file in the repository:

```bash
export Planner__ApiKey="..."
```

Without one, a described test is explored and then says that AI planning is not set up, and nothing is
sent. The budgets are in `appsettings.json` under `Planning`: $10 a month for the whole product and
$2 per organization. Each plan's cost is counted at the price book's prices (ADR-0029).

### The AI benchmark

The planner is measured rather than described. One command runs twelve controlled scenarios against
the demo shop, half of them with a real defect switched on, and writes
[`docs/ai-benchmark.md`](docs/ai-benchmark.md):

```bash
dotnet run --project tools/Autestiq.Benchmark
```

Without a key that measures the hand-written baseline only. To measure the planner as well:

```bash
PLANNER__APIKEY=sk-... dotnet run --project tools/Autestiq.Benchmark
```

The key is read from configuration and never stored in the repository, and it is never given to the
worker, which is treated as already compromised (ADR-0018, ADR-0019 §4). A full run with a planner
makes one model call per scenario and costs cents, not dollars; the per-run token and money budgets
stop it going further than that.

### Tests

```bash
dotnet test --max-parallel-test-modules 1
```

This runs everything: unit, component, architecture and browser tests. The browser tests
start the application themselves, so nothing needs to be running first.

The flag runs one test project at a time (tests inside a project still run in parallel), as CI
does. Without it, the sealed-run tests create Docker networks while the browser tests run;
Chromium takes each new network interface for a change of network and fails the page's
requests with `ERR_NETWORK_CHANGED`, so browser tests fail at random.

The data tests and the browser tests need **PostgreSQL**, because two of the three layers of
tenant isolation are the database's own and an in-memory provider would pass tests that
PostgreSQL fails. Each test class creates a database of its own and drops it afterwards. They
look for the server at `localhost:5432` with the usual development credentials; point them
somewhere else with:

```bash
export AUTESTIQ_TEST_DB="Host=localhost;Port=5432;Username=me;Password=secret"
```

**Once per machine**, the browser the tests drive has to be downloaded, or every browser
test fails with *"Executable doesn't exist"*:

```bash
dotnet build tests/Autestiq.E2E -t:InstallTestBrowser
```

That needs nothing beyond the .NET SDK: Playwright ships its own Node runtime inside the
build output and the target uses it. (Playwright's own documentation points at a
`playwright.ps1` script, which would mean installing PowerShell first.)

To run only part of the suite:

```bash
dotnet test --project tests/Autestiq.UI.Tests
dotnet test --project tests/Autestiq.E2E --filter-class "Autestiq.E2E.Pages.ThemeTests"
```

## How the solution is organized

```
src/
  Autestiq.Contracts        DTOs and service interfaces. No UI dependencies; the future
                            backend reuses this project.
  Autestiq.Domain           Entities and the rules that hold whatever the storage is.
  Autestiq.Persistence      EF Core on PostgreSQL, the migrations, the identity stores.
  Autestiq.Application      The use cases. Returns resource keys, never English sentences.
  Autestiq.Platform         Module and capability contracts, registries, route constants,
                            language helpers, feature-flag abstraction.
  Autestiq.UI.Shared        The design system: tokens, fonts, components, layouts.
  Autestiq.Website          The public marketing website (rendered statically).
  Autestiq.Modules.*        One module per stage of the testing journey. Six are shells.
  Autestiq.Capabilities.*   Pluggable test execution engines.
  Autestiq.Ai               The planner: prompt, parsing, the provider adapter.
  Autestiq.Worker           Drives Chromium out of process, behind its own egress proxy.
  Autestiq.DemoShop         A small web shop with switchable defects, to run tests against.
  Autestiq.BookingShop      A clinic booking site, the second application tests run against.
  Autestiq.InvoiceDesk      An invoice desk, the third, built to measure on something new.
  Autestiq.Export           Writes validated plans out as tests in a customer's language.
  Autestiq.Mocks            Deterministic in-memory data.
  Autestiq.Web              The host: composition root only.
tools/
  Autestiq.Benchmark        Measures the planner against a hand-written baseline.
tests/
  Autestiq.UI.Tests         Component tests (bUnit) and the rules that guard the structure.
  Autestiq.Application.Tests  Use cases, tenant isolation, planning.
  Autestiq.Worker.Tests     Execution, the egress policy, the crawl.
  Autestiq.Mocks.Tests      Mock data and timing.
  Autestiq.E2E              Browser tests (Playwright) and raw HTTP tests.
```

The layering runs one way: `Web` → modules → `Application` → `Domain`, with `Contracts` and
`Platform` beside them and `UI.Shared` holding no business logic at all. The worker is a separate
process on purpose: it drives a browser at a target that may be hostile, so it is treated as
already compromised and given no credentials and no route off the target (ADR-0018).

A module never references another module. The dependency rules are
[written down](docs/architecture.md#4-dependency-rules) and **enforced by a test**, so a
forbidden reference fails the build rather than quietly becoming normal.

Adding a stage of the journey, or a new kind of testing, means adding one project and one
registration line. Nothing existing changes.

## Documentation

| Document | What it answers |
|---|---|
| [`docs/testing-journey.md`](docs/testing-journey.md) | Which testing activities the platform supports and what it does in each |
| [`docs/architecture.md`](docs/architecture.md) | How the solution is put together and why |
| [`docs/sitemap.md`](docs/sitemap.md) | Every page of the website and the app |
| [`docs/domain-model.md`](docs/domain-model.md) | What is stored, and how customers are kept apart |
| [`docs/identity-and-access.md`](docs/identity-and-access.md) | Sign-in, roles, permissions, audit, GDPR |
| [`docs/ux/`](docs/ux/) | Who uses Autestiq, the flows, how the interface behaves, how it is written |
| [`docs/design-system.md`](docs/design-system.md) | Colors, type, spacing, components, accessibility rules |
| [`docs/roadmap.md`](docs/roadmap.md) | The milestones and what "done" means for each |
| [`docs/decisions/`](docs/decisions/) | Why each significant decision was made |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Branches, commits, pull requests, definition of done |

## Limitations, and what comes next

Today this is a platform with identity, a design system, a public website and real but narrow
execution. The six remaining stages of the testing journey are project shells: no screens, no
services, not even mock data behind them.

The nearest planned work, in order, is in [`docs/roadmap.md`](docs/roadmap.md): re-measuring the
planner after the four faults the first benchmark exposed, then the test-design and planning
modules, then defects and reporting. Nothing is deployed, there is no hosting, no tenancy beyond
the database boundary, and no customer-supplied target can be tested yet.

## Quality

Every change is expected to keep these true, and CI checks them on every push:

- The build produces **zero warnings** — warnings are errors.
- All tests pass.
- Every user-facing text exists in **English and German**.
- Pages meet **WCAG 2.2 AA**: keyboard support, visible focus, contrast, no status shown by color alone.
- Colors, spacing and type come from design tokens, never from raw values.

## License

Proprietary. See [LICENSE](LICENSE).
