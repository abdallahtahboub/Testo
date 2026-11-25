# Enterprise requirements: what an enterprise customer expects, and our plan for it

> **Status: planned, not built (2026-11-25).** The owner asked on 2026-11-25 for every enterprise feature named in
> engineering's assessment to be added to the plan, with engineering's suggestions as high priority. **Nothing in this
> document is built unless the "Today" column says so.** The milestones are in [`../roadmap.md`](../roadmap.md)
> §Enterprise programme. Each one is planned in detail, approved by the owner, built, and reviewed, as every milestone is.

## Priorities

**High priority** (engineering's suggestions, which the owner adopted):
1. **E1 — Administration for what the backend already has.** It is the cheapest, and a pilot customer needs it on day
   one.
2. **Milestone 6, in a new order:** Analysis (requirements, traceability) and Design (case library) come first. They are
   also stages B–D of the owner's goal ([`goal-application-understanding.md`](goal-application-understanding.md)).
3. **E2 — Enterprise identity:** single sign-on (SSO) and enforced two-factor sign-in.
4. **E3 — Integrations:** an API, webhooks, issue trackers, source hosting and CI.

**Then, in order:** E4 governance, E5 access refinements, E6 work at scale, E7 reporting (Milestone 6.6), E8 trust and
certification, E9 platform and operations, E10 commercial.

## The requirements, area by area

The "Today" column says what exists on 2026-11-25. "Built" there means in the code and tested.

### E1 — Administration (high)

| Requirement | Today | Notes |
|---|---|---|
| Members: list, invite, resend, revoke an invitation, remove a member | Built in the backend (invitations, memberships, audit); **no screen** | The first owner is created by the operator's `create-organization` |
| Roles: change a member's role, with the "last owner" rule | Built in the backend; **no screen** | Five roles per organization today |
| Audit log viewer: filter by person, action and date; open the subject | Stored, append-only; **no screen** | Export is in E4 |
| Usage: minutes, concurrent runs and AI spend this month against the plan's limits | Ledger, quotas and cost rows built; **no screen** | Prices are estimates until hosting is chosen |
| Organization settings: name, default project rules, evidence retention default | Retention is per project; **no organization screen** | |
| Plan shown to the customer (read-only) | Plan catalog built, operator-managed; **no screen** | Changing plans needs E10 |

### E2 — Identity (high)

| Requirement | Today | Notes |
|---|---|---|
| SSO with OpenID Connect (Entra ID, Okta, Google Workspace) | Not built; listed under "Later" before this plan | Per organization, behind ADR-0013's interfaces |
| SSO with SAML 2.0 | Not built | Many enterprises still require it |
| Enforced two-factor sign-in per organization | Signing in with a code works; **setting up an authenticator does not** | Enrollment first, then enforcement |
| User provisioning and removal (SCIM 2.0) | Not built | Needs SSO first |
| Domain verification (claim `example.com`, route its users to SSO) | Not built | Reuses the idea of site-ownership proofs |
| Session policy: timeout, sign out everywhere, IP allow list | Fixed defaults | |
| Online breached-password check | An embedded list only | |

### E3 — Integrations (high)

| Requirement | Today | Notes |
|---|---|---|
| Public REST API with API keys per organization, scoped and revocable | Not built (`Autestiq.Api` is planned, ADR-0014) | Over the same use cases as the UI |
| Webhooks (run finished, test approved, defect created), signed | Not built | |
| CI: start a run from GitHub Actions, GitLab CI, Azure DevOps; fail the build on a failed verdict | Not built | Needs the API |
| Issue trackers: Jira, Azure Boards, GitHub Issues: create a defect from a failed step, link back | Not built | Needs the Defects module (6.5) |
| Source hosting (GitHub, GitLab): optional, least-privilege, explicitly authorized | Not built | The binding rule: never required, always optional |
| Chat: Slack, Microsoft Teams notifications | Email only (ADR-0040) | |

### E4 — Governance

| Requirement | Today | Notes |
|---|---|---|
| Audit log export (CSV, JSON) and streaming to a customer's SIEM | Stored; no export | |
| Retention settings for every kind of customer data | Screenshots only (ADR-0031); snapshots and planning payloads open (G-5) | `docs/data-architecture.md` has the inventory |
| Data deletion on request: a project, an organization, with a report | Not built | |
| Data residency: EU or another region per organization | Undecided; the owner deferred it to the hosting decision | Multiplies each store |
| AI data controls: what may go to a model, per project; a minimized mode | Permission per project built; **minimized mode (G-2) not built** | Binding rule |
| Legal records: DPA, sub-processor list, terms | Placeholders | Needs counsel |

### E5 — Access refinements

| Requirement | Today | Notes |
|---|---|---|
| Per-project roles (someone is a QA lead on one project, a viewer on another) | Roles are per organization | |
| Teams or groups, assigned to projects | Not built | |
| Custom roles from a fixed list of permissions | Fixed roles | |
| Approval workflows (two people must approve a plan that changes data) | One approver, with an explicit tick (ADR-0030) | |
| Production access with a separate, time-limited permission | Production off by default; no time limit | |

### E6 — Work at scale

| Requirement | Today | Notes |
|---|---|---|
| Search across projects, tests and runs | Not built | |
| Filters, sorting and saved views on every list | Basic lists | `TDataTable` supports sorting |
| Bulk actions (approve, run, archive several at once) | Not built | |
| Export lists and reports to CSV and PDF | Not built | |
| Keyboard shortcuts for frequent actions | Not built | WCAG rules apply |
| Many organizations for one person (agencies), switching between them | One organization per session today | |

### E7 — Reporting (Milestone 6.6)

| Requirement | Today | Notes |
|---|---|---|
| Dashboards: pass rate, flakiness, coverage of requirements, trends | Content-free analytics views (R6); no screens | |
| Release readiness and a go/no-go view | Not built | |
| Test summary and completion reports (ISTQB) | Not built | |
| Scheduled reports by email | Not built | |

### E8 — Trust and certification

| Requirement | Today | Notes |
|---|---|---|
| A private agent on the customer's own machines (outbound, local secrets and evidence) | Designed in the binding rules, not built | Never claimed until built |
| Security documentation pack: architecture, data flows, sub-processors, pen-test summary | The registers exist internally | |
| An independent penetration test | Not done | Before larger customers |
| SOC 2 Type II, then ISO 27001 | Not started | Months of operating evidence; a later business decision |
| Accessibility statement (WCAG 2.2 AA) | Built to it and tested; no statement | |

### E9 — Platform and operations

| Requirement | Today | Notes |
|---|---|---|
| Hosting, TLS, alerting, log collection, status page | Not deployed; the runbook is rehearsed locally | The owner's hosting decision |
| Backups with tested restores; RPO ≤ 15 min, RTO ≤ 4 h | Designed (ADR-0038); rehearsed locally | |
| An uptime commitment (SLA) and support hours | None | A business decision |
| Scale-out: several web hosts with sticky sessions for Blazor's connections; several workers | One host; one planning instance by design (ADR-0037) | |
| A sandbox or trial organization | Not built | |

### E10 — Commercial

| Requirement | Today | Notes |
|---|---|---|
| Self-service plans, upgrades and invoices | Plan catalog and cost per run built; no billing | Pricing is a placeholder |
| Contracts and invoicing for enterprise (annual, by purchase order) | None | |
| Usage alerts before a limit is reached | Limits enforced; no alerts | |

## Decisions the owner will need to make

These are recorded here so that nothing waits on a conversation:
- **Hosting and data residency.** These block E9, part of E4, and trustworthy prices in E1.
- **Which identity providers come first** (Entra ID, Okta, Google). This shapes E2.
- **Which integrations come first** (GitHub or Azure DevOps; Jira or Azure Boards). This shapes E3.
- **Pricing and billing provider.** This shapes E10.
- **When to start certification.** This shapes E8.
