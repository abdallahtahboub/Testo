# Open questions and technical debt

> What is unresolved, as of 2026-11-24 (pilot-readiness review included). Each item says where it came from and what would close it. An item leaves
> this file when it is resolved, with the resolution recorded where it belongs (an ADR, the roadmap, a register).
> Security defects and risks have their own register (`docs/security-register.md`); privacy and onboarding gaps have
> theirs (`docs/secure-onboarding-gap-analysis.md`). This file holds what fits neither.

## Open questions (decisions not yet taken)

| Question | Why it matters | Source |
|---|---|---|
| **Where is the product hosted?** | Decides object storage for evidence, the cloud key service for the test-account key ring, and real machine and storage prices in the price book. Several "before the first pilot" items wait on it. | ADR-0029, ADR-0031, ADR-0033 |
| **Checks that cannot be scoped to one row or section.** | On PetClinic two plans could only check that a text is somewhere on the page ("none", "spayed"), not in the row or section the scenario names, and so ended partial (ADR-0020, 2026-11-24). Would exploration describing each text with its container, as locators already are (ADR-0024), let the planner name it? | ADR-0020, ADR-0024 |
| **Repository visibility.** The repository is public; `LICENSE` calls the source proprietary and confidential. Make it private, or change the licence? Not changed by any session. | A public repository publishes the code, the registers and the benchmark. | Pilot-readiness review, 2026-11-24 |
| **Which email provider?** | No deployed host can start without one (D-3's refusal). Needed for Milestone 5.1 R2. | Pilot-readiness review |
| **Approve Milestone 5.1 (pilot readiness)?** R1–R7 as proposed, or amended. | It is the smallest path to a staging rehearsal. | `docs/roadmap.md` §Milestone 5.1 |
| **Branch protection and required checks.** Can a session confirm them, or will the owner? They could not be read from a session. | A release procedure is only as good as the checks that hold it. | Pilot-readiness review |
| **When does the first pilot start, and with whom?** | The other testing-journey modules, the app shell's dashboard and the billing sandbox all wait until after it. | ADR-0020, `docs/commercial-readiness.md` |
| **The private agent (G-8): when, and in what form?** | Required before any claim of private execution, and the long-term home for customer secrets. | `docs/secure-onboarding-gap-analysis.md` G-8 |
| **Accept ADR-0036 and the AI learning strategy?** Order (lab and retrieval first, fine-tuning on evidence, no training from scratch), Experiment 0001 (approved 2026-11-24, ~$25–40), new lab applications, a research budget ceiling. | Decides whether and how the learning programme starts. | ADR-0036, `docs/ai-learning/strategy.md` |
| **Will customer-derived experience ever be used beyond the tenant it came from?** | The most valuable data is the most sensitive; the strategy proposes never by default, opt-in only. | `docs/ai-learning/security-and-governance.md` |
| **May the research workspace use Python (PyTorch, Transformers, TRL)?** | Fine-tuning tooling is Python; CLAUDE.md requires approval for non-.NET tools. Only needed from AI learning Phase 4. | `docs/ai-learning/learning-and-models.md` |
| **How should read-only exploration improve?** | It is a customer site's default until an Owner allows acting, and it measures 36 of 66 against 62 of 66. | ADR-0020, ADR-0032 |

## Technical debt and known limits

| Item | Effect | Source |
|---|---|---|
| Downloaded tests do not carry the test-account guard. | A downloaded test can pass by finding the account's own value; inside Autestiq's runner the same plan ends inconclusive. Confirmed still open 2026-11-24; **proposed fix: block export of such plans, with a parity test** (Milestone 5.1 R4). | ADR-0033 |
| G-2's minimized AI mode is not built. | Every plan sends the full description of the explored site; the owner's rules want minimal data. Marked "before the first pilot". | `docs/secure-onboarding-gap-analysis.md` G-2 |
| Evidence and the key ring live on local disk. | Not fit for more than one machine; waits on hosting. | ADR-0031, ADR-0033 |
| Exploring signed in changes the test account's data. | Shared test accounts drift (eShopOnWeb's demo basket filled up), which hurts repeatability. | ADR-0033, ADR-0020 |
| On an application the benchmark cannot reset, one exploration sees what earlier explorations did. | Exploring may act; the benchmark now plans everything before running anything, but explorations still follow one another. | ADR-0020 |
| The benchmark tool has no automated tests. | Changes to it are checked by replaying recorded plans, by hand. | PR #72 |
| The owner search on PetClinic, eShopOnWeb's quantity field: unlabelled fields cannot be named. | The vocabulary names elements by test id, role and name, or text; a field with none of them cannot be typed into. | ADR-0024, `tools/Autestiq.Benchmark/PetclinicScenarios.cs` |
| A test can stay in Planning for good. | A crash during planning leaves it stuck and its cost unrecorded. Tracked as R-17 in the security register; fix proposed as Milestone 5.1 R1. | Security register R-17 |
| No migration path, health check or release outside development. | Migrations run only with the development seed; there is no `/health`; no tag exists. | Milestone 5.1 R2, R7 |
| `RunDispatcher` is 1,415 lines. | Hard to review. Extract finalization and evidence persistence once its failure paths are covered; no rewrite. | Pilot-readiness review 4.2 |
| Hot queries are unmeasured. | The queue claim, the stale-run reap, the monthly AI spend and the retention sweep have indexes, none checked with query plans on representative data. | Pilot-readiness review 4.3 |
| Email delivery is deferred. | Invitation and reset links go to the log; the application refuses to start that way outside development. | `docs/roadmap.md` §Inventory |
