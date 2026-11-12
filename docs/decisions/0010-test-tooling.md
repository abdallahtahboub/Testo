# 0010 — Tests on Microsoft.Testing.Platform; browser tests host the app in-process

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The stack asks for xUnit, bUnit and Playwright for .NET. Current xUnit (the `xunit.v3`
package, version 4) runs on Microsoft.Testing.Platform, and the .NET 10 SDK no longer
runs such projects through the old VSTest path of `dotnet test`.

## Decision
- `global.json` selects Microsoft.Testing.Platform as the runner of `dotnet test`.
  Test projects are executables and need neither `Microsoft.NET.Test.Sdk` nor a
  Visual Studio runner package. Shared settings are in `tests/Directory.Build.props`.
- The browser tests start the real application **inside the test process**
  (`WebApplicationFactory` with Kestrel on a free port) in the "Production" environment,
  and one Chromium for the whole run. Each test opens its own browser context.
- A test fails on any console error or warning, script error, failed request or HTTP
  error it did not announce.
- Tests that only need raw HTTP (headers, cookies, redirects) use `HttpClient` against the
  same running application, without a browser.
- `E2E_BROWSER_PATH` can point the tests at an existing Chromium. Unset, Playwright uses
  the browser installed by its install script; CI does that.

## Consequences
- One command, `dotnet test`, runs everything; no separately started server, no fixed port.
- Command-line options are those of Microsoft.Testing.Platform and xUnit v3, for example
  `--filter-class` and `--report-xunit-trx` (not `--filter` and `--logger trx`).
- "Production" means the tests see the friendly error page and the real security headers.
  The factory adds the static files of the class libraries (a published site has them in
  its web root) and one URL that fails on purpose.

## Alternatives considered
- xUnit v2 on VSTest: familiar options, but the older line.
- Starting the app as a separate process: closer to a deployment, but needs process and
  port handling, and configuration cannot be changed per test.
