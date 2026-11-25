# 0041 — Guarded pressing: read-only exploration presses controls while the browser lets only reads leave it

- **Status:** Accepted (the owner approved building it on 2026-11-25; Milestone 5.1 R8)
- **Date:** 2026-11-25

## Context
On a customer's site, exploring may not act unless an Owner allows it (ADR-0032). Until now that meant it followed
links and pressed nothing. Read-only exploration is the weakest part of the product: 36 of 66 runs correct against 62
of 66 when acting (ADR-0020). The owner asked for it to be improved.

Many controls change nothing on the server: tabs, accordions, menus, dialogs, "show more" and filters that run in the
page. Client-side applications render what those controls show only when they are pressed, so a crawl that presses
nothing never sees it.

The mechanism to press safely already existed. `ReadOnlyGuard` holds a run approved as read-only (ADR-0030, part 3):
- every request other than GET, HEAD and OPTIONS is aborted inside the browser, before it leaves;
- WebSockets are closed before they connect;
- service workers are blocked when the browser context is made.

## Decision
1. **An exploration that may not act is guarded**, pressing or not. `ReadOnlyGuard.Applies` now covers it, because a
   page's own scripts can send data while it loads, and reading a customer's site sends none.
2. **Such an exploration presses controls** when the dispatcher gives it `PressGuarded`
   (`ExplorationPolicy.MayPressGuarded`). It uses the same breadth-first, bounded rule as acting exploration
   (ADR-0021), with these limits:
   - Nothing is typed, and no test account is used.
   - **No form is sent**, whatever its method. A control that would send its form is not pressed: a button inside or
     naming a form whose type is not "button" or "reset", or a submit or image input. That check runs before the
     click, by locator queries. The worker runs no script in the page (ADR-0019 §2).
   - A control named for sending, paying, deleting or signing out is not pressed: the existing name rules.
   - The target is not reset between visits, because nothing was written to it.
3. **A press whose page tried to send anything is not described.** The guard counts what it blocked. When that count
   rises during a visit's presses, what the page then shows (an error, a spinner, an unchanged page) is the block, not
   the application. Telling a planner that "Save" leads there would be false. The run says how many visits were not
   described.
4. **Acting and guarded pressing are never both.** A site that may be acted on (ours, or one an Owner allowed) acts as
   before, unguarded. `MayPressGuarded` is false whenever `MayPressControls` is true.
5. **An operator can turn it off**: `Execution:GuardedPressing=false` takes customer sites back to reading only. It is on
   by default.
6. **The benchmark measures it separately.** `--read-only --guarded` addresses our own application by name (the worker
   never acts on a name) and writes its own report (`-guarded.md`). Plain `--read-only` keeps measuring what the
   earlier read-only figures measured.

## Measurement (2026-11-25, free: the coverage check, no model)
The share of the hand-written baselines' steps whose element or page the read-only crawl shows, on the unseen sets:

| Application | Read-only, links only | Read-only, guarded pressing |
|---|---|---|
| Demo shop | 34 of 40 (85%) | 34 of 40 (85%) |
| Clinic | 47 of 52 (90%) | 47 of 52 (90%) |
| Invoice desk | 29 of 31 (94%) | 29 of 31 (94%) |

**No change on our three applications, and that is the honest result.** They are server-rendered, every button on them
sends a form, and what the crawl misses is what follows a write: the basket after adding, the booking after
confirming. Guarded pressing cannot reach those, by design. On the demo shop and the invoice desk every guarded press
tried to send something and was not described (7 and 9 visits).

So the gap between read-only and acting exploration on these applications is **what actions lead to, not what pages
show**. Closing it needs acting, with permission (ADR-0032), or stated expected results (ADR-0034). Guarded pressing
helps where controls change the page in place. A test site built for that shows it: a tab whose content is rendered
only when it is chosen is found, with how to reach it. It is to be measured on a client-side third-party application
(R9). No planner run was made: there is no API credit.

