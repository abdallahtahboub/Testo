# 0040 — Email through any SMTP relay, and an operator command for an instance's first organization

- **Status:** Accepted (the owner chose "build both, then rehearse" on 2026-11-25)
- **Date:** 2026-11-25

## Context
Rehearsing the pilot runbook locally (Milestone 5.1 R3) found two things that stop any deployed instance: the host
refuses to start without an email sender (D-3), and nothing can create the first organization or its owner, so an empty
instance has nobody who can sign in. The email provider is undecided, with hosting (owner, 2026-11-24).

## Decision
1. **SMTP, provider-neutral.** `SmtpInvitationNotifier` sends invitations and password resets through any SMTP relay
   with .NET's built-in `System.Net.Mail.SmtpClient`: no new dependency, and choosing a provider later is configuration
   (`Email:Smtp:*`; the password from the environment). The options refuse what cannot be sent safely: no host, no
   sender address, a link base that is not absolute, plain http to anything but this machine, STARTTLS off to a relay
   that is not on this machine.
2. **One message in English and German**, from resource files (`Application/Resources/EmailResources*.resx`), since a
   person's language is not known before they sign in. The token is only in the link; logs carry the kind of message
   and, on failure, the address and status.
3. **`create-organization <slug> <name> --owner <email> --by <name>`** in the operator tool creates the organization
   on the starting plan and an Owner invitation, audited (`organization.created`, `member.invited`), and sends it by
   email. It refuses without email configured: a link printed in a terminal would sit in its history. No password is
   ever set by anyone but the owner.
4. **Production also refuses to start without `Artifacts:Directory`**, found in the same rehearsal: evidence is
   customer data and was silently defaulting to the temporary directory.

## Consequences
- A deployed instance can start, get its first owner and invite members, with any SMTP provider.
- `SmtpClient` is in maintenance mode upstream; if a provider needs implicit TLS (port 465) or OAuth, a library such as
  MailKit would be a new dependency for the owner to approve.
- Messages are plain text in two languages; a per-person language waits for a stored preference.

## Alternatives considered
- **A provider's HTTP API (SES, Postmark, Mailgun …).** Rejected for now: it chooses the provider the owner deferred.
- **MailKit.** Better maintained, but a new dependency without a present need.
- **A sign-up page.** Rejected: open sign-up is a product and abuse decision, not a deployment fix; an operator
  command is enough for a pilot.
- **Printing the owner's link.** Rejected: tokens in terminal history and logs.
