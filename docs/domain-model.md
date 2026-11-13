# Domain model

> Status: **Proposal, awaiting review.** Nothing here is built yet.

What Testo stores, and how the pieces relate. This is the expensive thing to get wrong:
the user interface can be rewritten in an afternoon, a schema that leaks one customer's
data into another's cannot be unwritten.

Related: [`testing-journey.md`](testing-journey.md) (what each artifact is for),
[`identity-and-access.md`](identity-and-access.md) (who may see it),
[`architecture.md`](architecture.md) (where the code lives).

---

## 1. The shape of it

```
Organization ─┬─ Membership ── User            (who)
              ├─ Invitation
              ├─ AuditEntry
              └─ Project ─┬─ Release
                          ├─ Requirement ── TestCondition
                          ├─ TestCase ── TestCaseStep
                          ├─ TestSuite
                          ├─ TestEnvironment
                          ├─ TestPlan ─┬─ Risk
                          │            └─ ExitCriterion
                          ├─ TestRun ── TestCaseResult ── StepResult ── Evidence
                          └─ Defect ── DefectEvent
```

Everything below `Organization` belongs to exactly one organization. That is the isolation
boundary, and it is enforced in one place rather than remembered in every query — see §4.

**`User` is deliberately outside the boundary.** A person is one account with one password,
who may belong to several organizations: a consultant working for two customers, or
someone who changes employer. `Membership` is the join, and it carries the role.

## 2. Identity on every row

| Field | What it is |
|---|---|
| `Id` | A UUID version 7 — random, but ordered by creation time, so it indexes well as a primary key and reveals nothing in a URL |
| `OrganizationId` | On everything inside the boundary. Never null, never changed |
| `Key` | A human-readable identifier, unique per project: `REQ-1042`, `TC-2208`, `DEF-734`. What people say out loud and paste into chat |
| `CreatedAt`, `CreatedBy` | Set once |
| `UpdatedAt`, `UpdatedBy` | Set on every change |
| `Version` | PostgreSQL's row version, for optimistic concurrency: two people editing one test case do not silently overwrite each other |

Sequential integers are not used as primary keys: they leak how many customers and
records exist, and they collide when data is imported or merged.

**Deleting.** Anything another record points at is **archived, not deleted** — a test case
referenced by a run that happened is a fact about the past. Archived records disappear
from lists and stay reachable from history. Genuinely deleting an organization's data, on
request, is a separate and deliberate operation (GDPR Article 17).

## 3. The entities

### Who

| Entity | Holds | Notes |
|---|---|---|
| `Organization` | Name, slug, data region, plan, status | The tenant. The data region is chosen at creation and never changes |
| `User` | Email, password hash, display name, locale, theme, MFA settings | Global. Email is the identifier and is unique across the product |
| `Membership` | User, Organization, Role, status, joined date | A person's place in one organization |
| `Invitation` | Email, Organization, Role, token hash, expiry, who invited | Consumed on acceptance; expires |
| `AuditEntry` | Organization, actor, action, subject, timestamp, before/after | Append-only. Never edited, never deleted |

### What is being tested

| Entity | Holds | Notes |
|---|---|---|
| `Project` | Name, key prefix, description, settings | A product under test, not a customer |
| `Release` | Name, version, target date, status | What a test plan and a go/no-go decision attach to |
| `TestEnvironment` | Name, base URL, browser/device, credential reference | **Never a password.** A reference to a secret held elsewhere |

### Analysis and design

| Entity | Holds | Notes |
|---|---|---|
| `Requirement` | Key, title, description, acceptance criteria, source, external id, status | Imported from Jira or Azure DevOps, or written here. `ExternalId` is how re-import matches |
| `TestCondition` | Requirement, description, risk level, source | What to test. May be AI-derived — see §5 |
| `TestCase` | Key, title, preconditions, technique, priority, status, owner, tags | The current version. Approval is a status, not a separate entity |
| `TestCaseStep` | TestCase, order, action, expected result, data | Ordered children |
| `TestCaseVersion` | TestCase, version number, snapshot, who, when | History, so a run can say which version it executed |
| `TestSuite` | Name, description, kind (fixed list or saved query) | A query-based suite stays current as cases are added |

