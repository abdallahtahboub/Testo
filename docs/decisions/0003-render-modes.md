# 0003 — Render modes: static SSR for the website, Interactive Server for the app

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
Public pages need fast first load and SEO. The app needs rich interactivity and a live
feed. In this phase all data comes from in-process mock services.

## Decision
- Public website: static server-side rendering with enhanced navigation. No circuit, no WebAssembly.
- Product app (`/app/**`): Interactive Server.
- Interactive WebAssembly / Auto is not used in this phase.

## Consequences
- Visitors of the public site never open a SignalR connection.
- Mock services run on the server; nothing mock-related ships to the browser.
- Fluent UI Blazor components are used only in the interactive app area; public pages use
  our own static-friendly components.
- Moving the app to `InteractiveAuto` later needs a `.Client` project and HTTP service
  implementations, but no UI rewrites, because all data access is behind interfaces.

## Alternatives considered
- WebAssembly for the app: larger download, mocks in the browser, no benefit yet.
- Interactive Server everywhere: simpler, but slower and costlier public pages.
