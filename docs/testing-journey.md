# The Software Testing Journey in Testo

> Working product name: **Testo** (placeholder, see `CLAUDE.md` → "Product name").
> Status: **Draft for review (Step 0)**.

Testo supports the whole testing journey, following the **ISTQB test process** (CTFL v4.0).
The ISTQB activities are not a strict waterfall: planning and monitoring/control run
across the whole lifecycle, and analysis → design → implementation → execution repeats
every sprint or release. The platform reflects that: every stage is a feature module you
can enter at any time, and everything is linked by **traceability**.

```
              ┌──────────────── Test monitoring & control (continuous) ────────────────┐
              │                                                                         │
 Planning ──► Analysis ──► Design ──► Implementation ──► Execution ──► Defects ──► Reporting & release ──► Completion
    ▲                                                        │                                │
    └─────────────── Continuous regression & maintenance ◄───┴────────────────────────────────┘
```

## Legend: how the platform supports a stage

| Mode | Meaning |
|---|---|
| **Manual** | The platform gives structure, forms, templates and storage. People do the work. |
| **AI-assisted** | AI proposes drafts or suggestions. A person reviews, edits and accepts them. Nothing AI-generated becomes "approved" without a human. |
| **Automated** | The platform does the work end to end (runs, collects, computes) and reports results. |

## Core traceability chain

Every artifact carries stable IDs, so we can always answer "what tests cover this
requirement, what failed, and is it safe to release?"

```
Requirement / User story ─► Test condition ─► Test case ─► Test run result ─► Defect
        ▲                                                                      │
        └──────────────────────────── linked back ─────────────────────────────┘
```

| Prefix | Artifact | Example |
|---|---|---|
| `PRJ` | Project | `PRJ-ORION` |
| `REL` | Release | `REL-2026.12` |
| `REQ` | Requirement / user story (usually imported) | `REQ-1042` |
| `TCO` | Test condition | `TCO-311` |
| `TC` | Test case | `TC-2208` |
| `TS` | Test suite | `TS-CHECKOUT` |
| `RUN` | Test run | `RUN-18877` |
| `DEF` | Defect | `DEF-734` |

---

## 1. Test planning

**Purpose.** Decide what to test, how, by whom, and when it's "enough": scope, test
approach, risks, resources, schedule, entry and exit criteria.

**Inputs.** Product vision, release scope / epics, requirements, contracts and regulatory
needs, previous test summary reports and lessons learned, team capacity, environments.

**Outputs / artifacts.**
- Test plan (per project or per release): scope in/out, test levels and types, approach
- Product risk register (likelihood × impact → risk level → test depth)
- Entry and exit criteria (e.g., "0 open critical defects, ≥ 95 % of high-risk tests passed")
- Schedule and milestones, roles and responsibilities, environment needs

**Roles.** QA lead / test manager (owner), product owner, engineering manager,
developers (review), stakeholders (approval).

**What Testo does.**
| Capability | Mode |
|---|---|
| Test plan editor with a structured template (IEEE 29119-3 inspired sections) | Manual |
| Risk register with a risk matrix (heat map) and risk-based test priority | Manual |
| Suggest product risks from epics and past defect history | AI-assisted |
| Draft a test plan from the release scope; propose exit criteria | AI-assisted |
| Approval workflow with versioning (who approved which version) | Manual |

**Frontend module:** `Modules.Planning`.

## 2. Test monitoring & control

**Purpose.** Continuously compare actual progress with the plan, spot deviations early,
and take corrective actions (re-prioritize, add resources, change scope).

**Inputs.** Test plan, exit criteria, live execution results, defect data, coverage data.

**Outputs / artifacts.**
- Progress metrics: planned vs. executed vs. passed, burn-up/burn-down
- Coverage metrics: requirement coverage, risk coverage
- Defect metrics: open by severity, arrival vs. closure rate, defect age
- Control actions log (decision, reason, owner, date)

**Roles.** QA lead (owner), engineering manager, product owner.

**What Testo does.**
| Capability | Mode |
|---|---|
| Live metrics and trends from execution and defect data | Automated |
| Exit-criteria tracker with traffic-light status | Automated |
| Alerts on deviations (e.g., pass rate dropping, blocked tests rising) | Automated |
| Explain a trend ("why did the pass rate drop?") and suggest actions | AI-assisted |
| Control actions log | Manual |

**Frontend module:** `Modules.Reporting` (monitoring dashboards live with reporting; they
share the same metrics). The app **Dashboard** shows the key widgets.

## 3. Test analysis

**Purpose.** Analyze the test basis (requirements, user stories, designs) to find
*what* to test: test conditions, ambiguities and gaps. Defects in requirements found
here are the cheapest ones.