### Execution

| Entity | Holds | Notes |
|---|---|---|
| `TestRun` | Key, suite, environment, release, capability, trigger, status, started, finished, who | One execution of a suite |
| `TestCaseResult` | Run, test case, version executed, status, duration, assigned defect | Status: passed, failed, blocked, skipped, not run |
| `StepResult` | Case result, step, status, duration, actual result, message | What the live view streams |
| `Evidence` | Step or case result, kind, storage key, captured at | Screenshots, logs, video, traces. The file lives in object storage; this row points at it |

### Defects

| Entity | Holds | Notes |
|---|---|---|
| `Defect` | Key, summary, description, steps, expected, actual, severity, priority, status, assignee, environment, build, external id | The report |
| `DefectEvent` | Defect, type, from, to, actor, comment, timestamp | The lifecycle as history, not a mutable status field |
| `DefectLink` | Defect ↔ TestCase / TestRun / Requirement | Why the traceability matrix can be trusted |

### Planning and reporting

| Entity | Holds | Notes |
|---|---|---|
| `TestPlan` | Release, scope, approach, schedule, status, approvals | One per release |
| `Risk` | Plan, description, likelihood, impact, mitigation, owner | Drives how deeply something is tested |
| `ExitCriterion` | Plan, description, target, current value, met | What the release decision is measured against |
| `ReleaseDecision` | Release, decision, reason, decided by, when, criteria snapshot | Recorded even when it goes against the criteria, which happens |

## 4. How isolation is enforced

Three layers, because one is never enough:

1. **The query filter.** Every entity inside the boundary implements `IBelongsToOrganization`,
   and a global EF Core query filter adds `WHERE organization_id = @current` to every query
   automatically. Forgetting a `WHERE` clause is the normal way tenant data leaks; here it
   cannot happen, because the clause is not written by hand.
2. **Saving.** `OrganizationId` is set from the current context when a row is created and
   rejected if changed afterwards. An entity cannot be moved between organizations.
3. **The database.** `organization_id` is the first column of every index that matters,
   and foreign keys include it, so a row cannot reference a parent in another organization
   even if the application is wrong.

A test fixture runs the same queries as two organizations and fails if either sees a row
of the other. It runs against every entity, found by reflection, so a new entity is covered
the day it is added.

## 5. Where AI output lives

AI output is data with a provenance, not a special kind of record. Anything that can be
generated carries:

| Field | Meaning |
|---|---|
| `Origin` | `Human`, `AiSuggested`, `AiAccepted` |
| `SuggestedBy` | Which model and version produced it |
| `BasedOn` | What it was derived from — the requirement, the run, the history |
| `AcceptedBy`, `AcceptedAt` | Who took responsibility, and when |

A suggestion is a real row with `Origin = AiSuggested`. It appears with its badge, it can
be edited, and accepting it flips the origin and records the person. Nothing is approved
without a name against it, which is what [`ux/patterns.md`](ux/patterns.md) §7 promises.

## 6. What this model deliberately leaves out

- **Per-project permissions.** Roles are organization-wide to start with. Projects a person
  may not see is a later addition; the `Membership` row is where it would hang.
- **Custom fields.** Enterprise QA tools accumulate them. Not until a customer asks.
- **Test data management** as its own area — `TestCaseStep.Data` carries it for now.
- **Billing.** `Organization.Plan` is a placeholder; invoicing is a separate concern.

## 7. Open questions for review

1. **Project keys.** `REQ-1042` unique per project, or per organization? Per project reads
   better and matches Jira; per organization makes links unambiguous when a person works
   across projects. The proposal is **per project**.
2. **Requirement import.** When a requirement changes in Jira, does Testo overwrite its copy,
   or flag the difference for a person? The proposal is **flag it**, since a silently
   changed requirement invalidates tests nobody re-checked.
3. **Evidence retention.** Screenshots and videos are the bulk of the storage cost. How long
   are they kept? The proposal is **90 days by default, configurable per organization**,
   with the result rows kept forever.
