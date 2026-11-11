# 0006 — Language in the URL for the public site, in a cookie for the app

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
The product ships in English and German from day one and targets Germany. Public pages
must be indexable per language. The app is behind sign-in and is not indexed.

## Decision
- **Public site:** the language is the first URL segment: `/en/...` and `/de/...`. Paths
  stay English in both languages (`/de/pricing`). Each page declares one literal route per
  language (`@page "/en/pricing"` and `@page "/de/pricing"`); a test checks that no page
  misses a language. `/` redirects to the visitor's language (preference cookie, then the
  browser's `Accept-Language`, then English). Pages emit `hreflang` alternates.
- **App (`/app/...`):** no prefix. The language comes from the preference cookie
  (`.AspNetCore.Culture`), then `Accept-Language`, then English.
- **The cookie is only written on an explicit user action** (the language switch). A plain
  visit never sets it, which keeps it a "user preference" cookie that needs no consent banner.
- Supported cultures are `en` (default) and `de`, defined once in `Testo.Platform`.

## Consequences
- Search engines see two separate, linked versions of every public page.
- Literal routes mean unknown URLs are plain 404s, and translated slugs (`/de/preise`)
  can be added later page by page without new infrastructure.
- Adding a third language means adding one `@page` line per public page and the resource files.
- Public pages are static only: an interactive island there would get its language from the
  cookie, not from the URL, and could disagree with the page around it.

## Alternatives considered
- A `{culture}` route parameter on every page: one line per page, but it also matches unknown
  first segments (`/foo/pricing`), needs a shared base class, and Blazor's router has no
  regex constraint to restrict it.
- Stripping the prefix in middleware (`PathBase` per language): pages keep plain routes, but
  the same page becomes reachable under several URLs and the behavior is hard to see from the code.
- Cookie only, no prefix: one URL serves two languages, which is poor for search engines.
