# Testo

**Testo** is an AI-assisted QA platform for software companies. It covers the whole
software testing journey, from requirements to release sign-off, following the
[ISTQB test process](docs/testing-journey.md). The first automated capability is
AI-driven end-to-end web testing with a live progress view.

> **Status: frontend phase.** The user interface is being built against mock data behind
> clean interfaces, so the real backend can be plugged in later without rewriting the UI.
> `Testo` is a working name.

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

**Run every command below from the repository root**, the folder containing `Testo.slnx`.
From inside a project folder, `dotnet` only sees that one project: `dotnet test` then
reports "No test projects were found".

```bash
dotnet run --project src/Testo.Web
```

The site is served at `http://localhost:5080`:

| Address | What it is |
|---|---|
| `/en`, `/de` | Public website (static, fast, indexable per language) |
| `/app` | Product app (interactive, behind the app shell) |

### Tests

```bash
dotnet test
```

This runs everything: unit, component, architecture and browser tests. The browser tests
start the application themselves, so nothing needs to be running first.

**Once per machine**, the browser the tests drive has to be downloaded, or every browser
test fails with *"Executable doesn't exist"*:

```bash
dotnet build tests/Testo.E2E -t:InstallTestBrowser
```

That needs nothing beyond the .NET SDK: Playwright ships its own Node runtime inside the
build output and the target uses it. (Playwright's own documentation points at a
`playwright.ps1` script, which would mean installing PowerShell first.)

To run only part of the suite:

```bash
dotnet test --project tests/Testo.UI.Tests
dotnet test --project tests/Testo.E2E --filter-class "Testo.E2E.Pages.ThemeTests"
```

## How the solution is organized

```
src/
  Testo.Contracts        DTOs and data service interfaces. No UI dependencies; the
                         future backend reuses this project.
  Testo.Platform         Module and capability contracts, registries, route constants,
                         language helpers, feature-flag abstraction.
  Testo.UI.Shared        The design system: tokens, fonts, components, layouts.
  Testo.Website          The public marketing website (rendered statically).
  Testo.Modules.*        One module per stage of the testing journey.
  Testo.Capabilities.*   Pluggable test execution engines.
  Testo.Mocks            In-memory data, deterministic. Swapped for HTTP clients later.
  Testo.Web              The host: composition root only.
tests/
  Testo.UI.Tests         Component tests (bUnit) and the rules that guard the structure.
  Testo.Mocks.Tests      Mock data and timing.
  Testo.E2E              Browser tests (Playwright) and raw HTTP tests.
```

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
| [`docs/ux/`](docs/ux/) | Who uses Testo, the flows, how the interface behaves, how it is written |
| [`docs/design-system.md`](docs/design-system.md) | Colors, type, spacing, components, accessibility rules |
| [`docs/roadmap.md`](docs/roadmap.md) | The milestones and what "done" means for each |
| [`docs/decisions/`](docs/decisions/) | Why each significant decision was made |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Branches, commits, pull requests, definition of done |

## Quality

Every change is expected to keep these true, and CI checks them on every push:

- The build produces **zero warnings** — warnings are errors.
- All tests pass.
- Every user-facing text exists in **English and German**.
- Pages meet **WCAG 2.2 AA**: keyboard support, visible focus, contrast, no status shown by color alone.
- Colors, spacing and type come from design tokens, never from raw values.

## License

Proprietary. See [LICENSE](LICENSE).
