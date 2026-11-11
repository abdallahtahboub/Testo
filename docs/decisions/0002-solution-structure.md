# 0002 — Solution structure: split Platform from Contracts, Website from Web

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
The suggested structure put DTOs and service interfaces in `Contracts`, and public pages
in the `Web` host. But `ITestCapability` and the navigation/widget registries reference
Blazor types (component `Type`, `RenderFragment`), and the future backend must reuse
`Contracts` without depending on Blazor.

## Decision
- `Testo.Contracts`: DTOs + data service interfaces only, no UI dependencies.
- `Testo.Platform` (new): `IFeatureModule`, `ITestCapability`, registries, route constants, event bus.
- `Testo.Website` (new): all public marketing pages as a static-SSR Razor class library.
- `Testo.Modules.Settings` (new): settings area that hosts capability settings UIs.
- `Testo.Web` is only the composition root.
- Monitoring & control and test completion live in `Modules.Reporting`; test implementation
  and regression/maintenance live in `Modules.Execution`, until they are big enough to split.
- Dependency rules are enforced by an automated test.

## Consequences
- Backend can reference `Contracts` cleanly.
- Two more projects than suggested, but clearer boundaries.
- Splitting a merged stage into its own module later is cheap: modules never reference each other.

## Alternatives considered
- Keep everything in `Contracts`: forces a Blazor dependency on the backend.
- One project per ISTQB stage from day one (10 modules): several would be nearly empty.
