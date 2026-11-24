# Fault injection (proposed)

> **Proposed, not built**, except the 21 business-logic switches in our three applications. Every fault here exists
> only in the lab, against applications the lab started.

## 1. What a fault must declare

| Field | Why |
|---|---|
| **Id and version** | So a result names exactly which fault it faced |
| **Application and version** | A fault is meaningless without the code it applies to |
| **Mechanism** | Switch in the app, proxy rule, or DOM transform |
| **Trigger** | When it applies (route, step, count, time) |
| **Kind** | *Product defect* (a test should fail) or *test-environment fault* (a test should recover, retry, or say inconclusive) |
| **Ground truth** | The correct verdict for each scenario it is paired with, decided before any run |
| **Reset** | How the application returns to its clean state |
| **Evidence required** | What a run must show for its verdict to be accepted |
| **Safety** | Why it cannot reach anything outside the lab |

The **kind** is the distinction the brief insists on, and it is the hardest part: a product defect must make a good
test fail; an environment fault (a slow response, an expired session the test should handle) must *not* be counted as a
product defect, and a test that fails on it has a robustness problem, not a finding. Mixing the two is how a learning
loop learns to call flakiness a bug, or a bug flakiness.

## 2. Mechanisms

| Mechanism | How | Strength | Limit |
|---|---|---|---|
| **App switch** (exists) | A flag inside our own apps (`Defects.cs`) | Exact, cheap, real business logic | Only apps we own |
| **Fault proxy** (proposed) | A lab-only rule set on a proxy in front of the app: status codes, delays, dropped or mutated JSON fields, wrong pagination | Works on any app, no code change | Can't change server-side logic beyond what responses show |
| **DOM transform** (proposed) | Rewriting served HTML on the proxy: renamed ids or classes, duplicated elements, an overlay, a moved control | Exercises locator robustness on any app | Can produce pages no real app would; must be reviewed for realism |
| **Fixture state** (proposed) | Seed data per scenario: an expired session, a locked test account, an empty inventory | Deterministic edge states | Needs app support or a database seed |

Playwright's own request interception is **not** used for faults: it lives inside the browser the worker controls, so
the agent could in principle observe it, and it would not exist in a customer run. The proxy sits outside, as the
egress proxy does (ADR-0026).

## 3. Catalogue by category

| Category | Examples | Kind | First mechanism |
|---|---|---|---|
| **DOM and UI** | dynamic ids, changed classes, duplicate labels, hidden control, modal overlay, moved element, shadow DOM, iframe, accessibility regression (lost label) | Mostly environment; an accessibility regression is a product defect | DOM transform |
| **Auth and session** | expired session mid-flow, invalid test credentials, cookie flags, login redirect loop, locked account | Mixed: an unexpected logout is a product defect; an invalid stored credential is configuration | Fixture state, proxy |
| **API and network** | 500/503, timeout, slow response, missing JSON field, wrong page size, 429 | 5xx and wrong data are product defects; a transient timeout is environment | Fault proxy |
| **Timing and concurrency** | delayed render, loading overlay, detached element, out-of-order responses | Environment, unless the app shows a wrong final state | Fault proxy (delays) |
| **Business logic** | wrong discount, wrong tax, duplicate order, stock not updated, missing persistence, wrong permission, invalid transition | Product defect | App switch (exists for 21) |

## 4. Rules

- **A passing test never proves a defect was fixed.** It proves the test passed. "Fixed" needs the fault removed and the
  same test failing with it and passing without it.
- **No fault is aimed at anything the lab did not start.** The proxy accepts rules only for lab origins; the job flag
  that enables it is never set by the product's dispatcher.
- **Each new fault is tried with the hand-written baseline first,** with and without the fault, before any model sees
  it. If the baseline cannot tell, neither result means anything.
- **Faults are reviewed for realism** before they enter a training split: a fault no real application would exhibit
  teaches a skill nobody needs.

## 5. Open questions

- How to make the "kind" judgement reviewable when it is genuinely ambiguous (a slow page that eventually shows the
  right result). Proposed: ambiguous faults go to evaluation only, never to training.
- Whether DOM transforms on third-party apps produce realistic variety, or mostly noise. Experiment before scaling.
