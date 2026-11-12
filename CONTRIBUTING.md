# Contributing

How we work on Testo. `CLAUDE.md` holds the detailed coding conventions; this document is
about the process around them.

## Branches

Work flows in one direction: **feature branch → `develop` → `main`.**

| Branch | What it is | Who writes to it |
|---|---|---|
| `main` | Released, production-ready code. Every commit on it is a version worth shipping, and is tagged. | Only a release or a hotfix pull request |
| `develop` | The integration branch. Finished work collects here and must stay green. | Only a pull request from a working branch |
| working branches | One change in progress. Short-lived; deleted after merge. | You |

Nothing is pushed directly to `main` or `develop` — every change arrives through a pull
request, and CI must be green before it is merged. Both branches always build without
warnings with all tests passing.

Branch off `develop` and name the branch `<type>/<short-description>`, in lowercase with
hyphens:

| Type | For |
|---|---|
| `feat/` | A new capability or page, e.g. `feat/traceability-matrix` |
| `fix/` | A defect fix, e.g. `fix/language-switch-on-error-page` |
| `test/` | Tests added on their own |
| `docs/` | Documentation only |
| `refactor/` | Restructuring that does not change behavior |
| `chore/` | Build, CI, dependencies, repository setup |
| `milestone/` | A whole roadmap milestone, e.g. `milestone/2-design-system` |

Delete the branch once its pull request is merged.

### Getting work to `main`

While the product has no production deployment, `main` moves when a milestone is
accepted: open a pull request from `develop` to `main`, and tag the merge commit
(`v0.1.0`, `v0.2.0`, …).

Once there is a deployment, two more kinds of branch apply:

| Branch | From | Into | For |
|---|---|---|---|
| `release/x.y.z` | `develop` | `main` **and** back into `develop` | Stabilizing a release: version bumps and fixes only, no new features |
| `hotfix/x.y.z` | `main` | `main` **and** back into `develop` | An urgent fix to something already released |

Merging a release or hotfix back into `develop` is what keeps the two branches from
drifting apart.

## Commits

Commit after each meaningful step, not once at the end. A commit should build and should
be understandable on its own.

```
Add the traceability matrix to the analysis module

Requirements without a test case are the gap the matrix is for, so an
uncovered row is marked rather than left to the reader to spot.

The data comes from IRequirementService; the page renders loading,
empty and error states.
```

- **Subject:** imperative mood ("Add", not "Added" or "Adds"), no full stop, at most 72 characters.
- **Body:** wrapped at 72 characters, separated from the subject by a blank line. Explain *why*, not *what* — the diff already shows what.
- Reference an issue when one exists: `Part of #12`, or `Closes #12` to close it on merge.
- Never commit secrets, build output or personal data.

> We use plain imperative subjects rather than Conventional Commits (`feat:`, `fix:`).
> If you want machine-generated changelogs later, switching is cheapest while the history
> is short — raise it as a decision.

## Pull requests

One pull request per logical change; a whole milestone may be one pull request.

1. Make sure `dotnet build` is clean and `dotnet test` is green locally.
2. Push the branch and open a pull request against `develop` (against `main` only for a release or a hotfix).
3. Fill in the template: what changed, why, and how you verified it.
4. CI must pass. A red pull request does not get merged, and a test is never disabled to make it green.
5. Merge with **squash**, so `main` keeps one clear commit per change.

Because this repository is private on a free plan, GitHub cannot enforce "no direct pushes
to `main` and `develop`". It is a convention we keep ourselves. If that becomes too risky,
making the repository public or upgrading the plan enables branch protection.

## Definition of done

Before a pull request is ready for review:

- [ ] `dotnet build` — zero warnings (warnings are errors)
- [ ] `dotnet test` — everything green, including the browser tests
- [ ] New reusable components have bUnit tests; new pages have a Playwright smoke test with an accessibility scan
- [ ] Every new user-facing text exists in **English and German**
- [ ] Light and dark theme both checked
- [ ] Keyboard reachable, visible focus, no status conveyed by color alone
- [ ] Documentation updated in the same commit as the behavior it describes
- [ ] A significant decision is recorded as an ADR in `docs/decisions/`

## Decisions

Architectural decisions are recorded as short ADRs in [`docs/decisions/`](docs/decisions/),
numbered in sequence, using [`0000-template.md`](docs/decisions/0000-template.md). An ADR is
written in the same commit as the change it describes. ADRs are never deleted; they are
superseded by a later one.

## Issues

Issues carry the plan. Each roadmap milestone has an issue with its scope and its "done"
criteria as checkboxes, assigned to the matching GitHub milestone. Larger milestones have
sub-issues, one per module.

Labels: `milestone`, `module`, `decision`, `frontend`, `testing`, `infrastructure`,
`bug`, `documentation`, `enhancement`, `accessibility`.
