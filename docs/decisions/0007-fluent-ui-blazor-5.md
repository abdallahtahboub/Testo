# 0007 — Fluent UI Blazor 5, pinned, themed through CSS variables

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The app uses Microsoft Fluent UI Blazor for complex components (data grids, dialogs,
menus). When Milestone 1 started, version 5 had become the stable line (5.0.1); version 4
(4.14.5) is in maintenance. Version 5 is built on the Fluent UI Web Components 3 and takes
its whole look from CSS custom properties (`--colorBrandBackground`, `--fontFamilyBase`, …).

## Decision
- Use `Microsoft.FluentUI.AspNetCore.Components` **5.0.1**, pinned exactly in
  `Directory.Packages.props`. Updates are deliberate: read the release notes, run all tests.
- Only `Testo.UI.Shared` references the package. Modules use Fluent components through it.
- Fluent UI is loaded **on app pages only**. Its script (about 460 KB) and style sheet
  (about 175 KB) never reach a public page.
- Our look goes on top in three steps:
  1. `<body no-fuib-style>` switches off Fluent's own page styles; ours apply.
  2. `<body data-theme data-theme-color>` hands Fluent our theme choice and brand color.
  3. The app layout points Fluent's CSS variables at our tokens. Milestone 1 maps brand
     color, font, corner radius and focus color; Milestone 2 completes the mapping.
- When the visitor switches the theme, `theme.js` tells Fluent through the same script
  entry point its own .NET theme service uses (`Blazor.theme`), then clears what Fluent
  stored. Our cookie stays the single source of truth.

## Consequences
- Fluent components follow our tokens in light and dark without a second theme definition.
- We depend on Fluent's CSS variable names and on `Blazor.theme`. Both are checked by a
  browser test (the Fluent button must have our brand color in the dark theme).
- Fluent needs inline styles, so app pages allow them in the content security policy.

## Alternatives considered
- Version 4: stable for longer, but in maintenance, and themed through JavaScript design
  tokens instead of CSS variables, which makes our token mapping harder.
- A floating version (`5.*`): a component library update can change markup and styles;
  that should never happen unnoticed.
