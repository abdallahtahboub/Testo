# 0017 — Prove one real test execution before building breadth

- **Status:** Accepted
- **Date:** 2026-11-13
- **Supersedes the sequence in:** `docs/roadmap.md` (the milestones themselves are unchanged; the
  order they are built in is)

## Context

After Milestone 3 the product has real identity, real tenancy and a real design system. It has no
testing journey at all: the seven module projects are shells of one class each, `Testo.Mocks` holds
options and a random source rather than services, and `Testo.Capabilities.E2EWeb` is an empty
folder. Twelve pages are routable, six of which are sign-in.

The approved sequence was public website → app shell → seven modules → live run view → polish. That
puts the first real test execution four milestones away, behind roughly thirty screens built over
an assumption: that an execution engine will fit behind the interfaces designed for it.

The independent review in [issue #26](https://github.com/abdallahtahboub/Testo/issues/26) argued
for validating the core value proposition first. This decision accepts that argument, with one
change to its proposal.

## Decision

Build one real, bounded, deterministic test execution before building breadth.

Order of work from here:

1. **Milestone 3.1 — Milestone 3 debt.** The confirmed defects in `docs/security-register.md`:
   the missing authorization gate, tokens in logs, no rate limiting, the tenant guard's trust in a
   pre-set organization. Not new work; unfinished Milestone 3.
2. **Milestone 3.2 — Threat model and execution-isolation ADR.** Written before any code opens a
   browser.
3. **Milestone 3.3 — A deterministic execution slice.** One allowlisted local demo target, one
   happy path, one known failure, real run and step persistence, artifacts, cancel and rerun, in a
   worker outside the Blazor process. No AI.
4. **Milestone 3.4 — Measure, then bounded AI.** A planner behind an interface, schema-validated
   plans, a fixed browser-action vocabulary, hostile pages treated as untrusted data, never
   model-generated code. Benchmarked against the deterministic baseline.
5. **Then Milestone 4 onwards** as already written: the public website, the app shell, the modules.

The milestone numbers of the existing plan are unchanged. The new work is inserted as 3.1 to 3.4
rather than renumbering, because renumbering is what put the issue tracker one behind the roadmap
in the first place.

## Why not the review's order

The review proposed "website essentials, then the execution slice". The website is deferred
entirely instead, for one reason: **the execution slice does not depend on the website, and the
website's content depends on the slice.** Marketing copy written now describes a product we are
guessing at; written after, it describes one we have watched run. Doing the website first means
writing it twice.

The cost is real and accepted: there is nothing to show a prospect until the slice works. A
website in front of a product that does not exist is not a shorter route to a customer, only a
faster route to a conversation that ends badly.

## Consequences

- The first answer to "does this actually work" arrives one milestone from now instead of four.
- If the execution engine reshapes the domain model — which it probably will, around runs, steps,
  evidence and cancellation — it reshapes code nobody has built thirty screens on top of.
- `docs/sitemap.md` and the module plans stay valid; they are later, not wrong.
- Nothing in the product is sellable for longer. This is the deliberate trade.
- Each of 3.1 to 3.4 keeps the existing rule: it ends in a reviewable state and stops for approval.

## Alternatives considered

- **Keep the approved sequence.** Lowest friction, and the option this project is contractually in.
  Rejected because it defers the only question that decides whether the product is viable, and
  accrues the most rework if the answer is inconvenient.
- **Website essentials first, then the slice** (the review's proposal). Rejected as above: the
  dependency runs the other way.
- **Go straight to AI-driven execution.** Rejected outright. Without a deterministic baseline there
  is nothing to compare a model against, and no way to tell a genuine defect from a flaky run or a
  hallucinated assertion. An enterprise QA buyer's entire job is distrusting unverified results.
