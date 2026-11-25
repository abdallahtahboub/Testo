# Gap analysis: the current system against the secure onboarding and privacy requirements

> Written on 2026-11-17, as §10 of [`secure-onboarding-and-privacy.md`](secure-onboarding-and-privacy.md) asks,
> after inspecting the repository at `develop` plus Milestone 5 part 1 (branch `feature/m5-projects`). Nothing
> was restructured to write it. Recommendations marked **needs approval** wait for the owner.

## 1. Where the system stands, in one paragraph
Everything built so far is **black-box**: the planner works from a staging URL alone, and nothing anywhere asks
for or reads source code. Runs against a customer's site are fenced by deterministic controls: proof of
ownership, a destination allowlist, an egress proxy outside a sealed container, refusal of private and metadata
addresses, quotas, budgets and a kill switch. Those already match much of §3 and §5. **The biggest gap is §4: no
control yet decides whether a customer's page content may be sent to an AI provider.** Today that is harmless,
because only the benchmark calls a model, and only on our own applications. **It stops being harmless in
Milestone 5 part 2**, the first time the product itself calls a model for a customer's site. The next biggest
gaps are production targeting, state-changing steps, and evidence retention.

## 2. Already in line with the requirements

| Requirement | What exists | Where |
|---|---|---|
| §2 No source code required; staging-URL onboarding | The crawler and planner work from the running site only. No repository access exists anywhere. | ADR-0019, ADR-0024 |
| §5 Environment authorization | A run reaches a customer origin only with the organization's verified, unexpired proof that it controls the site. | ADR-0026, 3.5 |
| §5 Destination allowlisting, SSRF | The egress policy allows only the run's origin. Private, loopback, link-local, metadata and their IPv6 forms are refused. A redirect is judged at each hop. | `EgressPolicy`, `EgressProxy`, execution threat model §4.1 |
| §5 DNS rebinding | The proxy resolves the name itself, checks every address, and connects only to an address it approved. The browser never resolves. | `EgressProxy.ApproveAsync` |
| §5 Safe browser execution boundary | A sealed container per run: no route out but the proxy, the proxy outside the worker, non-root, read-only, capabilities dropped, resource limits. | ADR-0026 |
| §5 Prompt injection, malicious content | Closed action vocabulary, a validator against what the crawl saw, locators chosen by software, page text marked as data. Exported code holds plan strings only as escaped literals. | ADR-0019, ADR-0023, ADR-0024 |
| §5 Deterministic enforcement | Every control above is code with a test that fails without it; prompts are advisory. | ADR-0019 §5 |
| §5 Multi-tenant isolation | Query filter, save guard, composite keys, an isolation test over every tenant table. | security register D-2 |
| §5 RBAC, least privilege | Permission table by role; every use case checks it. | `identity-and-access.md` §2 |
| §5 Audit logging | Append-only audit log of sign-ins, membership, runs, plan changes, projects and environments. | `AuditWriter` |
| §5 Credentials | Plans name secrets; the model never sees a value; exported tests read them from environment variables; password fields are masked in screenshots. | ADR-0019, `ScenarioRunner` |
| §5 Resource limits, abuse | Per-organization quotas, concurrency with reservation, rate limits, per-run budgets, kill switch, stuck-run reaping. | ADR-0027, 3.5 |
| §6 Assumptions stated | The planner's notes already say "I assumed…" and "I could not check…", and a partial plan never passes. | ADR-0019, ADR-0020 |
| §3 A seam for private execution | The worker is a separate program speaking a job/result protocol over standard input and output. It runs as a process or a container today, and could run elsewhere tomorrow. | `WorkerProtocol` |
| §4 A seam for other AI processing | Planning goes through `IScenarioPlanner`; the provider is one implementation. | `Autestiq.Ai` |

## 3. Gaps, in order of priority

### P0 — needed before the product calls a model for a customer (before Milestone 5 part 2 ships)

**G-1 · No permission decides whether page content may go to an AI provider (§4).** *Built 2026-11-17, Milestone 5
part 2 (ADR-0030): off by default, set with an acknowledgement, audited, refused in `TestCaseService` and checked
again in `PlanningDispatcher` before each call, with negative tests.*
The crawl's snapshot holds page titles, headings, element names and visible texts. On a real staging site, those
can be customer names, emails, orders and prices. Today nothing would stop that from being sent to Anthropic the
moment part 2 calls the planner.
*Recommendation:*
- A **data-processing policy per project**, off by default, set by someone with `ManageProjects`, audited:
  "AI planning may process discovered page content, sent to [provider], under [terms]".
