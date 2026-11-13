# 0013 — Our own accounts, behind an interface

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Testo needs sign-in for a business audience: organizations, invitations, roles, and
eventually single sign-on against the customer's own identity provider. The choice is
between running authentication ourselves and buying it.

## Decision
ASP.NET Core Identity, in our own database, for email and password, invitations, roles and
TOTP second factor.

The application never references Identity. It depends on three interfaces in
`Testo.Contracts`: `IAuthenticationService`, `ICurrentUser` and `IOrganizationContext`.

Per-organization OpenID Connect comes later as a second implementation behind the same
interfaces. The sign-in screen asks for the email address first precisely so that the
route can be decided per organization without changing the flow.

## Consequences
- No per-user fee, which matters most in the years when revenue is smallest.
- Account data stays in our EU database: one less processor to list, one less
  data-transfer question to answer.
- We own the security burden: rate limiting, breached-password checks, session rotation,
  recovery flows. `identity-and-access.md` §5 lists what that means in practice, and those
  are the things to get right.
- SSO, which enterprise buyers will ask for, is work we do rather than a setting we enable.
  The boundary is what keeps that from becoming a rewrite.
- Switching to a provider later changes one project.

## Alternatives considered
- **Auth0 or Microsoft Entra External ID.** SSO and MFA on day one, less security burden.
  Rejected for now on per-user cost at the scale where it hurts most, and on having to
  answer where identity data lives.
- **Self-hosted Keycloak or Zitadel.** Free and full-featured, EU-hosted by us. Rejected
  for now as a second system to run and upgrade before there is a customer. Reasonable to
  reconsider when SSO demand arrives.
