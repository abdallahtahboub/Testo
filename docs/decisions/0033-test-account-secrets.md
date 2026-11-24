# 0033 — A test account's sign-in details, kept encrypted per environment

- **Status:** Accepted (the owner approved it on 2026-11-18)
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

## Decision
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

## Built (2026-11-18)
- **The store.** `EnvironmentSecret` (organization, environment, name, ciphertext, who set it), tenant-filtered, unique
  per environment and name, deleted with its environment. `IProjectService.SetSecretAsync` and `DeleteSecretAsync`:
  - `ManageProjects` only;
  - names are lower-case words (`^[a-z][a-z0-9_]{0,31}$`), values 1 to 500 characters, kept exactly as typed;
  - refused for production;
  - audited as `environment.secret_set` and `environment.secret_deleted`, with the name and never the value.
  - `ListEnvironmentsAsync` returns names and dates only. No method returns a value.
- **Encryption.** `ISecretProtector` in Application. In Web it is ASP.NET Core Data Protection, under the purpose
  `Autestiq.EnvironmentSecret.v1/{organization}/{environment}`. The key ring lives in the directory
  `DataProtection:KeyDirectory`, and the application refuses to start outside Development without it. A value
  that cannot be decrypted (another key ring, another purpose) refuses the run; it is never sent half-filled.
  Without a protector, the store refuses to save anything.
- **Planning and approval.** The planner is told the environment's secret names. The validator refuses a plan that
  types any other name (`Planning_Error_UnknownSecret`). A plan typing secrets is approvable once the environment
  has every one of them, and the dispatcher checks it again before a run.
- **Running.** The dispatcher decrypts the values when a run starts, for runs of approved plans, and for explorations
  only where ADR-0032's permission stands (or on our own trusted-local applications). Scripted runs get none.
- **Exploring signed in.** With both a `username` and a `password` stored, and only where the exploration may type
  into forms, the explorer adds one more way to fill a form that has a password field: the account's user name in
  its email or text field and the password in its password field. The page it reaches is described as reached by
  "sign in with the test account (secrets username and password), then click …". **Every stored value is then
  replaced in the whole description by `[secret:<name>]` before it leaves the worker** (values of four characters
  or more), so a page that shows "signed in as …" never sends the user name to the model. A plan therefore cannot
  check the user name's text.
- **A pass resting on the account's own value is not a pass.** If a planned run passes and one of its positive checks
  (text or visible) looks for the value of a secret the plan typed, by name or as the same plain text, the worker ends
  it **inconclusive**, naming the secret and never its value (`TypedSecretChecks`). A page can show a value the test
  typed whether or not signing in worked: eShopOnWeb prints its demo account on its login page (ADR-0020). Failures and
  "not there" checks are left alone, so the guard can only take away an unearned pass. **Not covered:** a downloaded
  test runs outside the worker and does not carry this guard (ADR-0023); its text check is the plan's, as approved.
  **Amended 2026-11-24 (Milestone 5.1 R4):** such a plan is no longer downloadable. The guard moved to
  `Autestiq.Contracts.Planning.TypedSecretChecks`, shared by the worker and the download; the download refuses
  (`Tests_Error_ExportRestsOnAccountValue`) and the test page says why by the value's name instead of offering the file.
  When the stored values cannot be read, every secret the plan types counts, so the check fails closed. One regression
  fixture (`tests/Shared/AccountValueFixture.cs`) is run by the worker, where it ends inconclusive, and downloaded by
  the application tests, where it is refused.
- **The screen.** Each test environment on the Environments page has a "Test account" panel: the saved names with
  their dates and a Remove button, and a user name and a password field (the browser is told not to fill either).
  Saving clears both fields; nothing saved is shown again. Who set a value is in the audit log, not on the panel.
- **Tests:** the refusals, the audit, purpose binding, another key ring, roles, production, another organization,
  the validator, approval with and without the secret, values reaching approved runs and permitted explorations
  only, the explorer signing in on the demo shop (and failing without the change), a wrong password staying on the
  refusal, no sign-in without form typing, no value in the description, and a browser test of the panel.
- **Unchanged:** the explore and coverage figures on our three applications, with and without test ids.
- **Measured on eShopOnWeb (2026-11-22, the owner's run).** `--target eshop --signed-in` gives its two sign-in scenarios
  the published demo account's user name as well as its password and writes `docs/ai-benchmark-eshop-signed-in.md`, so
  the 18-of-22 set stays as it was. Result: 18 of 22, no false positive. The explorer signed in and reached checkout;
  the values never reached the planner. Placing an order stays inconclusive because paying is never pressed while
  exploring (ADR-0032), so the thank-you page is unseen (ADR-0020).

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