- No planning call is made without it, enforced in the service, not the screen, with a negative test.
- The policy screen says plainly what is sent and what never is (credentials, screenshots, traces).

**G-2 · No minimization of what is sent (§4).** *Recording built 2026-11-17, Milestone 5 part 2: each planning
attempt's text is stored and shown on the test's page. The minimized mode is not built and not measured.*
*Recommendation:*
- Record exactly what each planning call sent, per run, so a customer can see it.
- Design, measure and then offer a **minimized mode** that masks values in visible text (emails, numbers, names)
  while keeping structure and labels.
- Measure it on the benchmark before offering it, because masking can lower accuracy.
- Keep credentials and evidence out of prompts entirely, as today.
*Pilot-readiness review, 2026-11-24: confirmed open.* The whole explored `SiteSnapshot` goes to the planner. A
rehearsal with synthetic data does not need it; real customer data does. **Design to measure:** send only the
elements of the pages the scenario's words and stated results point at, plus each page's title and address. Mask
values in visible text (emails, numbers, names) while keeping labels and roles. Keep the project's opt-in and the
record of what was sent. **Tests:** a value excluded by the policy, and every credential value, absent from the
provider request, the stored record and the logs. **Measure** the unseen sets with and without it before offering it.

**G-3 · No production flag; production is not off by default (§5).** *Built 2026-11-17, Milestone 5 part 1
(ADR-0030).*
Part 1's environments have a name and an address, nothing else.
*Recommendation:*
- Each environment has a **kind**, test or production, chosen when it is added.
- A production environment cannot be run against unless an Owner explicitly authorizes it, with a stated
  acknowledgement, audited.
- Enforced in the dispatcher like the ownership proof.
- Cheap now: one column. Expensive later, once runs exist.

### P1 — needed before running AI-written plans against customers (Milestone 5 part 3)

**G-4 · State-changing steps are not identified or approved separately (§5).** *Built 2026-11-17, Milestone 5
part 3 (ADR-0030): decided by `PlanEffects` from the plan and the explored site, marked on each step, approved only
with an explicit tick, and held at run time by a read-only guard in the browser for plans approved without it. The
proxy-based detection of non-GET requests recommended below was not used: the proxy cannot see methods inside an
HTTPS tunnel.*
A plan may place an order, book a slot or cancel something. Plan approval is decided, but nothing tells the
approver that a plan changes data.
*Recommendation:*
- Mark a plan **"may change data"** deterministically: any step that submits a form, or any non-GET request the
  proxy sees during discovery or a run.
- Show it on the approval screen in words, and require an explicit tick for such plans.
- The proxy already sees every request, so the evidence exists.

**G-5 · Evidence retention and deletion are not built (§5, gap G12).** *Retention built 2026-11-17 (ADR-0031): a
per-project policy, an hourly deletion job, none stored when a project keeps none. Deletion with a project or an
organization waits for those deletions to exist.*
Screenshots are kept on our servers' disk indefinitely.
*Recommendation:*
- Retention by policy (90 days decided), deletion when a project or organization is deleted.
- A per-project evidence policy: keep screenshots or not.
- Before the first pilot at the latest. It is part 2 of Milestone 3.8, already planned.
*Pilot-readiness review, 2026-11-24: screenshots only.* Still without a lifecycle:
- `TestCase.Snapshot`, what exploring saw;
- `TestCase.SentToModel`, the recorded planning payload, kept forever;
- application logs and backups, which wait on hosting.

There is no project or organization deletion, so nothing deletes them with their owner. **Before real customer
data:**
- a retention for snapshots and payloads, tied to the project's policy;
- project and organization deletion that removes every row and file;
- **deletion verified end to end** by a test that, after deleting, finds no row, file or link that names the deleted
  item;
- a stated backup expiry, so deleted data leaves backups within a known time.

*Designed (proposed) in `docs/data-architecture.md` §5 and §8, ADR-0038.*

