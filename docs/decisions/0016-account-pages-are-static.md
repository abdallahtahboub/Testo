# 0016 — The account pages are rendered statically, outside /app

- **Status:** Accepted
- **Date:** 2026-11-13

## Context
Everything under `/app` is interactive: it renders on the server over a SignalR connection
(ADR-0003). Signing in has to issue a session cookie, and a cookie can only be set on an HTTP
response. An interactive session has no request and no response to put one on — the page was
delivered long before the person pressed the button.

## Decision
Sign in, sign out, the password reset, the one-time code and accepting an invitation live under
`/account`, are marked `[ExcludeFromInteractiveRouting]`, and post plain forms.

They carry `[HasForm]`, so they keep their antiforgery token (ADR-0009).

The split runs through the code as well. `Testo.Application` decides **whether** somebody may in
and hands back a `SignInTicket`; `Testo.Web` decides **how** the session is carried and turns
that ticket into a cookie. Only the host ever touches `HttpContext`.

## Consequences
- The credential rules are testable without an HTTP request, which is why there are sixty-five
  tests over them that need no browser.
- These pages cannot use event handlers, and a validation message costs a round trip. For a
  five-field form that is the right trade, and it is how the public website already works.
- The route test has a third category. Public pages have one address per language, app pages live
  below `/app`, and account pages live below `/account` and are static — each asserted.
- A person's organization and role reach the interactive session through `SignedInUser`, carried
  over with `[PersistentState]` the same way the theme is. The circuit never reads the cookie,
  because it cannot.
- Adding per-organization single sign-on later changes where the sign-in screen sends somebody,
  not how the rest of the product learns who they are.

## Alternatives considered
- **Interactive sign-in with a JavaScript interop call to set the cookie.** Possible, and worse in
  every way: a second mechanism to secure, JavaScript where Blazor would do, and a cookie written
  somewhere other than the one place that should write it.
- **A minimal API endpoint for the form post, with an interactive page in front.** Works, but
  splits one flow across two models and leaves the page unable to show what went wrong without
  either a redirect with the reason in the URL or client-side state.
- **Putting the account pages under `/app`.** They would then be interactive by the rule that
  governs that folder, which is exactly what cannot work.
