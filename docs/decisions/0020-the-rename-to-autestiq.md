# 0020 — The rename to Autestiq, and what was deliberately left alone

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

The company and the product became **Autestiq**, with the tagline *Autonomous Testing. Intelligent
Quality.* The previous name, "Testo", had always been recorded as a working name, and `CLAUDE.md`
said a rename would be a mechanical find-and-replace.

It mostly was. This ADR records the places where it was not, because those are the decisions
somebody will otherwise have to re-derive from a 600-file diff.

## Decision

**The name moved everywhere it is a name.** Twenty-six projects and every namespace
(`Testo.*` → `Autestiq.*`), the configuration prefix (`Autestiq`), the environment prefix
(`AUTESTIQ_`), internal service identifiers (`autestiq-*`), the default development database
(`autestiq_dev`), the brand palette (ADR-0005, amended) and all user-facing text.

**The database schema did not move.** Every table and column is already brand-free snake_case —
`users`, `organizations`, `test_runs`, `test_run_steps`, `artifacts`. There was nothing to rename,
so no migration was written, none was regenerated or squashed, and the migration IDs in
`__EFMigrationsHistory` are untouched. An existing database keeps working.

What *did* have to move inside the migration files are the CLR type names that the Designer files
and the model snapshot carry as strings (`"Testo.Domain.Identity.User"`). Those name types, not
schema. Leaving them would have made the next `dotnet ef migrations add` believe every entity had
been dropped and re-added.

**The database name is a new-installation default, not a migration.** `autestiq_dev` is what a
fresh checkout creates. An existing database keeps its name and keeps working — the name is not in
the schema, so pointing the connection string at it is the whole procedure, and the data and the
history survive untouched.

**Cookie-borne names moved, and that signs people out once.** The claim prefix is `autestiq:` and
the theme cookie is `autestiq.theme`. A session cookie issued before the rename no longer carries
the claims the application reads, so a person signs in again and their theme falls back to their
system setting once. This is acceptable because nothing is deployed. **Had there been production
sessions, the right move would have been to read both prefixes for one release and then drop the
old one**, and that is the procedure to follow if either name ever changes again.

## Consequences

**Easier.** One name, one prefix, one palette. A reader of the solution sees the product they are
working on.

**Harder, once.** Anyone with `ConnectionStrings__Testo`, `TESTO_TEST_DB`, `TESTO_DB` or
`TESTO_BROWSER_PATH` exported has to rename those variables; nothing falls back to the old names,
deliberately, because a silent fallback is how two spellings end up supported forever.

**To remember.** The product name is read from `BrandOptions.ProductName` and the tagline from the
`Brand_Tagline` resource. That is the reason this rename cost one line in the UI instead of
hundreds, and it is worth keeping even though the name is now settled.

## What was deliberately left alone

- **The `T` prefix on design-system components** (`TButton`, `TDataTable`) and the `t-` CSS class
  prefix. They are the design system's, not the brand's; they appear in no user-facing text; and
  renaming forty components and every CSS class would be churn with a real chance of breaking
  styling that no test covers.
- **GitHub URLs in the documentation** (`github.com/abdallahtahboub/Testo/issues/26`). They point at
  the repository as it is named today and they resolve. They can be updated when and if the
  repository is renamed — GitHub redirects old URLs, so there is no hurry and no breakage either
  way.
- **"Testo" where it is history.** ADR-0005's superseded decision, the Step 0 entry in the roadmap,
  and ADR-0002's structure note all still say what was decided under the old name, marked as
  historical. An ADR is a record of what was decided and why, not a description of the present.
- **`Testobjekt`** in the German UX copy, which is the German word for "test object" and has
  nothing to do with the brand. It is the reason the rename was done with anchored patterns rather
  than a case-insensitive replace.

## Alternatives considered

- **Renaming the database and its tables to match the brand.** Rejected: the schema carries no
  brand, so this would be a destructive migration bought for nothing.
- **Keeping `Testo.*` namespaces and renaming only the user-facing name.** Rejected: the brief asked
  for `Autestiq.*`, and a codebase whose internals say one name while its screens say another is a
  question every new developer has to ask once.
- **Using the requested accent `#55DCE0` as the light-theme brand colour.** Rejected on measurement:
  1.65 : 1 on white. See ADR-0005's amendment.
