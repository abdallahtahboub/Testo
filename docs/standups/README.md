# Daily stand-ups

> **Rule (the owner, 2026-11-26):** a stand-up is held every working day, and each one is recorded here, in the same
> session, as `YYYY-MM-DD.md`. The record is part of the repository's memory: another engineer or assistant should be
> able to see from these files what was done, what is next and what is waiting, without the conversation.

## How a stand-up is recorded

- **One file per day**, named by the date it was held (`2026-11-26.md`). A second stand-up the same day is added to
  that file under its own time.
- **Facts from the repository, not from memory:** merged pull requests are linked, and figures are given with their
  source (an ADR, a review, a benchmark report).
- **Implemented and planned are never mixed:** "done" means merged into `develop` with green CI.
- **No credentials, no customer data, no conversation transcripts** (`CLAUDE.md`).
- Decisions taken in a stand-up are also recorded where they belong (an ADR, the roadmap, `docs/business/`); the
  stand-up links to them.

## Template

```markdown
# Stand-up, YYYY-MM-DD

## Done since the last stand-up
- … (link the pull request)

## Next
- … (what is proposed, and what the owner chose)

## Blockers and decisions waiting on the owner
| Item | What it blocks |
|---|---|

## Risks to watch
- …

## Decisions taken
- … (link where each is recorded)
```

## Index

| Date | Summary |
|---|---|
| [2026-11-26](2026-11-26.md) | First stand-up: release 0.5.0 merged, guarded pressing, client-side exploration, stronger locators, R9 measured; enterprise programme planned |