**G-6 · Secrets for sign-in scenarios have no encrypted store in the product (§5).** *Built 2026-11-18 (ADR-0033): one
test account per test environment, encrypted with Data Protection under a per-environment purpose, key ring outside
the database, write-only, never for production, names only to the planner, values removed from what exploring
describes. An interim until a private agent keeps secrets on the customer's side (G-8).*
The benchmark passes them in; the product has nowhere to keep them.
*Recommendation:*
- Encrypted at rest (ASP.NET Core Data Protection with a key held outside the database, or a cloud key
  service), never logged, never returned to a screen.
- Or defer scenarios that need sign-in until it exists. **Needs approval:** which.

**G-7 · Observed, documented and assumed are not yet shown apart (§6).** *Built 2026-11-17, Milestone 5 part 2:
"Checked by Autestiq" apart from "The AI's notes: unverified", and the tester's request on its own. The AI's notes
are not split by the model's own labels, because that would trust the model to label itself. Since 2026-11-22
(ADR-0034) a tester may state expected results, sent labelled as theirs, and every check carries a label decided by
software: observed by the explorer, stated by the tester, or assumed and needing verification.*
The planner's notes mix them in prose.
*Recommendation:*
- In part 2's plan view, label each note as observed by the crawl, given by the tester, or assumed, with
  assumptions marked "needs verification".
- The owner's choice to show "Noticed while exploring" as unverified observations already follows this.

### P2 — Phase 2 (private execution)

**G-8 · No private agent (§3).**
*What exists:* the worker protocol, and a container image.
*Missing:*
- an outbound-authenticated agent mode;
- an agent identity with revocation and versioning;
- customer-side secret retrieval;
- local-by-default evidence, and a policy for what is sent back.

*Until it exists, never describe our sealed container as private execution:* it runs on our infrastructure.
**Needs approval** as its own milestone, with an ADR.

### P3 — Phase 3 and later

- **G-9 · No API testing and no OpenAPI or GraphQL import** (§2.3). Phase 3.
- **G-10 · No requirements or documents input** (§2.4, §6). Phase 3.
- **G-11 · No repository integration** (§2.5). Correct as a default. Phase 4, opt-in, least-privilege.
- **G-12 · No customer-controlled AI inference** (§4). The planner interface is the seam. Phase 5.
- **G-13 · Onboarding steps 2, 3 and 6** (application type, connection method, discovery-scope review) are
  missing (§7). The first two matter once there is more than one method. A **discovery-scope review** (which
  pages, how deep, which actions are allowed) fits part 2.

## 4. Architectural risks this adds to the security register
- **R-13 · Customer page content reaching an AI provider: High, from part 2.** Mitigated by G-1 and G-2.
- **R-14 · An approved plan changing or destroying customer data: High, from part 3.** Mitigated by G-4, G-3,
  and ownership proofs.
- **R-15 · Misrepresenting where processing happens: Medium, permanent.** Marketing or UI could call our hosted
  container "private". Mitigated by wording reviews and this document.
- Existing and still relevant:
  - R-9: a hostile page can talk to the planner. Permanent, mitigated by the vocabulary and validator.
  - R-3 and G12: artifacts on local disk.
  - R-7: one database for every tenant. Accepted.

## 5. What is needed now, and what can safely wait

| When | Change | Size |
|---|---|---|
| **Now** | These requirements in CLAUDE.md and `docs/`, and this analysis | Done with this change |
| **Before merging part 1** (recommended) | **G-3:** environment kind, production off by default. One column, a choice in the form, refused in the dispatcher, with tests. | Small |
| **Before building part 2** | **G-1:** per-project AI data-processing policy, off by default, enforced and tested. **G-2:** record what each planning call sent. **G-7:** label the plan's notes by source. ADR-0030 amended. | Medium |
| **Before part 3** | **G-4:** "may change data" detection and explicit approval. **G-6:** secret store, or defer sign-in scenarios. | Medium |
| **Before the first pilot** | **G-5:** retention and deletion (3.8 part 2). G-2's minimized mode, measured. | Medium |
| **Later phases, each with approval and an ADR** | G-8 private agent (Phase 2); G-9 and G-10 specifications and requirements (Phase 3); G-11 repositories (Phase 4); G-12 private AI (Phase 5) | Large |

Nothing in the list requires restructuring the repository or changing working features. The two seams the later
phases need already exist: the worker protocol for execution, and the planner interface for AI.
