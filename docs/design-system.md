# Design System

> Status: **Accepted** (Step 0 approved on 2026-11-11). Palette: **Option A "Indigo & Teal"** (ADR-0005).
> All contrast ratios below were calculated with the WCAG 2.x relative-luminance formula.

> This document is the **visual** language: color, type, spacing, components.
> How the interface **behaves** — states, forms, tables, AI suggestions, wording — is in
> [`ux/patterns.md`](ux/patterns.md), and who it is for is in [`ux/users.md`](ux/users.md).

## 1. Principles

1. **Calm and trustworthy.** White and light-gray surfaces, one strong brand color, few accents.
2. **Spacious.** Generous whitespace; let content breathe; strong section rhythm.
3. **Clear structure.** Obvious hierarchy, consistent grid, predictable navigation.
4. **Confident typography.** Large, tight-tracked headings; comfortable body text.
5. **Status is data.** In the app, color is reserved for meaning (pass/fail/severity), always
   paired with an icon or label — never color alone.
6. **Original.** Our own brand: own logo, own SVG illustrations (abstract "flow lines" and
   node graphs that echo the testing journey), no borrowed assets or layouts.

## 2. Color palette

**Chosen: Option A "Indigo & Teal"** (ADR-0005). Options B and C are kept for the record.
All three use the same neutrals and semantic colors (§3); only brand and accent differ.

### Option A — "Indigo & Teal" *(chosen)*
Calm, technical, distinctive. Teal reads as "quality / passing" without being the success green.

Tokens are named by their role, not by a shade number, and every color token holds its
light and its dark value (`light-dark()`, ADR-0008). Components only use these names.

| Token | Light | Dark | Notes |
|---|---|---|---|
| `--color-brand` | `#2E3192` | `#8B93FF` | Primary color. Also the link and the focus color |
| `--color-on-brand` | `#FFFFFF` | `#0E1124` | Text on the brand color: **10.66 : 1** light, **6.86 : 1** dark |
| `--color-brand-hover` | `#1F2170` | `#A5ABFF` | Hover and pressed state of brand-colored controls |
| `--color-brand-subtle` | `#E8E9FB` | `#1C1F4A` | Selected rows, the option in effect, subtle highlights |
| `--color-accent` | `#0F766E` | `#5EEAD4` | Accent text and fills: **5.47 : 1** on white, **12.63 : 1** on the dark background |
| `--color-on-accent` | `#FFFFFF` | `#0E1124` | Text on the accent color |
| `--color-accent-decorative` | `#14B8A6` | `#14B8A6` | Illustrations and glows only, never text |
| `--color-hero-start`, `--color-hero-end` | `#1F2170`, `#2E3192` | same | Hero areas keep their deep indigo in both themes |
| `--color-on-hero`, `--color-on-hero-muted` | `#FFFFFF`, `#C7CBFF` | same | Text on hero areas |

### Option B — "Midnight Blue & Amber" *(not chosen)*
Classic enterprise, warm call-to-action.

| Token | Light | Dark theme | Notes |
|---|---|---|---|
| `--color-brand-700` (primary) | `#0B3D91` | `#6CB4FF` | White on `#0B3D91`: **10.04 : 1** · `#6CB4FF` on dark: **8.66 : 1** |
| `--color-accent-500` | `#F5A524` | `#F5A524` | Amber **needs dark text** (`#0B1020` on amber: **9.28 : 1**); never white text |
| `--color-accent-700` | `#B45309` | — | Amber text/links on white: **5.02 : 1** |

Risk: amber collides with "warning" semantics in the app; we'd use it only on the public site.

### Option C — "Royal Indigo & Coral" *(not chosen)*
Brighter, more modern / start-up feel.

| Token | Light | Dark theme | Notes |
|---|---|---|---|
| `--color-brand-700` (primary) | `#4338CA` | `#8B93FF` | White on `#4338CA`: **7.90 : 1** |
| `--color-accent-600` | `#C2410C` | `#FB923C` | White on `#C2410C`: **5.18 : 1** · `#FB923C` on dark: **8.25 : 1** |

Note: the brighter coral `#E8590C` fails with white text (3.58 : 1), so the accent must use the darker shade.
Risk: coral is close to "danger" red in the app.

## 3. Neutrals and semantic colors (shared)

