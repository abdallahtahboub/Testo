# 0022 — Filling forms while exploring: only on an application we run, and only fixed values

- **Status:** Accepted
- **Date:** 2026-11-15

## Context
The coverage measure (`dotnet run --project tools/Autestiq.Benchmark -- --coverage`) counts how many
of the elements and pages the hand-written baseline plans need are in what the exploration shows a
planner. On 2026-11-15 it showed 150 of 168 for the booking clinic and 62 of 65 for the demo shop.
Every missing element sat behind a form: a validation message, a booking confirmation, the result of
a search, the answer to a discount code. An exploration that never types and never sends a form
cannot show them, and a planner cannot name an element it was never shown.

Typing into a form and sending it is a larger action than pressing a control. On somebody else's
application it can create a record, send a message or start a payment. The owner approved it for
applications we run ourselves, with the limits in code.

Building it also showed that the search of ADR-0021 did not scale: with forms it made 253 visits to
the demo shop and took three minutes, most of them returning to pages already known.

## Decision
1. **One rule, in one place: `ExplorationPolicy`.** An exploration may press controls only on an
   application we run ourselves, and may fill and send forms only there and only when the job also asks
   for it (`WorkerJob.SubmitForms`). This also turns ADR-0021's "not on a customer's application" from
   a sentence into a check.
2. **"An application we run ourselves" is two conditions together.** The job names the target as
   trusted-local, and the target is a loopback address written as a number. A name is not enough, not
   even `localhost`. The second condition holds when the first is wrong: the run dispatcher names a
   run's own target as trusted-local today (R-11), and a customer's application on the internet still
   cannot be pressed or typed into.
3. **Refusal is said.** When pressing is asked for and refused, the application is read and the run's
   step says that pressing was refused.
4. **Only fixed values are typed** (`FormValues`). The application decides nothing about what is typed.
   It can only be the reason one fixed value is chosen over another: a field that asks for an email
   address, by its type or its name, gets the email value. No value is a credential, so a sign-in form
   is only ever seen refusing.
5. **A form is tried at most three ways:** as it is; with every field filled; and, when it has an email
   field, filled with an address that is not one. Only the fields of the pressed control's own form are
   filled.
6. **Never pressed, anywhere:** a control whose name says it orders, pays, purchases, places, deletes,
   cancels, signs out or logs out.
7. **Every visit starts afresh.** Cookies are cleared, and an application that keeps state on its server
   is put back first (`ScenarioRunner.StartAfresh`, which only an in-process caller can supply). So a
   page's "reached by" is true on a fresh application, which is where a plan runs.
8. **The search of ADR-0021 is replaced.** Every crawled page is a place to start. Links are followed
   only after a press. A page is searched further only when its content has not been seen before. One
   control of a kind is pressed, and not the same control twice running. At most three presses after
   the start, 150 visits and 40 pages.
9. **The crawl opens one address of every kind first** (`CrawlFrontier`), where a kind is an address
   with its numbers masked. A second address of a kind is opened as a check. If it is the same kind of
   page, the rest are left alone, and leaving them is not reported as stopping short.

## Consequences
- **Measured on 2026-11-15, no model involved:** the clinic 167 of 168, the demo shop's first twelve
  scenarios 64 of 65 and its held-out twenty 106 of 106. The exploration takes about 19 seconds on the
  demo shop (47 visits) and 33 on the clinic (67 visits).
- **Two gaps left, both on purpose.** The signed-in account page needs a real password. The slot shown
  as booked needs a booking and then a second walk through the site, which is four presses.
- **This is a ceiling, not a result.** It says what a planner could be shown. Whether the planner writes
  correct plans from it has not been measured since.
- **Every set measured so far is a design set.** The explorer was changed after reading the results on
  the demo shop and on the clinic, and its rules were worked out on these two applications. A third
  application, with scenarios written before any run, is what a clean measurement needs.
- **Must remember:** the policy is only as good as the loopback check and the trusted-local list. R-11
  must be closed before any customer target exists.
- **Not solved:** what a page does not show cannot be told apart. A catalogue reads the same with a
  teapot in the basket as with a cup, so only the first is searched further. A control that changes
  something without changing the page it is on is not noticed. Links the crawl cannot follow are not
  pressed on an untouched page. Browser storage other than cookies is not cleared between visits.
- **Planner prompts change.** A page's "reached by" now starts with the page the path opened, and can
  say that test values were typed.

## Alternatives considered
- **Let the model choose what to type.** Rejected: ADR-0019 §5. The page could lead it.
- **Take values from the page**, such as a placeholder. Rejected: the application would then choose
  what is typed.
- **Type a stored credential to see signed-in pages.** Rejected for now: it signs in, and a signed-in
  page can show personal data. It needs its own decision.
- **Trust the trusted-local list alone.** Rejected: R-11 shows the list can be wrong.
- **Keep the search of ADR-0021 and raise its limits.** Rejected: 253 visits for 28 pages.
- **Let visits build on each other's state.** Rejected: the snapshot then says a booking leads to "that
  time has just been booked", which is false on a fresh clinic.
