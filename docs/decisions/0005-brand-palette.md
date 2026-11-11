# 0005 — Brand palette: "Indigo & Teal"

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
The design system needs one strong primary brand color plus an accent, in a deep
blue/indigo family. Three options were proposed in `docs/design-system.md` §2.

## Decision
Option A, "Indigo & Teal": primary `#2E3192` (indigo), accent `#0F766E` (teal), with the
shared neutrals and semantic colors from `docs/design-system.md` §3.

## Consequences
- The accent does not collide with the status colors of the app (success green, warning
  amber, danger red), so status always reads as status.
- All text pairs meet WCAG 2.2 AA; a test (`TokenContrastTests`) checks the token file.
- Changing the palette later means editing `tokens.css` only; components use tokens, never raw colors.

## Alternatives considered
- B "Midnight Blue & Amber": the amber accent reads as "warning" inside the app.
- C "Royal Indigo & Coral": the coral accent is close to "danger" red inside the app.