| Token | Light | Dark | Use |
|---|---|---|---|
| `--color-bg` | `#FFFFFF` | `#0E1124` | Page background |
| `--color-surface` | `#FFFFFF` | `#161A33` | Cards, panels |
| `--color-surface-subtle` | `#F5F6FA` | `#1B2040` | Alternating sections, app background |
| `--color-border` | `#D0D4E4` | `#2C3258` | Dividers (decorative, not required to hit 3:1) |
| `--color-border-strong` | `#7B8199` | `#6B7290` | Input borders: at least **3.2 : 1** on every surface in both themes (1.4.11 non-text contrast) |
| `--color-text` | `#1A1D2E` | `#EEF0F8` | Body text: **16.68 : 1** on white |
| `--color-text-muted` | `#5B6178` | `#A3A9BF` | Secondary text: **6.13 : 1** on white, **5.68 : 1** on `#F5F6FA`, **7.99 : 1** on dark |
| `--color-success` | `#13753A` | `#4ADE80` | Passed: **5.78 : 1** on white |
| `--color-warning` | `#AD4407` | `#FBBF24` | Warnings, flaky: **5.82 : 1** on white |
| `--color-danger` | `#B42318` | `#F87171` | Failed, critical: **6.57 : 1** on white |
| `--color-info` | `#1D4ED8` | `#60A5FA` | Info, running: **6.70 : 1** on white |

Three values changed against the first draft (border-strong, success, warning) because they
missed the minimum on the gray and the tinted surfaces. `TokenContrastTests` now checks every
text color on every surface, the control borders and the focus color, in both themes.
It reads `tokens.css` directly, so a later color change cannot slip below WCAG AA unnoticed.

`--color-backdrop` (`rgb(14 17 36 / 0.5)`) dims the page behind dialogs.

### Test status tokens (app)
| Status | Token | Icon |
|---|---|---|
| Passed | `--status-passed` → success | check-circle |
| Failed | `--status-failed` → danger | x-circle |
| Running | `--status-running` → info + pulse animation | spinner/progress ring |
| Blocked | `--status-blocked` → warning | block |
| Skipped / Not run | `--status-skipped` → text-muted | minus-circle |
| Flaky | `--status-flaky` → warning | wave |

### Defect severity tokens
Critical (danger, filled badge) · Major (warning, filled) · Minor (info, outlined) · Trivial (neutral, outlined).

## 4. Typography

- **Font:** **Inter** 4.001 (SIL Open Font License), variable font, **self-hosted** (`woff2`), `font-display: swap`. No font CDN (GDPR). The "latin" file (48 KB) covers English and German and is preloaded; "latin-ext" is fetched only when a page needs it. A metric-adjusted fallback ("Inter Fallback", based on Arial) keeps the layout from shifting while the font loads. Sources and licenses: `THIRD-PARTY-NOTICES.md`.
- **Monospace:** **JetBrains Mono** 2.211 (OFL) for logs, IDs, code. Programming ligatures are switched off, so logs show characters as they are.
- **Features:** tabular numbers in tables and KPIs (`.t-tabular`). The font files in use do not contain Inter's optional letter variants (`cv11`, `ss01`), so the design does not use them.

Type scale (1.25 ratio, fluid on the public site with `clamp()`):

| Token | Size (desktop) | Line height | Weight | Use |
|---|---|---|---|---|
| `--text-display` | 64 px (clamp 40–64) | 1.05 | 700, tracking −0.02em | Home hero |
| `--text-h1` | 48 px (clamp 34–48) | 1.1 | 700 | Page titles (public) |
| `--text-h2` | 36 px (clamp 28–36) | 1.15 | 650 | Section titles |
| `--text-h3` | 24 px | 1.25 | 600 | Card titles, app page titles |
| `--text-h4` | 20 px | 1.3 | 600 | Sub-sections |
| `--text-lead` | 20 px | 1.55 | 400 | Intro paragraphs |
| `--text-body` | 16 px | 1.6 | 400 | Body |
| `--text-body-sm` | 14 px | 1.5 | 400 | App UI default, table cells |
| `--text-caption` | 12 px | 1.4 | 500 | Labels, metadata (min. size) |
| `--text-overline` | 12 px | 1.4 | 600, uppercase, tracking 0.08em | Eyebrows above headings |

Max line length for reading text: `65ch`.

## 5. Spacing, grid, layout

