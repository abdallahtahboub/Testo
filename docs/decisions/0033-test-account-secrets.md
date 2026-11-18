# 0033 — A test account's sign-in details, kept encrypted per environment

- **Status:** Proposed (waiting for the owner's approval)
- **Date:** 2026-11-18

## Context
Nothing behind a sign-in can be planned, approved or run today:
- the explorer does not sign in, so it never sees those pages;
- a plan that types a secret cannot be approved (ADR-0030 part 3, gap analysis G-6), because there is nowhere to keep
  one.

eShopOnWeb showed what that costs (ADR-0020). Placing an order was the one scenario still without a verdict, because
checkout is behind a sign-in. On a customer's site, most of what matters is behind one.

The owner's rules:
- credentials never go to a model;
- a private agent should keep secrets on the customer's side (§3);
- security is enforced by software, not by prompts.

A hosted store is therefore an interim, and must be labelled as one.

## Decision (proposed)
1. **What is kept.** Per test environment, the sign-in details of **one test account**: a name for each value, such as
   `username` and `password`, and the value.
   - The screen says, in words, to use an account made for testing and never a real person's.
   - Never for a production environment.
2. **Who.** Owners and QA leads may set, replace and delete them (`ManageProjects`).
   - Values are **write-only**: once saved they are never shown, logged or returned by any API. The screen shows only
     each name, who set it and when.
   - Every change is audited, with names and never values.
3. **How they are kept.** Encrypted at rest with ASP.NET Core Data Protection, under a purpose that names the
   organization and the environment, so one environment's ciphertext cannot be read as another's.
   - **The key ring lives outside the database:** in a configured directory for now, and in the cloud key service
     once hosting is chosen. A stolen database alone reveals nothing.
   - The application refuses to start outside development unless the key ring's location is configured.
4. **Never to a model.** The planner is told the names only, as it is today (`PlanRequest.SecretNames`).
   - The validator gains a rule: a plan may type only a secret the environment has, by name. Any other name refuses
     the plan.
5. **Used only at run time, only where already allowed.** The dispatcher decrypts the values when a run starts and
   passes them in `WorkerJob.Secrets`, which already exists, is never logged, and is masked out of screenshots for
   password fields.
   - **Runs of approved plans:** a plan that types a secret becomes approvable when the environment has every secret
     it names. The approval text lists them.
   - **Explorations,** only where the environment's owner allows exploring to act (ADR-0032). The explorer may then
     sign in with the stored account, filling a sign-in form's email or user field and its password field, and explore
     what is behind it.
6. **Downloaded tests are unchanged:** they read secrets from environment variables by name and never contain a value
   (ADR-0023).
7. **Deletion.** Deleting the secrets, or the environment, removes the ciphertext. Withdrawing exploration's
   permission stops their use in explorations at once.

## Consequences
- **Easier:** flows behind a sign-in can be explored, planned, approved and run, on our applications and a customer's.
- **Harder:** we hold a customer's test credentials, so a compromise of our servers together with the key ring would
  reveal them. This is why it is a test account only, write-only, staging only, and an interim until a private agent
  keeps secrets on the customer's side.
- **Risks, stated:**
  - A typed user name, unlike a password, is not masked in screenshots. It is a test account's, and the evidence
    policy (ADR-0031) still applies.
  - Signing in during exploration can change data, for example a last-login time. That is covered by ADR-0032's
    permission.
- **Deferred:**
  - several accounts per environment, such as roles;
  - multi-factor sign-in;
  - secrets fetched from the customer's own vault;
  - the private agent (G-8).

## Tests the implementation must have
- **Refusals:** a value is never returned by any service method or page, and never appears in logs or the audit log.
  Another organization's secrets are invisible. The store is refused for production. QA engineers, Developers and
  Viewers cannot set or read anything.
- **Encryption:** ciphertext is useless without the key ring, and undecryptable under another environment's purpose.
- **Planning:** a plan naming a secret the environment lacks is refused by the validator.
- **Approval:** a plan typing a stored secret becomes approvable; without the secret it is still refused.
- **Running:** secrets reach the worker for approved runs, and for explorations only with ADR-0032's permission.
- **Evidence:** a password field is masked in screenshots.
- **Measurement:** re-measure eShopOnWeb with its published demo account, with the owner's yes and a price first.

## Alternatives considered
- **Keep refusing plans that sign in.** It is safe, but most of a real site is unreachable.
- **Store the values in the database encrypted with a key in configuration.** A configuration leak would then reveal
  every secret. The key ring outside the database is the stronger separation.
- **Ask for the password at each run, never storing it.** It cannot work for scheduled runs, and invites pasting
  passwords into a page every time.