**Inputs.** Requirements and user stories (imported from Jira / Azure DevOps or created
in Testo), acceptance criteria, designs, risk register.

**Outputs / artifacts.**
- Test conditions, linked to requirements and risks
- Requirement review findings (ambiguous, untestable, missing acceptance criteria)
- **Traceability matrix** (requirements × test conditions / test cases)

**Roles.** Test analyst / QA engineer (owner), product owner, business analyst, developers.

**What Testo does.**
| Capability | Mode |
|---|---|
| Requirement list with import placeholder (Jira, Azure DevOps) | Manual (import automated later) |
| Testability review of a user story: flags ambiguity, missing acceptance criteria | AI-assisted |
| Derive test conditions from a user story and its acceptance criteria | AI-assisted |
| Requirement → test traceability matrix, with gap highlighting (uncovered requirements) | Automated |

**Frontend module:** `Modules.Analysis`.

## 4. Test design

**Purpose.** Turn test conditions into concrete **test cases** (*how* to test), using
coverage techniques, and identify test data needs.

**Inputs.** Test conditions, requirements, risk priority, existing test cases.

**Outputs / artifacts.**
- Test cases: preconditions, steps, expected results, priority, technique, tags
- Test data specifications
- Coverage per technique (equivalence partitioning, boundary value analysis,
  decision tables, state transition, pairwise, use-case / scenario based)

**Roles.** Test analyst / QA engineer (owner), developers (review), product owner (review
of acceptance scenarios).

**What Testo does.**
| Capability | Mode |
|---|---|
| Test case editor (steps, expected results, attachments, versioning) | Manual |
| Generate test cases from a test condition with a chosen technique | AI-assisted |
| Propose boundary values / equivalence classes for input fields | AI-assisted |
| Synthetic test data generation (no production personal data, GDPR) | AI-assisted |
| Duplicate / overlap detection across test cases | AI-assisted |
| Review & approval status for test cases | Manual |

**Frontend module:** `Modules.Design`.

## 5. Test implementation

**Purpose.** Prepare everything needed to execute: organize test cases into suites and
runs, prepare environments and data, and create automation scripts.

**Inputs.** Approved test cases, environments, test data specifications.

**Outputs / artifacts.**
- Test suites and test run plans (manual and automated)
- Environment definitions (URLs, browsers, devices, credentials references — never secrets in the UI)
- Automation scripts / AI E2E test specifications
- Readiness check against entry criteria

**Roles.** QA engineer / test automation engineer (owner), DevOps engineer (environments).

**What Testo does.**
| Capability | Mode |
|---|---|
| Suite builder (static and query-based "smart" suites) | Manual |
| Environment registry | Manual |
| Turn a manual test case into an AI E2E web test specification | AI-assisted |
| Entry-criteria checklist before a run starts | Automated |

**Frontend module:** `Modules.Execution` (suites, environments and run setup are tightly
coupled to execution; a separate `Modules.Implementation` can be split out later
without breaking anyone because both depend only on `Contracts`).

## 6. Test execution

**Purpose.** Run tests, compare actual with expected results, record outcomes and
evidence, and report anomalies.

**Test types.** Manual and automated across: unit, integration, API, **E2E web**,
performance, security, accessibility.

**Inputs.** Suites, environments, test data, the build / version under test.

**Outputs / artifacts.**
- Test run results per test case and step: passed, failed, blocked, skipped, not run
- Evidence: screenshots, logs, videos, network traces
- Anomalies → defect candidates

**Roles.** QA engineer / tester (owner), developers (unit/integration in CI), CI pipeline.

**What Testo does.**
| Capability | Mode |
|---|---|
| Manual test runner (step-by-step, pass/fail per step, evidence upload) | Manual |
| **AI E2E web testing** with a **live progress view** (first capability plug-in) | Automated |
| Import results from CI (JUnit/TRX/xUnit) for unit, integration and API tests | Automated (later) |
| API, performance, security, accessibility capabilities (future plug-ins) | Automated (later) |
| Failure analysis: classify a failure as product bug, test issue, or environment issue | AI-assisted |
| Self-healing locator suggestions when the UI changed | AI-assisted |

**Frontend modules:** `Modules.Execution` (run lists, manual runner, run history) +
capability plug-ins (`Capabilities.E2EWeb` first).

## 7. Defect management

**Purpose.** Log, triage, fix, retest and close defects, with full links to the tests
and requirements they affect.

**Inputs.** Failed test results and evidence, exploratory findings, production incidents.

**Outputs / artifacts.**
- Defect reports: summary, steps to reproduce, expected vs. actual, severity, priority,
  environment, build, evidence
- Lifecycle history: New → Triaged → In progress → Fixed → Ready for retest → Closed
  (plus Rejected, Duplicate, Deferred, Reopened)
