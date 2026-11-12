# Who uses Testo

Five people. The first three use it daily; the other two visit it to make a decision or
to fix something. A feature that serves none of them does not get built.

Roles map to the responsibilities in [the testing journey](../testing-journey.md); what
each person can see and do is enforced by permissions, see [`patterns.md`](patterns.md).

---

## Mara — QA lead / test manager
**Owns quality for a release. Accountable when something ships broken.**

Plans what gets tested and how deeply, watches progress against that plan, and says
whether a release is ready. Runs a team of two to six testers.

| | |
|---|---|
| **Comes here to** | Plan a release, see where testing stands, decide go/no-go |
| **Needs** | Progress against plan, risk coverage, open defects by severity, exit criteria at a glance |
| **Worst day** | Being asked "are we ready?" an hour before a release and having to assemble the answer by hand from four tools |
| **Succeeds when** | The release readiness view answers the question before the meeting starts, and she can show *why* |

She reads more than she writes. Her screens are dashboards and reports, and every number
on them must be traceable to the runs and defects behind it.

---

## Tom — QA engineer / test analyst
**Lives in the product. Writes and runs the tests.**

Turns requirements into test conditions and test cases, runs them, and reports what broke.
Switches between a dozen tasks a day.

| | |
|---|---|
| **Comes here to** | Write test cases, run tests, watch a run, report a defect |
| **Needs** | Fast entry, keyboard paths, bulk actions, no retyping what the tool already knows |
| **Worst day** | Writing the same test case for the fifth time in slightly different words, and copying a failure into a defect report by hand |
| **Succeeds when** | A failed step becomes a complete defect report in one action, with its evidence attached |

He is the heaviest user and the harshest critic. If a flow takes six clicks where three
would do, he will notice every single day. AI suggestions save him the most time — and
cost the most trust if they are wrong, so they are always reviewable before they count.

---

## Nadia — Test automation engineer
**Keeps the automated suite worth running.**

Builds and maintains the automated tests, owns the environments, and fights flakiness.

| | |
|---|---|
| **Comes here to** | Set up a capability, investigate a failed run, fix a flaky or outdated test |
| **Needs** | Full run detail: logs, screenshots, timings, history of the same test over weeks |
| **Worst day** | A run fails and the tool cannot say whether it is the product, the test, or the environment |
| **Succeeds when** | Test health shows her the five tests worth fixing this week, not a list of two hundred |

She needs depth, not summaries. Her screens can be dense; she will trade beauty for
information every time.

---

## Jonas — Developer
**Visits only when something of his is broken.**

Does not live here, has no patience for a tool that makes him hunt. Arrives from a link in
a defect or a failed pipeline.

| | |
|---|---|
| **Comes here to** | Understand one failure well enough to fix it |
| **Needs** | Steps to reproduce, expected versus actual, environment, build, evidence — on one screen, no clicking |
| **Worst day** | A defect that says "login does not work" with no version, no steps and no screenshot |
| **Succeeds when** | He can reproduce the problem within two minutes of opening the link |

Design for him as a **first-time user every time**. A deep link must stand alone: no prior
context, no tour, no "set up your workspace first".

---

## Claudia — Engineering manager
**Looks monthly. Decides where effort goes.**

Wants to know whether quality is improving, where the risk sits, and whether the tool is
earning its licence.

| | |
|---|---|
| **Comes here to** | Check trends across releases and teams |
| **Needs** | A small number of honest figures with their trend, and an explanation in plain language |
| **Worst day** | A dashboard of twelve charts that does not say whether things are getting better |
| **Succeeds when** | She can tell her own manager in one sentence how quality is moving, and defend it |

She is often the person who signs the contract. Her screens are the ones a prospect sees
in a demo.

---

## Sven — Organization administrator
**Not a tester. Sets the product up and keeps access correct.**

Often an IT or platform person at the customer. May be Mara wearing a second hat in a
smaller company.

| | |
|---|---|
| **Comes here to** | Invite and remove people, set roles, connect single sign-on and integrations, check the audit log |
| **Needs** | Clarity about who can see what, and confidence that a departing employee loses access immediately |
| **Worst day** | Discovering a contractor still has access three months after leaving |
| **Succeeds when** | Setup is done once, correctly, and never surprises him again |

He appears because Testo is sold to businesses. His screens are rare but high-stakes: a
mistake here is a security incident, so destructive actions are explicit and everything is
written to the audit log.