## Tests
On a page with every route to a write a browser has, the server counts every request that could change data:
- `fetch` with POST, PUT, PATCH and DELETE;
- `sendBeacon`;
- a WebSocket;
- a form sent by script;
- a POST form's button and a GET form's button.

The tests check that:
- the guarded exploration pressed (at least 8 visits), and **none of it reached the server**;
- it found the tab's rendered content and its "reached by";
- it described none of the pages a blocked press showed;
- it typed nothing;
- without `PressGuarded`, it pressed nothing, as before.

Policy tests:
- guarded pressing never applies where acting does;
- it needs an exploration that asks to press and was given guarded pressing;
- a customer's site given it is never typed into.

A dispatcher test checks that the job carries the setting, that the operator's switch turns it off, and that no other
kind of run gets it.

## Consequences
- **Easier:** client-side applications are explored much more fully without any permission to act; a customer's site
  is now protected from writes during reading too, not only during pressing.
- **Harder:**
  - Explorations of customer sites take longer: one page load per press, within the same visit limit of 150.
  - A page that breaks when its analytics POST is blocked is described broken.
- **Remember (limits):**
  - **A GET that changes data is not blocked.** HTTP says GET must not change anything; some sites change data with
    it anyway (`/logout`, `/cart/add?id=`). Named controls of that kind are not pressed. A control with an innocent
    name that changes data by GET is pressed. The same limit already applies to read-only approved runs (R-14). It is
    recorded as R-21.
  - Browser storage (localStorage, sessionStorage) is not cleared between visits; cookies are. A client-side
    application keeping state there could carry one press's effect into the next visit's "reached by".
  - Service workers are blocked rather than watched: a site that needs one to work is described without it.

## Alternatives considered
- **Pressing nothing on a customer's site (the status quo).** Rejected: it leaves client-side applications largely
  unseen, and the owner asked for read-only exploration to improve.
- **Pressing and describing every outcome, blocked or not.** Rejected: a planner would be told that "Save" leads to an
  error page, and would write checks against the block.
- **Sending GET forms, which only read.** Rejected for now: a search form typed into is typing, and the owner's
  condition was that forms are never sent. It may be reconsidered with measured evidence.
- **Blocking in the egress proxy instead of the browser.** Not possible for HTTPS: the proxy sees only an encrypted
  tunnel (ADR-0030, part 3).
- **A separate permission for guarded pressing.** Rejected: nothing leaves the browser that a plain visit would not
  send, so it needs no more permission than reading. The operator's switch is the safety valve.

## Amendment (2026-11-25, R9): the browser's guard leaked; a second layer in the proxy

The guarded-pressing test's server, whose page now posts every 300 ms as Juice Shop's live updates do, received one
POST in one of two explorations: the browser's route did not see every request a page sent while it navigated (R-23).
Three changes:

1. **The egress proxy refuses writes for a guarded run or exploration** (`EgressProxy.ReadsOnly`). Every plain-http
   request other than GET, HEAD or OPTIONS is refused there, without connecting, and the refusal is not written as an
   origin, so it never reads as an unreachable site. An https request is in a tunnel the proxy cannot read: there the
   browser's guard is still the only layer (R-23, open).
2. **A guarded page is stopped before its context closes**: sent to a blank page and closed, while the routes still hold.
3. **A press is judged by what it sent beyond what pages send by themselves.** A request blocked while pages were only
   being opened (a poll) is background, and does not withhold a press: on Juice Shop that withheld 11 of 19 visits. The
   cost: a press whose only write is that same request is described as the page without it.

**Measured on a client-side application (2026-11-25, R9).** On OWASP Juice Shop, with the explorer changes of ADR-0021's
R9 amendment, guarded pressing shows 74% of what the baselines need, against 68% for following links only, and the same
as acting exploration (74%). It is the first application where it adds anything, as expected: its controls change the
page in place. The figure is a coverage ceiling, not a planner result; no planner has run on Juice Shop.

