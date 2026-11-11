# 0001 — Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
The project will be built over many sessions. Decisions need to stay visible and explainable.

## Decision
Architectural decisions are recorded as short ADRs in `docs/decisions/`, numbered
sequentially, using `0000-template.md`. An ADR is written in the same commit as the change.
ADRs are never deleted; they are superseded.

## Consequences
A small writing cost per decision; a clear history of why things are the way they are.

## Alternatives considered
- Only `architecture.md`: shows the current state but loses the reasons and the history.
