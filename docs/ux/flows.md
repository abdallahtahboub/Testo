# Key flows

The paths people take through Testo. Each one names the person from [`users.md`](users.md),
the steps, and what happens when things are missing or go wrong — because that is where
most products fall apart.

Flows marked **(planned)** are not built yet; the milestone is noted.

---

## A. Getting an organization started *(planned, identity milestone)*

**Sven**, once, at the beginning. Everything else depends on this going well.

```
Invitation or sign-up  →  Create the organization  →  Invite the team
                                   ↓
                      Create the first project  →  Connect a source of requirements
```

1. **Arrive.** Either a sales-led invitation to an organization that already exists, or
   self-service sign-up that creates one. Both land in the same place.
2. **Create the organization.** Name, and the region its data lives in. The region cannot
   be changed later, so it is asked once, explicitly, with its consequence stated.
3. **Invite the team** by email address, each with a role. Invitations can be skipped and
   done later; a one-person organization is valid.
4. **Create the first project** — a product under test, not a customer.
5. **Connect requirements** (Jira, Azure DevOps) or skip and write them in Testo.

Rules:

- Every step after the first is **skippable**, and the product is usable without any of
  them. An empty project is a legitimate state, not a half-finished setup.
- Nothing is lost if the browser closes. Progress is saved per step.
- An invitation that is already a member of another organization joins the new one;
  a person can belong to several.

**Goes wrong:** an expired invitation explains itself and offers to request a new one. An
email address already in the organization says so instead of silently doing nothing.

---

## B. Signing in *(planned, identity milestone)*

**Everyone, daily.** The most-used screen in the product. It should be boring.

```
/sign-in  →  email  →  ┌ password (+ second factor)  ┐
                       └ redirected to the company's identity provider ┘  →  where they were going
```

1. Ask for the email address first. It decides what happens next: an organization with
   single sign-on redirects to its provider; everyone else gets a password field.
2. After signing in, land on **the page they asked for**, not a generic home page. A deep
   link from a defect email survives the detour through sign-in.
3. Someone in several organizations picks one, with the last one preselected.

Rules:

- Never say whether an email address exists — the same message either way.
- Password reset and "resend invitation" are reachable from here without an account.
- Sessions survive a browser restart; signing out ends the session everywhere it is shown.

**Goes wrong:** a wrong password says what to do next, not what went wrong internally.
After several failed attempts the account is protected and the person is told how to
recover it, not locked out silently.

---

## C. From a requirement to a set of test cases

**Tom**, every sprint. Where AI saves the most time.

```
Requirement  →  AI testability review  →  Test conditions  →  Test cases  →  Approved
                      (he accepts or edits every suggestion)
```

1. Open a requirement. Its coverage status is visible immediately: how many test
   conditions and cases exist, and whether anything is approved.
2. Ask for a **testability review**. The AI flags what is ambiguous or untestable, with the
   sentence it is reacting to quoted. Tom accepts, edits or dismisses each point.
3. **Derive test conditions**, then generate test cases from them with a chosen technique
   (boundary values, equivalence classes, decision table).
4. Each suggestion arrives as a draft, visibly marked as AI-generated, with its source
   shown. Accepting one records who accepted it and when.
5. Approved cases become part of the suite and appear in the traceability matrix.

Rules:

- **Nothing AI-generated is ever approved automatically.** The badge stays until a person accepts it.
- Bulk accept exists, but shows what is about to be accepted first.
- Generation is interruptible and its partial results are kept.

**Goes wrong:** the AI being unavailable never blocks the flow — every step can be done by
hand, and the manual path is always visible, not hidden behind a failure.

---

## D. Running an E2E test and watching it

**Tom or Nadia.** The showpiece, and the thing a prospect sees in a demo.

```
Choose suite + environment  →  Run starts  →  Live view  →  Finished  →  Results
                                                  ↓
                                        stop, or create a defect from a failed step
```

1. Start a run: pick the suite, the environment and the capability. Defaults come from
   the last run of the same suite.
2. The **live view** shows the step timeline filling in, the current step highlighted,
   pass or fail per step, a screenshot panel and a log console, with elapsed time.
3. A step that fails does not necessarily stop the run; the view makes clear whether it
   continued.
4. Stop is always available and takes effect visibly.
5. When it finishes, the same page becomes the results view — no navigation, no losing
   your place.

Rules:

- The view survives a **lost connection**: on reconnect it catches up rather than restarting
  or showing a gap.
- Screen-reader users are told about step changes politely and can mute the announcements.
- Watching is read-only and safe: nothing a viewer does can affect the run except stop.

**Goes wrong:** a run that cannot start says which part failed — environment, suite or
capability — and offers the fix, rather than reporting "failed".

---

## E. A failure becomes a fixed defect

**Tom → Jonas → Tom.** The flow that crosses roles, and the one most often broken in other tools.

```
Failed step  →  Defect created (evidence attached)  →  Triaged  →  Fixed  →  Retested  →  Closed
     Tom                    Tom                         Mara      Jonas      Tom
```

1. From a failed step, **create a defect in one action**. Steps to reproduce, expected
   versus actual, environment, build and evidence are filled in from the run. The AI
   drafts the summary; Tom edits it.
2. Duplicate detection runs before saving and offers to link instead.
3. Triage sets severity and priority and assigns an owner.
4. **Jonas opens the link cold.** Everything he needs is on that one screen.
5. When fixed, the linked test is marked for retest; passing it offers to close the defect.

Rules:

- A defect always keeps its links to the test, the run and the requirement. Those links are
  what make the traceability matrix and the release decision honest.
- The lifecycle is visible as history: who changed what, when.

**Goes wrong:** creating the defect while the integration with Jira is down still saves it
in Testo and syncs later, rather than losing the report.

---

## F. The release decision

**Mara**, at the end of every release. The flow the product is ultimately judged on.

```
Release readiness  →  Exit criteria (met / not met)  →  Open critical defects  →  Go / No-go recorded
```

1. One page states whether each exit criterion is met, with the evidence behind it.
2. Anything not met is explained: which tests, which defects, which requirements are uncovered.
3. The decision is recorded with the named person who made it and the reason, including
   when it goes against the criteria — which happens, and should be visible afterwards.
4. The test summary report is generated from the same data, not assembled separately.

Rules:

- The page never shows a single green light without its basis. Mara has to defend this
  decision to people who were not in the room.
- A figure that cannot be computed says so, rather than showing zero.

---

## Flows still to be written

Planning a release and the risk register (Mara), maintaining the automated suite (Nadia),
managing people and single sign-on (Sven), and the first five minutes of a trial. Each gets
written here before the screens are built.
