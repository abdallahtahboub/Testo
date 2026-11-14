# 0005 — Brand palette: "Navy & Electric Cyan"

- **Status:** Accepted; amended 2026-11-14 for the rename to Autestiq
- **Date:** 2026-11-11

## Context
The design system needs one strong primary brand color plus an accent, in a deep
blue/indigo family. Three options were proposed in `docs/design-system.md` §2.

## Decision
Option A, "Indigo & Teal": primary `#2E3192` (indigo), accent `#0F766E` (teal), with the
shared neutrals and semantic colors from `docs/design-system.md` §3.

> **Superseded by the amendment at the end of this file.** The text above is the decision as it was
> taken on 2026-11-11, kept because an ADR is a record of what was decided and why, not a
> description of the present. The palette in use today is "Navy & Electric Cyan".

## Consequences
- The accent does not collide with the status colors of the app (success green, warning
  amber, danger red), so status always reads as status.
- All text pairs meet WCAG 2.2 AA; a test (`TokenContrastTests`) checks the token file.
- Changing the palette later means editing `tokens.css` only; components use tokens, never raw colors.

## Alternatives considered
- B "Midnight Blue & Amber": the amber accent reads as "warning" inside the app.
- C "Royal Indigo & Coral": the coral accent is close to "danger" red inside the app.

## Amendment, 2026-11-14 — "Navy & Electric Cyan"

The company and the product became **Autestiq**, and the brand direction with it: deep navy with
an electric cyan accent, for an enterprise engineering audience.

| Token | Light | Dark |
|---|---|---|
| `--color-brand` | `#162B42` | `#55DCE0` |
| `--color-brand-hover` | `#0B1220` | `#8CE8EB` |
| `--color-on-brand` | `#FFFFFF` (14.40 : 1) | `#0B1220` (11.32 : 1) |
| `--color-accent` | `#0A6A70` (6.35 : 1 on white) | `#55DCE0` (11.32 : 1 on the navy) |
| `--color-accent-decorative` | `#55DCE0` | `#55DCE0` |
| `--color-hero-start`, `--color-hero-end` | `#0B1220`, `#162B42` | same |

**The brand colour swaps role between the themes, which the indigo never had to.** Navy carries the
brand on a light background; on a dark background navy *is* the background, so the cyan carries it
there. Both directions were measured, not judged by eye.

**The requested accent `#55DCE0` is 1.65 : 1 on white.** It cannot hold text in the light theme —
not as a link, not as a label, not at any size — so the light theme uses a darkened cyan for
anything a person reads and keeps the electric value for illustration and glow, where
`--color-accent-decorative` already says it is never text. Shipping the bright cyan as light-theme
link colour would have put the whole site below AA while looking, in a mock-up, exactly right.

The reasoning of the original decision is unchanged and still holds: the accent is a cyan, so it
does not collide with success green, warning amber or danger red, and status still reads as status.
`TokenContrastTests` checks every pair in both themes, and it passed before this amendment was
written down.
