# 0014 — A domain and application layer, with the API as a second host

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The user interface currently talks to interfaces in `Testo.Contracts`, implemented by
`Testo.Mocks`. Real persistence replaces those implementations. The question is what sits
behind them, and whether the Blazor application should call an HTTP API or the database.

A QA platform also needs a **public API**: customers push results from their CI pipelines
into it. That is a product requirement, not an internal detail.

## Decision
Three new projects behind the existing contracts:

```
Testo.Domain        entities and the rules that are true regardless of storage
Testo.Application   use cases; implements the Contracts interfaces
Testo.Persistence   EF Core, PostgreSQL, migrations
```

The Blazor host calls `Testo.Application` **in process**. App pages already render on the
server, so an HTTP hop to our own API would add latency and a failure mode for nothing.

`Testo.Api` is a **second host** over the same application layer, for CI integrations and
customers. It is a separate milestone, and it shares the use cases rather than
reimplementing them.

```
Testo.Web (Blazor) ─┐
                    ├─► Testo.Application ─► Testo.Domain
Testo.Api (REST) ───┘            │
                                 └────────► Testo.Persistence ─► PostgreSQL
```

## Consequences
- The user interface does not change when mocks are replaced: `AddTestoMocks()` becomes
  `AddTestoApplication()`, which is what the original architecture was built for.
- One place holds a use case, used by both hosts, so the web app and the API cannot drift.
- `Testo.Mocks` stays, for component tests and for a demo mode that needs no database.
- Three more projects. Justified by the API host and by keeping domain rules testable
  without a database; not justified by fashion.
- Moving the app area to WebAssembly later would mean the browser calling `Testo.Api`.
  That is why the contracts stay free of server types.

## Alternatives considered
- **Blazor calls an HTTP API.** A clean single path, and the usual shape when a front end
  is separate. Rejected: our front end runs on the server, so this is a network hop to
  ourselves, with its own serialization, timeouts and failure modes.
- **No layers; EF Core in the UI project.** Fastest to write and genuinely fine for a small
  application. Rejected because the API host would then have nowhere to live but a copy.
