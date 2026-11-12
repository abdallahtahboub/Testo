# 0008 — One token file; themes through `light-dark()`; the server renders the choice

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The product has a light and a dark theme and a "same as system" default. A theme must
never flash (first the wrong one, then the right one), also not on static pages without
JavaScript, and the two themes must not drift apart.

## Decision
- All design tokens live in `Testo.UI.Shared/wwwroot/css/tokens.css`.
- A color token carries both values side by side: `--color-bg: light-dark(#FFFFFF, #0E1124)`.
  Which one applies follows the CSS `color-scheme`:
  - default: `light dark`, so the browser follows the operating system;
  - `<html data-theme="light|dark">`: the visitor's explicit choice.
- The choice is stored in the cookie `testo.theme`. The **server** reads it and renders
  the attribute, so the first paint is already right. "Same as system" needs no cookie.
- The theme switch is a plain form (works without JavaScript). `theme.js` only makes it
  instant where JavaScript runs (ADR-0004).
- `TokenContrastTests` checks the text, control and focus pairs of the token file against
  WCAG 2.2 AA in both themes.

## Consequences
- No duplicated dark block, no script before first paint, no flash.
- `light-dark()` needs browsers from 2024 on (Chrome/Edge 123, Firefox 120, Safari 17.5).
  Older browsers are not supported.
- Tokens that are not colors cannot differ per theme this way. Shadows solve it by using
  `light-dark()` for their color and fading out in the dark theme.

## Alternatives considered
- A `[data-theme="dark"]` block plus a `prefers-color-scheme` media query: needs every dark
  value twice and a test to keep the copies equal.
- A script in `<head>` that sets the theme from local storage: needs JavaScript, an inline
  script (forbidden by our content security policy) and still risks a flash.
