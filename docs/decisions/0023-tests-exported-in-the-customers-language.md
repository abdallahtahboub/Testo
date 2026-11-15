# 0023 — Tests exported in the customer's language; the plan stays the source of truth

- **Status:** Proposed
- **Date:** 2026-11-15

## Context
The owner's requirement: Autestiq is built in .NET, but the tests it produces should be in the language
and framework the customer chooses, for example Python. Customers run tests in their own CI and keep
them in their own repositories, in the language their team reads.

Two existing rules constrain how this can work. ADR-0019: a model's output is never executed as code;
the model writes a plan in a closed vocabulary, a validator checks it, and our worker carries it out.
ADR-0020: every claim about a generated test rests on evidence from a run.

## Proposal
1. **The validated plan stays the single source of truth.** Our own runs keep executing the plan, in
   .NET, inside the worker's isolation (ADR-0018). Nothing changes there.
2. **A plan is exported as test code by a deterministic template per language, never by the model.**
   Each of the ten actions has one fixed translation per language, so an exported test does exactly
   what the plan does, and the export can be reviewed and tested like any other code generator.
3. **Languages are limited to ones with first-class browser automation.** Playwright officially
   supports Python, TypeScript/JavaScript, Java and C#. C++ has no mainstream browser-testing
   framework, so it is not offered for web tests; it may come back for other kinds of testing.
4. **Every exporter is verified against our own applications.** An exported test is run against the
   demo shop, the clinic and the invoice desk, with each defect off and on, and must give the same
   verdict as the plan. A language is offered only after its exporter passes that.
5. **Secrets stay names.** An exported test reads a secret from the customer's own environment by name;
   no value is ever written into exported code.

## Consequences
- **Easier:** customers own their tests, in their language, with no runtime dependency on us.
- **Harder:** one exporter and one verification suite per language. Python first is the suggestion:
  the most common choice for test automation, and the one the owner named.
- **Must remember:** an exported test runs outside our isolation, on the customer's machines, against
  whatever they point it at. That is their decision; the export carries the same plan, not more.

## Open questions for the owner
- Which language first: Python is suggested.
- Is the exported code a feature of every plan, or only of plans a person has approved?

## A finding from the independent application (2026-11-15)
Microsoft's eShopOnWeb, the independent application chosen for a clean measurement, has no `data-testid`
attributes at all. The explorer presses only controls with a test id (ADR-0021), so on that application
exploration would press nothing, and plans would have to name every element by role and visible text.
Many customer applications will look like this. Whether the product requires test ids, or explores and
plans by role and name as well, is a product decision this export work depends on too: an exported test
names elements the same way the plan does.
