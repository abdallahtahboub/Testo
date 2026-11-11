# 0004 — Small vanilla JS enhancement for the public header

- **Status:** Proposed (needs explicit approval — it is an exception to "no JavaScript")
- **Date:** 2026-11-11

## Context
Public pages are static SSR (ADR-0003), so Blazor event handlers do not run there. The
header still needs: a mega menu that closes on Escape/outside click, an instant theme
switch without a flash of the wrong theme, and a mobile menu with a focus trap.

## Decision
- Build a no-JS baseline with native HTML (`<details>/<summary>`, form POST for the theme cookie).
- Add one small vanilla ES module (target ≤ 2 KB gzipped, no dependencies), loaded as an
  external file (CSP-friendly), that progressively enhances the header.
- No JS framework. The module is covered by Playwright tests (keyboard + pointer).

## Consequences
- The site works without JavaScript; with it, the header feels polished.
- A small amount of JS to maintain and test.

## Alternatives considered
- Interactive Blazor island for the header: opens a SignalR circuit (or downloads WASM)
  for every visitor; hurts first load, the main goal for public pages.
- CSS-only menu (`:hover` / `:focus-within`): cannot close on Escape and is weak for
  touch and screen-reader users; not good enough for WCAG 2.2 AA quality.
