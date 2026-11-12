# 0009 — Public pages ship no Blazor script and set no cookie

- **Status:** Accepted (refines ADR-0003)
- **Date:** 2026-11-12

## Context
ADR-0003 made public pages static. Building it showed two things the default setup still
does to a static page:

1. `blazor.web.js` loads on every page, and with it the scripts of all component
   libraries. With Fluent UI that is more than 600 KB for a page that uses none of it.
2. Blazor issues an antiforgery token for every page it renders, with or without a form.
   That sets a cookie on the first visit and marks the response "no-store", which also
   keeps the browser from restoring the page instantly on back/forward.

## Decision
- A page is public when it has `[ExcludeFromInteractiveRouting]`; all pages of
  `Testo.Website` have it. `App.razor` renders public pages without `blazor.web.js`,
  without the import map and without the app's style bundle.
- Public pages therefore have no enhanced navigation. Links are normal page loads.
- `PublicPageAntiforgery` wraps the framework's antiforgery service and issues no token for
  public pages. A public page with a form that posts back to itself opts in with `[HasForm]`.
  Validation of incoming posts is never skipped.
- The theme and language switches post to two small endpoints that work without a token
  (they store a display preference only, reject posts from other websites and redirect
  only within the site).

## Consequences
- A public page is a few small files and needs no server connection. A plain visit sets
  no cookie, which keeps the cookie notice short.
- A form on a public page without `[HasForm]` is rejected with "400 Bad Request". That is
  loud on purpose, and Milestone 3 adds a test that finds such pages.
- `PublicPageAntiforgery` relies on Blazor accepting "no token". The browser tests cover
  every public page and would fail if a framework update changed that.
- App pages are unchanged: Blazor script, antiforgery token, server connection.

## Alternatives considered
- Keep the defaults: simplest, but every visitor pays for the app's scripts and gets a cookie.
- Remove the cookie and the header from the response afterwards: hides the symptom and
  breaks pages that really need the token.