- Links: defect ↔ test case ↔ run ↔ requirement

**Roles.** Tester (reporter), QA lead / triage board (triage), developer (fix),
product owner (priority).

**What Testo does.**
| Capability | Mode |
|---|---|
| Defect board (list + kanban by status) and detail view with lifecycle | Manual |
| One-click "create defect from failed step" with evidence pre-filled | Automated |
| Draft the defect report text from a failed run | AI-assisted |
| Duplicate detection and severity suggestion | AI-assisted |
| Sync with Jira / Azure DevOps (UI placeholder now) | Automated (later) |

**Frontend module:** `Modules.Defects`.

## 8. Reporting & release readiness

**Purpose.** Give stakeholders a clear picture of quality and support the **go / no-go**
decision for a release.

**Inputs.** Exit criteria, execution results, coverage, open defects, risk register.

**Outputs / artifacts.**
- Quality dashboards (per project, release, team)
- Test summary report (per release)
- Release readiness score and go / no-go record with sign-offs

**Roles.** QA lead (owner), engineering manager, product owner, release manager.

**What Testo does.**
| Capability | Mode |
|---|---|
| Dashboards and charts from live data | Automated |
| Release readiness widget: exit criteria status, open critical defects, risk coverage | Automated |
| Draft the test summary report in plain language | AI-assisted |
| Go / no-go sign-off with named approvers | Manual |
| Export (PDF/Markdown) | Automated (later) |

**Frontend module:** `Modules.Reporting`.

## 9. Test completion

**Purpose.** Close a release or project cleanly: archive testware, hand over, and learn.

**Inputs.** Final results, open defects, test summary report, team feedback.

**Outputs / artifacts.**
- Test completion (closure) report
- Lessons learned / retrospective notes
- Archived, read-only snapshot of the release's testware
- Decisions on remaining open defects (deferred, accepted, moved to backlog)

**Roles.** QA lead (owner), whole team for lessons learned.

**What Testo does.**
| Capability | Mode |
|---|---|
| Closure checklist and report template | Manual |
| Draft lessons learned from metrics and defect patterns | AI-assisted |
| Snapshot and archive a release (read-only) | Automated |

**Frontend module:** `Modules.Reporting` (release "Close" flow). Can become its own
module if it grows.

## 10. Continuous regression & maintenance

**Purpose.** Keep the test suite valuable as the product changes: run regression
continuously, fix or retire flaky and obsolete tests, and keep traceability current.

**Inputs.** Code and requirement changes, run history, flakiness data, defect trends.

**Outputs / artifacts.**
- Regression suites (scheduled or triggered by CI)
- Flaky test report, test health score
- Impact analysis: which tests to run for a change
- Maintenance backlog (outdated, duplicate, never-failing tests)

**Roles.** QA engineer / automation engineer (owner), developers, DevOps.

**What Testo does.**
| Capability | Mode |
|---|---|
| Scheduled and CI-triggered regression runs | Automated (later) |
| Flakiness detection from run history | Automated |
| Change impact analysis: suggest the minimal regression set | AI-assisted |
| Suggest updates to tests affected by a changed requirement | AI-assisted |

**Frontend module:** `Modules.Execution` (regression schedules, test health) for now;
candidate for `Modules.Maintenance` later.

---

## Stage → module map (summary)

| # | Stage | Frontend module | In first frontend phase |
|---|---|---|---|
| 1 | Test planning | `Modules.Planning` | Yes |
| 2 | Monitoring & control | `Modules.Reporting` + Dashboard widgets | Yes |
| 3 | Test analysis | `Modules.Analysis` | Yes (incl. traceability matrix) |
| 4 | Test design | `Modules.Design` | Yes |
| 5 | Test implementation | `Modules.Execution` | Yes (suites, environments) |
| 6 | Test execution | `Modules.Execution` + `Capabilities.*` | Yes (+ live E2E run view) |
| 7 | Defect management | `Modules.Defects` | Yes |
| 8 | Reporting & release readiness | `Modules.Reporting` | Yes |
| 9 | Test completion | `Modules.Reporting` | Light (closure report page) |
| 10 | Regression & maintenance | `Modules.Execution` | Light (test health widget) |

## AI principles (apply to every AI-assisted feature)

1. **Human in the loop.** AI output is always a *suggestion* with a visible "AI-generated"
   badge until a person accepts it.
2. **Explainable.** Show why: which requirement, which run, which data led to the suggestion.
3. **Traceable.** Store who accepted what and when.
4. **Privacy first (GDPR).** No production personal data in prompts by default; synthetic
   test data; the data processing location is shown in settings.
5. **Graceful without AI.** Every stage works fully in manual mode.
