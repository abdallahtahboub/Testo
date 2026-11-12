# 0015 — Our own icon set, not an icon package

- **Status:** Accepted
- **Date:** 2026-11-12
- **Supersedes** the icon choice in `design-system.md` §9

## Context
`design-system.md` planned to use Fluent UI System Icons through
`Microsoft.FluentUI.AspNetCore.Components.Icons`. Building the design system is the moment
to check that.

The package carries several thousand icons in several sizes and styles. The product needs
roughly thirty. Because pages render on the server, an unused icon costs nothing to a
visitor — but it costs restore and build time on every machine and every CI run, for a
set we use one percent of.

## Decision
A curated set in `TIcon`, drawn by us: one 20×20 grid, 1.5px strokes, round caps and
joins, outline only. An icon is added when a screen needs it, not in advance.

`TIcon` takes a name from an enum, so an icon that does not exist is a compile error
rather than a blank square. Every icon is decorative by default (`aria-hidden`); giving it
a `Label` makes it meaningful and gives it a `role="img"` and a title.

## Consequences
- No dependency, no build-time cost, and the icons are part of our brand rather than
  borrowed from another product's.
- One optical weight across the whole interface, matching our 1.5px stroke convention.
- Every new icon is drawn by hand. That is a few minutes each, and it keeps the set small
  and deliberate.
- A screen that wants an icon we do not have cannot have it today. That is the point: the
  alternative is three similar arrows in the same product.

## Alternatives considered
- **The Fluent icons package.** Complete, consistent with the Fluent components, MIT
  licensed. Rejected on the ratio of what we would carry to what we would use, and because
  the set is visually Microsoft's rather than ours.
- **An icon font.** Smaller, but worse: icons become text that can be selected, read out
  by a screen reader as a private-use character, and go missing when the font fails.
