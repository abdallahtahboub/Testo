# Open questions and technical debt

> What is unresolved, as of 2026-11-24 (pilot-readiness review included). Each item says where it came from and what would close it. An item leaves
> this file when it is resolved, with the resolution recorded where it belongs (an ADR, the roadmap, a register).
> Security defects and risks have their own register (`docs/security-register.md`); privacy and onboarding gaps have
> theirs (`docs/secure-onboarding-gap-analysis.md`). This file holds what fits neither.

## Open questions (decisions not yet taken)

| Question | Why it matters | Source |
|---|---|---|
| **Where is the product hosted?** *Deferred by the owner on 2026-11-24 to the deployment and go-live stage.* | Decides object storage for evidence, the cloud key service for the test-account key ring, backups, real machine and storage prices in the price book, and the hosting architecture, which is not designed. Until then, work that needs a host is done provider-neutral or waits. | ADR-0029, ADR-0031, ADR-0033 |
| **Must customer data stay in the EU (or in Germany)?** *Undecided; the owner asked for it to be studied first (2026-11-24).* | Shapes hosting, AI providers (where a model call is processed), backups and subprocessors. `Organization.DataRegion` exists but nothing enforces it. Nothing may be built or claimed as EU-only until it is decided. | `docs/commercial-readiness.md` §7 |
| **Checks that cannot be scoped to one row or section.** | On PetClinic two plans could only check that a text is somewhere on the page ("none", "spayed"), not in the row or section the scenario names, and so ended partial (ADR-0020, 2026-11-24). Would exploration describing each text with its container, as locators already are (ADR-0024), let the planner name it? | ADR-0020, ADR-0024 |
| **Repository visibility.** The repository is public; `LICENSE` calls the source proprietary and confidential. Make it private, or change the licence? Not changed by any session. | A public repository publishes the code, the registers and the benchmark. | Pilot-readiness review, 2026-11-24 |
| **Which email provider?** | No deployed host can start without one (D-3's refusal). Needed for Milestone 5.1 R2. | Pilot-readiness review |
| **Approve Milestone 5.1 (pilot readiness)?** R1–R7 as proposed, or amended. | It is the smallest path to a staging rehearsal. | `docs/roadmap.md` §Milestone 5.1 |
| **Branch protection and required checks.** Can a session confirm them, or will the owner? They could not be read from a session. | A release procedure is only as good as the checks that hold it. | Pilot-readiness review |
| **How long must the audit log and the accounting records (usage, costs) be kept?** | `docs/data-architecture.md` §3 proposes 2 years for the audit log and leaves accounting records open; law may require longer for both. Needs counsel. | ADR-0038 |
| **When does the first pilot start, and with whom?** | The other testing-journey modules, the app shell's dashboard and the billing sandbox all wait until after it. | ADR-0020, `docs/commercial-readiness.md` |
| **The private agent (G-8): when, and in what form?** | Required before any claim of private execution, and the long-term home for customer secrets. | `docs/secure-onboarding-gap-analysis.md` G-8 |
| **Accept ADR-0036 and the AI learning strategy?** Order (lab and retrieval first, fine-tuning on evidence, no training from scratch), Experiment 0001 (approved 2026-11-24, ~$25–40), new lab applications, a research budget ceiling. | Decides whether and how the learning programme starts. | ADR-0036, `docs/ai-learning/strategy.md` |
| **Will customer-derived experience ever be used beyond the tenant it came from?** | The most valuable data is the most sensitive; the strategy proposes never by default, opt-in only. | `docs/ai-learning/security-and-governance.md` |
| **May the research workspace use Python (PyTorch, Transformers, TRL)?** | Fine-tuning tooling is Python; CLAUDE.md requires approval for non-.NET tools. Only needed from AI learning Phase 4. | `docs/ai-learning/learning-and-models.md` |
| **How should read-only exploration improve?** | It is a customer site's default until an Owner allows acting, and it measures 36 of 66 against 62 of 66. | ADR-0020, ADR-0032 |
| An event platform (Kafka or similar). **Considered and deferred (2026-11-26).** | Not needed at the estimated size (ADR-0038): PostgreSQL holds the run queue, audit log and usage ledger, and SignalR (inside Blazor Server) carries live updates. Before any event platform: a durable outbox in PostgreSQL for webhooks and audit export (E3, E4), then a managed queue if workers spread across regions. Reconsider only on measured need: sustained high event rates, several independent services replaying one history, or a database queue that no longer keeps up; an ADR with the owner's approval. | `docs/standups/2026-11-26.md` |

## Technical debt and known limits

| Item | Effect | Source |
|---|---|---|
| G-2's minimized AI mode is not built. | Every plan sends the full description of the explored site; the owner's rules want minimal data. Marked "before the first pilot". | `docs/secure-onboarding-gap-analysis.md` G-2 |
| Evidence and the key ring live on local disk. | Not fit for more than one machine; waits on hosting. | ADR-0031, ADR-0033 |
| Exploring signed in changes the test account's data. | Shared test accounts drift (eShopOnWeb's demo basket filled up), which hurts repeatability. | ADR-0033, ADR-0020 |
| On an application the benchmark cannot reset, one exploration sees what earlier explorations did. | Exploring may act; the benchmark now plans everything before running anything, but explorations still follow one another. | ADR-0020 |
| The benchmark tool has no automated tests. | Changes to it are checked by replaying recorded plans, by hand. | PR #72 |
| The owner search on PetClinic, eShopOnWeb's quantity field: unlabelled fields cannot be named. | The vocabulary names elements by test id, role and name, or text; a field with none of them cannot be typed into. | ADR-0024, `tools/Autestiq.Benchmark/PetclinicScenarios.cs` |
| Every planner figure predates the prompt of ADR-0042 (2026-11-25): label targets, the menuitem and tab roles, wider containers. | The unseen sets, PetClinic and the release candidate are re-measured before their figures are quoted for the current prompt; that needs API credit. | ADR-0042, ADR-0020 |
| Exploring a client-side application stops at its budget: Juice Shop's acting and guarded explorations describe 19 pages and stop; its search route, reached only by buttons, and products past the 60-element limit per page are not shown. (Fragment routes and reading before the page is drawn were fixed on 2026-11-25: 24% to 68% read-only, 0% to 74% acting.) | 74% of what the Juice Shop baselines need is shown acting or guarded. | ADR-0021 amendment, `docs/reviews/2026-11-25-r9-juice-shop.md` |
| The crawl spends its page limit on one part of a large application. | On Gitea, read-only exploration described 24 pages of one repository and never reached the sign-in or registration pages its header links to. To fix on a tuning application, not on Gitea before its planner run. | `docs/reviews/2026-11-25-r9-juice-shop.md` |
| The audit log page keeps its own one-read-at-a-time guard, a copy of the Execution module's `DataTurns`. | Modules may not reference each other, so the helper cannot be shared until it moves to `Autestiq.Platform` or `UI.Shared` (an architecture change to propose). Every page that can fire two reads together needs one. | E1.2, `src/Autestiq.Modules.Settings/Pages/AuditLogPage.razor.cs` |
| Enumerations are text with no CHECK constraint; some references by id have no foreign key (`test_runs.test_case_id`, `rerun_of`, `test_run_steps.screenshot_artifact_id`); three paging services have no maximum page size. | A bad write from outside the application is accepted; a careless caller can ask for any page size. | `docs/database-schema.md` §8; E11 step D1 |
| A target cannot be scoped to a plain `div`, nor to a section introduced by a heading. | Text shown twice, once inside a `div` section (Gitea's README), cannot be named; one Gitea scenario could not be written. Landmarks, dialogs and named regions are containers since ADR-0042. | `tools/Autestiq.Benchmark/GiteaScenarios.cs`, ADR-0042 |
| No release; no email provider chosen. | Email is built for any SMTP relay (ADR-0040); choosing a provider is configuration. No release exists yet. | Milestone 5.1 R7 |
| Background jobs log an error every second before the database is migrated. | Noise, not harm; the runbook says to migrate first. | R3 rehearsal F6 |
| `RunDispatcher` is 1,415 lines. | Hard to review. Extract finalization and evidence persistence once its failure paths are covered; no rewrite. | Pilot-readiness review 4.2 |
| Hot queries are unmeasured. | The queue claim, the stale-run reap, the monthly AI spend and the retention sweep have indexes, none checked with query plans on representative data. | Pilot-readiness review 4.3 |
| Email delivery is deferred. | Invitation and reset links go to the log; the application refuses to start that way outside development. | `docs/roadmap.md` §Inventory |
