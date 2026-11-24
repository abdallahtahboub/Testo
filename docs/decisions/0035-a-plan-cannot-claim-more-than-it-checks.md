# 0035 — A plan cannot claim more coverage than it checks

- **Status:** Accepted (the owner approved step 1 on 2026-11-24); step 2 still proposed
- **Date:** 2026-11-24

## Context
A plan says how much of the tester's request it covers: `full` or `partial`. A partial plan that passes ends
**inconclusive**, never passed (ADR-0020), because a pass of half a request would be an assurance wider than the
evidence. That rule only works if the coverage is honest, and today the coverage is the model's own word.

The clean measurement on Spring PetClinic (ADR-0020) showed the gap:
- In "finding one owner opens their page", the plan listed every owner and opened George Franklin's link. Its own note
  said "the search-by-last-name filtering itself is not exercised". Yet it declared `full`, so it counted as a pass.
- The same model, on "a shared last name lists both owners", wrote the same kind of note and declared `partial`. So the
  model knows the rule; it applies it unevenly.

Also found while reading the code:
- **The parser fails open.** `PlanJson.Coverage` reads a missing or empty `coverage` as **full** (`null or "" or "full"
  …`). A plan that forgets to say how much it covers is believed to cover everything.

The owner's rules say security and evidence rules are enforced by software, not prompts (ADR-0019 §5), and that a
generated test traces to its evidence with its uncertainty stated (§6).

## Options
1. **Read the notes for phrases like "not exercised".** Rejected: it trusts the model's prose to be both honest and in
   the expected words, and it breaks the first time the wording changes.
2. **A second model judges the coverage.** Rejected for now: a model grading a model is still a model's word, at
   twice the cost per plan.
3. **The tester's stated expected results decide, where there are some (ADR-0034).** If the tester stated results, a
   plan is full only if **every** stated result is checked (by the same software matching that labels checks
   "stated by the tester"). Otherwise software sets it to partial. Deterministic, no prompt change, no re-measurement.
   It covers only tests with stated results, so it would not have caught the PetClinic case.
4. **A structured list of what is not tested.** The plan's JSON gains `notTested: ["…"]`, one line per part of the
   request it leaves untested. Software sets the plan to partial whenever the list is not empty, whatever `coverage`
   says, and shows the list to the approver. It still depends on the model admitting the gap, but it closes the
   channel the PetClinic plan used: saying so in prose while claiming full. **It changes the prompt**, which is frozen
   for measurement, so every set measured so far would need re-measuring before the change ships.
5. **Fail closed on a missing coverage.** A plan that does not say how much it covers is partial. One line in the
   parser.

## Decision (proposed)
In two steps, each with its own review:

**Step 1, now (no prompt change, no re-measurement):**
- **Option 5:** a missing or unreadable `coverage` is partial, not full.
- **Option 3:** with stated expected results, a plan is full only if each stated result is checked. Otherwise software
  marks it partial and says which stated results have no check, as a "Checked by Autestiq" note.
- **The approval screen** shows a partial plan's notes beside the refusal, so a person sees why.

**Step 2, after the owner approves a re-measurement with its price:**
- **Option 4:** `notTested` in the plan's JSON and the prompt, software forcing partial when it is not empty. The prompt
  change then goes through the same re-measurement as any prompt change: the three unseen sets (with and without test
  ids) and PetClinic, run by the owner, figures published as they come.

## Step 1, built (2026-11-24)
- **Fails closed.** `PlanJson` reads a plan as full only when it says `full` (or `complete`, `all`). A missing, empty or
  unknown coverage is partial.
- **Stated results decide.** `ExpectedResults.Unchecked` lists the stated results no check looks for: a check that
  something is there (text or visible) whose text matches, ignoring case and surrounding spaces. A result's page, when
  given, says where it appears; checking the text is what makes it tested. A check that it is *not* there does not
  count. `PlanningService` marks a plan with any such result partial and adds a "Checked by Autestiq" note naming the
  tester's words: "The tester stated “…”, and no check looks for it, so this plan covers only part of the request."
  The model's notes are not read.
- **The screen** says, on a partial plan, that the notes below (Autestiq's own checks and the AI's) say which part is
  missing. Approval was already refused for a partial plan, and a passing run of one already ends inconclusive.
- **The benchmark's recordings** now write `coverage`. They left it out, so a recorded partial plan was replayed as full,
  and since this change every recording would have been replayed as partial.
- **Tests:** an empty coverage is partial; with stated results, a full plan checking them all stays full, one checking
  none or only one of two becomes partial with a note naming each missing one (and only those), a "not there" check
  does not count, and a plan without stated results keeps its coverage. The three refusal tests fail with the rule
  switched off. A replay of the demo shop's hand-written plans from recordings is unchanged.

## Consequences
- **Easier:** a plan's claim of full coverage is backed by something software checked, where the tester stated
  results; and a forgotten coverage no longer passes for complete.
- **Harder:** more plans end inconclusive, honestly. Some testers will have to state expected results to get a verdict.
- **Not solved by step 1:** a plan with no stated results that quietly tests less than asked, as on PetClinic. Step 2
  narrows that; nothing in software can fully judge whether checks match a sentence in natural language.
- **Measurements:** step 1 changes no request sent to the model. On the benchmark it can only turn passes into
  inconclusive. The eShopOnWeb order scenario, the one set with stated results, already checks its stated result.

## Tests step 1 needs
- A plan without `coverage`, or with an unknown word, is partial.
- With stated expected results: a full plan checking all of them stays full; one missing a check becomes partial,
  with a note naming the stated result, never the model's words; a plan with no stated results is unchanged.
- A partial plan still cannot be approved, and its run that passes ends inconclusive (existing behaviour, still tested).
- The benchmark's other sets are unchanged when replayed from recorded answers.

## Alternatives considered
See Options 1 and 2 above, rejected for trusting a model's word more than now; and doing nothing, rejected because
the PetClinic pass is exactly the false assurance the partial label exists to prevent.
