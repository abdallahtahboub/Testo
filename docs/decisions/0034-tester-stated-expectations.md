# 0034 — What the tester says the application should show

- **Status:** Accepted (the owner approved it on 2026-11-22)
- **Date:** 2026-11-22

## Context
A test is described in one sentence (ADR-0030, part 2). The planner turns it into checks, but it can name only what
the explorer saw. When the result of a test lies past a step exploring never takes, there is nothing to check:
- **eShopOnWeb, placing an order:** the explorer reached checkout signed in, but never presses "Pay Now", because
  paying is never pressed while exploring (ADR-0032). So the thank-you page is unseen. The planner wrote a partial plan
  rather than guess its wording, and the run ends inconclusive (ADR-0020). That is correct, and it will stay so:
  exploration that pays is not on offer.
- **A customer's site:** the same holds for anything past a payment, a deletion or an email, which is where most of
  what matters is.

Today the tester's sentence can say "check that the shop thanks you", but not *what* it says. The planner then
guesses (which the owner's rules forbid presenting as fact) or gives up (inconclusive).

The owner's rules (`docs/secure-onboarding-and-privacy.md` §6) also ask that **observed, documented and assumed are
never mixed**, and that every generated test traces to its evidence. Today the test page shows the validator's notes
("Checked by Autestiq") apart from the AI's notes ("unverified"), but a check is not labelled by where its expected
value came from.

## Decision
1. **Expected results, stated by the tester.** A test may carry up to **5** expected results besides its description.
   - Each is a short text the application should show at the end, written exactly as it appears ("Thanks for your
     order"), with an optional page path where it appears (`/Basket/Success`).
   - Limits: 1 to 200 characters, one line, no control characters; a path that starts with `/`.
   - Added when the test is described, and editable until the plan is approved. Changing them discards the plan,
     because the plan was made from them. Same permission as describing a test (`ManageTestCases`).
   - The screen says, in words, not to put passwords or personal data in them: they go to the AI provider like the
     description.
2. **To the planner, labelled as the tester's.** They are sent only under the project's AI permission (ADR-0030), in
   the request's own block, "Expected results, as the tester states them (not observed by the crawl)". The block is
   left out when there are none, so every existing request, and every benchmark measured so far, is byte for byte
   what it was. **The system prompt does not change.**
3. **Each check labelled by its evidence, by software.** After validation, each check in a plan is classed
   deterministically, never by the model's own labels:
   - **Observed:** its target or text is on a page the explorer saw.
   - **Stated by the tester:** its text, or its path for a URL check, matches an expected result (exact text, ignoring
     case and surrounding spaces).
   - **Assumed:** neither. Shown as "needs verification".

   The test page shows the label beside each check, and the downloaded test carries it as a comment. A check that is
   both observed and stated is shown as observed.
4. **What a stated result can and cannot do.**
   - The validator's "not on any page the crawl saw" note is not raised for a check that matches a stated result: the
     tester has said it is expected there.
   - A plan whose only missing part was covered by stated results may be **full**, and its run can then pass or fail.
   - It changes nothing that keeps runs safe: approval, the "may change data" tick (paying is a step that changes data,
     so a plan that pays still needs it, ADR-0030), read-only runs, the never-pressed list in exploration, and the guard
     against checks for the test account's own value (ADR-0033).
5. **Measured before it is claimed.** The eShopOnWeb scenario "an order can be placed" gains the expected result
   "Thanks for your Order!" on `/Basket/Success` (taken from the hand-written baseline, which already checks both). It is
   measured with the owner's yes and a price first. The other scenarios and sets have none, so their figures stand.

## Built (2026-11-22)
- **Contracts.** `ExpectedResult(Text, Path?)`, `CheckEvidence` (observed, stated by the tester, assumed) and
  `ExpectedResults`: `Normalize` (the rules of §1), `States` (does a check match a stated result) and `EvidenceFor` (the
  label of each step). One place, used by the service, the validator, the test page and the export.
- **Kept and changed.** `TestCase.ExpectedResults` (JSON, migration `TestCaseExpectedResults`).
  `ITestCaseService.DescribeAsync` takes them; `SetExpectedResultsAsync` replaces them:
  - `ManageTestCases` and the project's AI permission;
  - refused once the plan is approved (`Tests_Error_ExpectedResultsApproved`) and while it is being planned;
  - a planned or failed test whose exploration found something goes back to planning, from what was explored, with
    no new exploration;
  - audited as `test.expected_results_set` with how many, never the texts.
- **To the planner.** `PlanRequest.Expected`, written by `PlanPrompt.User` as its own block before the application's
  content, one line per result. Without results the request is byte for byte the stored request of before (a test
  holds it). The system prompt is unchanged.
- **The validator** raises no "not on any page the crawl saw" note for a check that matches a stated result.
- **Labels.** `TestCaseDetail.Evidence`, shown as a badge beside each check on the test page, in words, and written as
  a fixed comment in the exported Python (`# Evidence: …`, no tester text in it). Without labels the export is the
  same code as before.
- **Screens.** The describe form and the test page share `ExpectedResultsEditor`: up to five rows of text and page,
  labelled, with the warning about passwords and personal data. The test page shows the results as "Stated by the
  tester" and lets them be changed until approval, saying that saving plans again.
- **Benchmark.** `BenchmarkScenario.Expected`, passed to the planner. eShopOnWeb's "an order can be placed" carries
  "Thanks for your Order!" on `/Basket/Success`. No other scenario has any, so their requests are unchanged.
- **Tests:** the rules and refusals (count, length, line breaks, control characters, paths), the byte-for-byte request,
  the one-line block outside the application's content, the unchanged system prompt, the validator note, every label
  (including both, case, other text, a negative check, and nothing taken from the model's notes), the export comment,
  the service (describe, change, replan without re-exploring, approval lock, roles, another organization, the AI
  permission, while planning, the audit), a planned run on the demo shop passing when the stated result is shown and
  failing when not, the editor's component tests, and a browser test of the test page in both themes.
- **Not yet measured:** eShopOnWeb with the stated thank-you. It needs a paid run with the owner's yes.

## Consequences
- **Easier:**
  - flows whose result lies past a step exploring never takes can reach a verdict;
  - every check says where its expected value came from, which is what a tester needs to judge it.
- **Harder:**
  - a tester can state something wrong. The test then fails on a sound application: a false alarm, visible and cheap.
    The label says the expectation was the tester's.
  - The label is only as good as the matching. A check whose text the tester wrote one way and the page shows another
    is labelled assumed, which errs on the side of caution.
- **Risks, stated:**
  - A stated result that also appears elsewhere on the page (a "Thanks" in a footer) can pass a run whose action
    failed. That is the same weakness as any text check, and the same mitigation applies: check something specific,
    on the path where it appears. The path is offered for that reason.
  - Expected results are tester's text sent to an AI provider. They pass the same permission as the description, and
    the screen asks for no secrets or personal data in them. Software cannot tell that a sentence is confidential.
- **Deferred:**
  - expected results drawn from requirements or specifications (the Requirements module, not built);
  - expectations other than visible text (an email arriving, a record in a database);
  - several outcomes per test, such as "sound" and "error" cases.

## Tests the implementation must have
- **Refusals:** more than 5 results, empty or over 200 characters, line breaks or control characters, a path not
  starting with `/`; changing results after approval; a role without permission; another organization's test.
- **Privacy:** nothing is sent without the AI permission. A request without results is identical to today's, checked
  byte for byte against a stored request.
- **Labels:** observed, stated and assumed each assigned correctly, including a check that is both, and a case
  difference; the labels never taken from the model's output.
- **Validator:** no "unseen" note for a check matching a stated result; the note still raised for one that does not.
- **Run:** a plan that checks a stated result passes on the demo shop when it is shown and fails when it is not.
- **Browser:** adding, editing and removing results on the test page, the warning text, and the labels on the plan,
  in both themes.
- **Measurement:** the benchmark's other sets unchanged without results; eShopOnWeb "an order can be placed" with
  its stated result, run by the owner.

## Alternatives considered
- **Let exploration press "Pay Now" on our own applications.** It would make our benchmark look better and teach us
  nothing about a customer's site, where paying stays off limits.
- **Put the expected wording in the description.** The planner could use it, but nothing could tell the tester's words
  from the model's guess, and the labels in §3 would have nothing to match against.
- **Let the model label its own checks.** That trusts the model to say how sure it is, which the owner's rules forbid
  (ADR-0019 §5). Software does it instead.
- **Ask the tester to approve each assumed check.** Useful later, but it adds a step to every test; the label already
  says which checks need a person's look.