- **Spacing scale** (4 px base): `--space-1` 4 · `2` 8 · `3` 12 · `4` 16 · `5` 20 · `6` 24 · `8` 32 · `10` 40 · `12` 48 · `16` 64 · `20` 80 · `24` 96 · `32` 128 px.
- **Section rhythm (public):** `--space-24` vertical padding desktop, `--space-16` mobile.
- **Grid:** 12 columns, CSS Grid, gutter `--space-6` (24 px) desktop / `--space-4` mobile.
- **Max content width:** `--container-max: 1440px`, inner padding `clamp(16px, 4vw, 64px)`. Text-heavy pages use `--container-narrow: 880px`.

| Breakpoint | Min width | Columns | Typical layout |
|---|---|---|---|
| `xs` (mobile) | 0 | 4 | stacked, drawer navs |
| `sm` | 600 px | 8 | 2-up cards |
| `md` (tablet) | 900 px | 12 | 3-up cards, app nav collapses to icons |
| `lg` (desktop) | 1200 px | 12 | full mega menu, full app nav |
| `xl` (wide) | 1600 px | 12 | content capped at 1440 px, app uses extra space for side panels |

Breakpoints are used in CSS isolation files via shared custom media / documented values (CSS custom properties can't be used in media queries, so the values are documented here and repeated as constants).

- **App layout:** left nav 264 px (collapsed 64 px), top bar 56 px, content padding `--space-6`, app background `--color-surface-subtle`, content on white cards.

## 6. Shape and depth

| Token | Value | Use |
|---|---|---|
| `--radius-sm` | 4 px | Badges, checkboxes |
| `--radius-md` | 8 px | Buttons, inputs, menu items |
| `--radius-lg` | 12 px | Cards, dialogs |
| `--radius-xl` | 20 px | Hero media, feature panels |
| `--radius-full` | 9999 px | Pills, avatars |
| `--shadow-1` | `0 1px 2px rgb(16 20 50 / .06), 0 1px 3px rgb(16 20 50 / .08)` | Cards at rest |
| `--shadow-2` | `0 4px 12px rgb(16 20 50 / .08)` | Hover, dropdowns |
| `--shadow-3` | `0 12px 32px rgb(16 20 50 / .12)` | Mega menu, dialogs |

Dark theme replaces shadows with lighter surfaces + 1 px borders (shadows are barely visible on dark).

## 7. Motion

| Token | Value |
|---|---|
| `--duration-fast` | 120 ms (hover, focus) |
| `--duration-base` | 200 ms (menus, tabs) |
| `--duration-slow` | 320 ms (drawers, page sections) |
| `--ease-standard` | `cubic-bezier(.2, 0, 0, 1)` |
| `--ease-exit` | `cubic-bezier(.3, 0, 1, 1)` |

- Animate only `opacity` and `transform`.
- `@media (prefers-reduced-motion: reduce)`: durations → `0.01ms`, no parallax, no pulsing; live-run "current step" uses a static highlight instead of a pulse.
- No auto-playing carousels.

## 8. Components

**Built in Milestone 1** (each with bUnit tests): `TSkipLink`, `TBrand` (mark + product name),
`TLinkButton` (primary, secondary), `TChoiceForm` (a row of buttons that posts one choice;
works without JavaScript), `TThemeSwitch`, `TLanguageSwitch`, and the two layouts
`SiteLayout` and `AppLayout`. Global helper classes: `.t-container`, `.t-visually-hidden`,
`.t-display`, `.t-lead`, `.t-overline`, `.t-muted`, `.t-tabular`.

**Milestone 2 scope:**

All in `Testo.UI.Shared`, prefixed `T` (e.g., `TButton`) so they don't clash with Fluent's `Fluent*` names.

| Component | Variants / notes | Built on |
|---|---|---|
| `TButton` / `TLinkButton` | primary, secondary, ghost, danger; sm/md/lg; icon-only (requires `AriaLabel`); loading | own (works in static SSR) |
| `TCard` | default, interactive (whole card link), feature, stat | own |
| `TInput`, `TTextArea`, `TSelect`, `TCheckbox`, `TRadioGroup`, `TSwitch` | label, hint, error, required; bound to `EditForm` validation | own for public site; Fluent wrappers in app |
| `TBadge` / `TStatusBadge` | neutral, brand, status (passed, failed, …), severity | own |
| `TTabs` | horizontal, with keyboard arrows (ARIA tabs pattern) | Fluent in app; own static version for public pages |
| `TMegaMenu` | Disclosure pattern, columns + featured card, mobile drawer | own + small JS enhancement (ADR-0004) |
| `TSiteHeader` / `TSiteFooter` | public site chrome | own |
| `TDataTable<TItem>` | sorting, paging, selection, sticky header, empty/loading states, density | wraps `FluentDataGrid` |
| `TKpiTile` | value, label, delta (up/down, good/bad), sparkline | own (inline SVG sparkline) |
| `TTimeline` | vertical steps with status, current step, timestamps, expandable details | own |
| `TSkeleton`, `TEmptyState`, `TErrorState` | standard states | own |
| `TDialog`, `TDrawer`, `TMenu`, `TTooltip`, `TToast` | app only | wrap Fluent |
| `TChart` (line, bar, donut) | simple charts as accessible inline SVG with a data-table fallback | own (no JS chart lib) |
| `TAvatar`, `TBreadcrumbs`, `TPagination`, `TSection`, `TContainer`, `TGrid` | layout + misc | own |

### Fluent UI theming *(ADR-0007)*
Fluent UI Blazor 5 takes its look from CSS variables. The app layout points those
variables at **our** tokens, so its components follow our brand and both themes without a
second theme definition. Milestone 1 maps the brand color, font, corner radius and focus
color (a Fluent button is already exactly our indigo). Milestone 2 completes the mapping
for neutrals, strokes, type ramp and shadows. Fluent UI is loaded on app pages only.

## 9. Iconography and imagery

- **Icons:** Fluent UI System Icons (MIT license) via the Fluent icons package — consistent with the components; used at 16/20/24 px, 1.5 px visual stroke.
- **Logo:** original wordmark + simple geometric mark: a check that flows into a node, on a rounded brand-colored tile (`TBrand`, also the favicon). Placeholder until you have a final name.
- **Illustrations:** original abstract SVGs (flow lines, nodes, grid dots) in brand colors, theme-aware via `currentColor` / CSS variables.
- **Photos:** none in this phase. Any placeholder image is a neutral SVG with a visible "Placeholder" label.
- **Customer logos:** generic shapes labeled "Customer logo placeholder".

## 10. Accessibility rules (WCAG 2.2 AA)

1. **Contrast:** text ≥ 4.5 : 1 (≥ 3 : 1 for ≥ 24 px or ≥ 18.66 px bold); UI component boundaries and focus indicators ≥ 3 : 1.
2. **Focus:** visible on every interactive element: `outline: 2px solid var(--color-focus); outline-offset: 2px` (`--color-focus` = brand-700 light / brand-300 dark). Focus is never hidden behind sticky headers (2.4.11 Focus Not Obscured).
3. **Keyboard:** everything works with keyboard only; logical tab order; Escape closes menus/dialogs and returns focus; "Skip to main content" link on every page.
4. **Target size:** interactive targets ≥ 24 × 24 px (2.5.8); we aim for 40 px in the public site and 32 px in dense app UI.
5. **Semantics first:** native elements (`button`, `a`, `details`, `table`) before ARIA; landmarks (`header`, `nav`, `main`, `footer`); one `h1` per page; no heading level skips.
6. **Forms:** visible labels, error text linked via `aria-describedby`, errors announced (`aria-live="polite"`), no placeholder-only labels; don't ask for the same info twice (3.3.7 Redundant Entry).
7. **Status is not color-only:** always icon + text.
8. **Live regions:** the live test run announces step changes politely (rate-limited so screen readers aren't flooded), with a toggle to mute announcements.
9. **Charts:** every chart has a text summary and an accessible data table alternative.
10. **Motion:** respect `prefers-reduced-motion`; no flashing content.
11. **Language:** `lang` attribute per page; `lang="en"` on English fragments inside German pages where needed.
12. **Zoom/reflow:** usable at 400 % zoom / 320 px width without horizontal scrolling (except data tables, which scroll inside their own region).
13. **Testing:** axe scan in every Playwright smoke test (zero serious/critical violations), plus keyboard-only checks in bUnit for interactive components.

Germany note: the **Barrierefreiheitsstärkungsgesetz (BFSG)** applies to many B2C digital services since June 2025; aiming at WCAG 2.2 AA keeps us on the safe side.
