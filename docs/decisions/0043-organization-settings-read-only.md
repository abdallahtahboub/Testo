# 0043 — The organization's settings, read-only first

- **Status:** Accepted (the owner approved the E1 plan, which names this page "name and plan view only", on 2026-11-26)
- **Date:** 2026-11-26

## Context
E1 Administration gives screens to what the backend already has. Members, the audit log and usage had services; the
organization itself did not. Nothing in `Contracts` read an organization's name, region, two-factor rule or plan
version for the people who run it. The approved plan said: a read-only page, and an ADR if a new service is needed.

Changing these settings is a different matter:
- Renaming changes what invitations and the audit log show.
- Requiring two-factor sign-in locks out members who have not set it up, and enrolling an authenticator is not built
  yet (E2).
- The plan version is moved only by an operator (ADR-0028). A self-service change of plan is billing (E10).

## Decision
1. **A new read service, `IOrganizationService.GetCurrentAsync`**, returns an `OrganizationProfile`:
   - name, slug, data region, the two-factor rule and the creation date;
   - the plan, its version and when that version took effect;
   - the version's limits;
   - the counts of active projects and active members.
2. **Owners and admins only**, by `Permission.ManageOrganization`, which only they hold. Anyone else gets `null`
   from the service, and the page says the settings are not shown to them. The navigation item carries the same
   permission.
3. **Read-only.** The service has no method that changes anything. The page says so, and says who makes these
   changes for now: the product team, on an owner's request.
4. **The organizations table is the tenant boundary itself**, so it has no query filter. The service selects the
   organization in context by its id, and only that one. Projects and memberships are counted through the query
   filter.

## Consequences
- **Easier:** owners and admins see what their organization is and what its plan allows, next to members, the audit
  log and usage.
- **Harder:** nothing yet. When editing comes, each change is its own use case with an audit entry and its own
  decision:
  - renaming, here, in a later E1 step if the owner asks;
  - the two-factor rule, with E2's enrollment;
  - the plan, with E10's billing.
- **Remember:** the data region is shown as chosen at creation. Nothing routes by it yet (`data-architecture.md` §10),
  and the page does not claim more.

## Alternatives considered
- **Reusing `IUsageService`**, which already reads the plan. Rejected: usage is for every member, and the
  organization's details are for owners and admins. One service with two audiences would need two permission rules
  inside it.
- **Editing the name now.** Rejected for this step: the approved plan says view only, and a rename needs its own audit
  entry and test.
- **Showing the page to every member.** Rejected: least privilege. Members already see the plan's limits that concern
  them on the usage page.
