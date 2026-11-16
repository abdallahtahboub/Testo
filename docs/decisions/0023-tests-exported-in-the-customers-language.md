# 0023 — Tests exported in the customer's language; the plan stays the source of truth

- **Status:** Accepted by the owner on 2026-11-16 ("let's carry on"); the Python exporter is built and verified
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
5. **The language is the client's choice, per project.** A client may have projects in different
   languages, with different preferences, so the language is a setting of each project, not of the
   product or the account. Exporters sit behind one interface, so adding a language adds an exporter and
   changes nothing else. Which exporters are built, and in what order, follows what clients ask for.
6. **Secrets stay names.** An exported test reads a secret from the customer's own environment by name;
   no value is ever written into exported code.

## Consequences
- **Easier:** customers own their tests, in their language, with no runtime dependency on us.
- **Harder:** one exporter and one verification suite per language. The first one built is the
  suggestion of Python, the most common choice for test automation; it is not a default, and a project
  chooses its own.
- **Must remember:** an exported test runs outside our isolation, on the customer's machines, against
  whatever they point it at. That is their decision; the export carries the same plan, not more.

## Open questions for the owner
- Which exporter to build first: Python is suggested. Every project chooses its own language once more
  than one exists.
- Is the exported code a feature of every plan, or only of plans a person has approved?

## A finding from the independent application (2026-11-15)
Microsoft's eShopOnWeb, the independent application chosen for a clean measurement, has no `data-testid`
attributes at all. The explorer presses only controls with a test id (ADR-0021), so on that application
exploration would press nothing, and plans would have to name every element by role and visible text.
Many customer applications will look like this. Whether the product requires test ids, or explores and
plans by role and name as well, is a product decision this export work depends on too: an exported test
names elements the same way the plan does.

## Built: the Python exporter (2026-11-16)
- **Shape.** `ITestExporter` and `ExportLanguage` in `Autestiq.Contracts.Export`; the exporters in
  `Autestiq.Export`, which references `Contracts` only. `PythonExporter` writes one pytest file per plan,
  using Playwright for Python: one fixed statement per action, the step's description as a comment above it.
- **Locating, as our runs do.** Every file carries a small `target()` helper with the worker's rules
  (ADR-0024): a test id, a role with its exact accessible name, or an exact text, narrowed to the innermost
  container that holds both a "within" text and the element; and a looser name only when nothing has the
  exact one within two seconds. So the file needs nothing of ours to run, and finds what our run found.
- **Nothing from a plan becomes code.** Every value that came from a plan is written as a Python string
  literal in which everything but printable ASCII is an escape and no quote is unescaped, or as a comment
  stripped of anything that could end a line. The check below also exports a plan whose every string tries
  to become code and has Python's own parser confirm the test function is one call per action and names
  nothing but `page`, `target`, `expect`, `re` and `os`.
- **Secrets are names.** A secret is read from `AUTESTIQ_SECRET_<NAME>` in the customer's environment, and
  the export lists the variables it needs. No value is ever in a file.
- **Verified as §4 requires** (`--export-check python --python <interpreter with pytest-playwright>`): every
  baseline of every set exported, run by pytest against the application with each defect off and on.
  **107 of 107 runs gave the plan's verdict**: demo shop 18 of 18, held-out set 26 of 26, clinic 26 of 26,
  invoice desk 26 of 26, eShopOnWeb 11 of 11. Every defect a plan catches, its exported test catches, and no
  exported test fails an application the plan passes. Measured with Playwright for Python 1.65.2, pytest
  8.4.2 and pytest-playwright 0.7.1, in a virtual environment outside the repository; nothing in the product
  depends on Python.

**Not built yet.** The language is to be a setting of each project, and projects do not exist yet
(Milestone 5): until they do, the exporter is reached through the benchmark tool only, and nothing in the
product offers an export. TypeScript, Java and C# are not built; each needs its own exporter and the same
107-run check before it is offered. The second open question above — whether export is offered for every
plan or only for plans a person has approved — is still the owner's.
