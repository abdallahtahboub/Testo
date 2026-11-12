# UI/UX design

How screens are designed before they are built, and the rules that make every screen feel
like the same product.

| Document | What it answers |
|---|---|
| [`users.md`](users.md) | Who uses Testo, what each person is trying to get done, and what makes their day worse |
| [`flows.md`](flows.md) | The end-to-end paths through the product, from signing in to signing off a release |
| [`patterns.md`](patterns.md) | How the interface behaves: states, forms, tables, feedback, AI suggestions, permissions |
| [`content.md`](content.md) | How we write: voice, microcopy, and the English/German terminology |

Related, and deliberately kept separate:

- [`../design-system.md`](../design-system.md) — the **visual** language: color, type, spacing, components. What things look like.
- [`../sitemap.md`](../sitemap.md) — every page and its purpose. Where things are.
- These documents — what people are trying to do and how the product should behave. Why things are the way they are.

## How a screen gets designed

Design happens in writing before it happens in code. For anything larger than a single
component:

1. **Name the job.** Which person from [`users.md`](users.md), trying to do what, at which
   stage of [the testing journey](../testing-journey.md)? If it does not serve a named job,
   it does not get built.
2. **Write the flow.** The steps, in order, including what happens when something is
   missing, slow, empty, or goes wrong. Add it to [`flows.md`](flows.md) if it is a path
   people will take often.
3. **Reach for an existing pattern.** Check [`patterns.md`](patterns.md) before inventing
   anything. A new pattern is a decision: it gets added there, with its reason.
4. **Write the words.** Headings, button labels, empty states and error messages, in
   English and German, following [`content.md`](content.md). Text written at the end is
   text nobody thought about.
5. **Build it** from the design system. A screen that needs a component the system does not
   have means extending the system, not styling one page.
6. **Check it** against the definition of done: both themes, keyboard only, both languages,
   mobile width, and the loading, empty and error states.

There are no mockup files in this repository. The design system is the source of truth for
how things look, and a built page in the running app is the only honest preview. Sketches
are welcome, but they do not belong in version control where they can quietly go stale.

## What we are optimizing for

Testo is used all day by people whose job is finding problems. The product should feel
like a calm instrument, not a dashboard competing for attention.

1. **Trust over delight.** A QA engineer stakes their professional judgment on what this
   tool reports. Numbers are explainable, every figure can be traced to its source, and the
   product never implies more certainty than it has.
2. **The next action is obvious.** Every screen answers "what is the state?" and "what do
   I do about it?" — a page that only informs has failed half its job.
3. **Speed for the hundredth time, not the first.** These are daily tools. Keyboard paths,
   sensible defaults and remembered context matter more than onboarding polish.
4. **AI suggests, people decide.** Nothing AI-generated becomes approved on its own, and
   every suggestion says what it was based on.
5. **Nothing is lost.** Work in progress survives a reload, a lost connection and a
   mis-click. Destructive actions are reversible or confirmed.
