# Identity and access

> Status: **Proposal, awaiting review.** Nothing here is built yet.

Who can sign in, what they may do, and how that is proved. Testo is sold to businesses, so
this is a sales topic as much as a technical one: a security questionnaire will ask most of
these questions before a contract is signed.

Related: [`domain-model.md`](domain-model.md) (what is stored),
[`ux/flows.md`](ux/flows.md) A and B (what it feels like),
[`ux/users.md`](ux/users.md) (Sven, who administers it).

---

## 1. Decisions

| | |
|---|---|
| **Accounts** | Our own, using ASP.NET Core Identity. No per-user fee while the customer count is small, and the data stays in our database, which keeps the GDPR story simple |
| **Behind a boundary** | The application never calls Identity directly. It depends on `IAuthenticationService`, `ICurrentUser` and `IOrganizationContext`, so adding an external provider later changes the implementation, not the product |
| **Session** | A cookie: `HttpOnly`, `Secure`, `SameSite=Lax`, sliding expiry of 8 hours, absolute 30 days |
| **Passwords** | Minimum 12 characters, checked against a list of known-breached passwords. No forced rotation, no composition rules — both make passwords worse |
| **Second factor** | TOTP, optional per person, can be required by an organization |
| **Single sign-on** | Per-organization OpenID Connect, a later milestone. The email address decides the route at sign-in, which is why that screen asks for it first |

## 2. Roles

Six roles, organization-wide. Fewer would not express a QA team; more would not be
understood.

| Role | Who | Can |
|---|---|---|
| **Owner** | The person who created the organization | Everything, including billing and deleting the organization. At least one always exists |
| **Admin** | Sven | Manage people, roles, integrations, SSO, settings. Not billing |
| **QA lead** | Mara | Everything in testing: plans, approvals, release decisions. Invite testers |
| **QA engineer** | Tom, Nadia | Create and run tests, report and triage defects. Cannot approve a release |
| **Developer** | Jonas | Read everything, comment, change defects assigned to them |
| **Viewer** | A stakeholder | Read only. No export of personal data |

Rules:

- A person has **one role per organization**, and a different role in another.
- **The last owner cannot be removed or demoted.** The product refuses, with an explanation.
- A person cannot change their own role, nor raise anyone above their own.
- Permission is checked **on the server, on every request**. The interface hides what a
  person cannot use ([`ux/patterns.md`](ux/patterns.md) §9), but hiding is not enforcing.

### What each role may do

| | Owner | Admin | QA lead | QA engineer | Developer | Viewer |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Read projects, tests, runs, defects | ● | ● | ● | ● | ● | ● |
| Create and edit test cases | ● | | ● | ● | | |
| Start, stop and rerun tests | ● | | ● | ● | | |
| Report a defect | ● | ● | ● | ● | ● | |
| Triage: severity, priority, assignee | ● | | ● | ● | | |
| Change a defect assigned to them | ● | ● | ● | ● | ● | |
| Approve test cases and plans | ● | | ● | | | |
| Record a release decision | ● | | ● | | | |
| Create and archive projects | ● | ● | ● | | | |
| Invite and remove people, set roles | ● | ● | testers only | | | |
| Integrations, SSO, organization settings | ● | ● | | | | |
| Read the audit log | ● | ● | | | | |
| Billing, delete the organization | ● | | | | | |

A matching test walks this table and asserts every cell, so the document and the code
cannot drift apart.

## 3. Joining and leaving

**Invitation.** An admin enters an email address and a role. The invitation carries a
random token, stored only as a hash, valid for 7 days, usable once. The email never
reveals whether that address already has an account.

**Accepting.** With an account, it adds a membership. Without one, it asks for a name and
a password first. Either way the person lands where the invitation pointed.

**Leaving.** Removing a membership ends every session of that person in that organization
**immediately**, not at the next expiry — Sven's worst day in [`ux/users.md`](ux/users.md)
is exactly this not working. Their work stays, attributed to them. Their other
organizations are untouched.

**Closing an account.** The person's own data is removed; their authorship becomes an
anonymous marker so history and audit stay intact.

## 4. What is written down

Every action that changes access or destroys something writes an `AuditEntry`: who, what,
which object, when, from where, and the values before and after. Sign-ins and failed
sign-ins are included.

The log is **append-only** — no edit, no delete, not even by an owner. Owners and admins
can read and export it. It is kept for 2 years.

This exists because a QA platform will be asked to prove it: "who approved that release?"
and "who still had access in March?" are audit questions, and the answer has to be better
than a shrug.

## 5. Protecting the sign-in screen

| Risk | What stops it |
|---|---|
| Guessing a password | Rate limit per address and per IP; slow down, then lock with a self-service recovery. Never a silent lock |
| Finding out who has an account | Identical response and timing whether the address exists or not |
| Stealing a session | `HttpOnly` cookie, `Secure`, the session identifier rotated on sign-in and on privilege change |
| Cross-site request forgery | Antiforgery tokens on every form that changes something, already in place |
| A stolen invitation link | Single use, 7 days, hashed at rest, bound to the address it was sent to |
| An insider reading another customer's data | Tenant isolation, enforced in three layers — [`domain-model.md`](domain-model.md) §4 |

## 6. GDPR, concretely

Germany is the first market, so this is a feature, not paperwork.

- **Where the data is.** One EU region, chosen when the organization is created and shown
  in its settings. No customer data leaves it, including backups.
- **Processors.** Every third party that touches customer data is listed publicly, with
  what it processes and where. A customer's data protection officer will ask for this list.
- **Personal data we hold.** Account data (name, email, password hash) and activity
  (who did what, when). Test data belongs to the customer and may contain personal data,
  which is why synthetic test data is the default the product nudges towards.
- **Export and erasure.** A person can export their own data; an organization can export
  everything and request deletion. Deletion is real deletion, with a stated window for
  backups to age out.
- **AI.** No customer data is used to train anything. Which model processes what, and
  where it runs, is stated in the settings and in the processor list.

## 7. Open questions for review

1. **Self-service sign-up, or invitation only?** Self-service brings trials and support
   load; invitation-only suits a sales-led enterprise product. The proposal is
   **invitation-only at first**, with sign-up behind a feature flag for when trials start.
2. **Is a second factor required?** The proposal is **optional per person, enforceable per
   organization** — mandatory from day one would cost trials.
3. **How long is a session?** 8 hours sliding fits a working day. Security questionnaires
   sometimes demand less; the proposal is to make it **configurable per organization** later.
