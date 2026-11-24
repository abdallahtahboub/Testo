# Open questions and technical debt

> What is unresolved, as of 2026-11-24. Each item says where it came from and what would close it. An item leaves
> this file when it is resolved, with the resolution recorded where it belongs (an ADR, the roadmap, a register).
> Security defects and risks have their own register (`docs/security-register.md`); privacy and onboarding gaps have
> theirs (`docs/secure-onboarding-gap-analysis.md`). This file holds what fits neither.

## Open questions (decisions not yet taken)

| Question | Why it matters | Source |
|---|---|---|
| **Where is the product hosted?** | Decides object storage for evidence, the cloud key service for the test-account key ring, and real machine and storage prices in the price book. Several "before the first pilot" items wait on it. | ADR-0029, ADR-0031, ADR-0033 |
| **A plan that calls itself full while its own notes say part was not tested.** | On PetClinic a plan passed without exercising the search it was asked to test, and still declared full coverage. ADR-0035 step 1 and step 2 are built: a missing coverage, an unchecked stated result, or anything the plan lists as not tested makes it partial. Step 2 still depends on the model listing the gap, and it changed the prompt: **every published figure predates it until the unseen sets and PetClinic are re-measured** (about $3). | ADR-0020 (PetClinic), ADR-0035 |
| **When does the first pilot start, and with whom?** | The other testing-journey modules, the app shell's dashboard and the billing sandbox all wait until after it. | ADR-0020, `docs/commercial-readiness.md` |
| **The private agent (G-8): when, and in what form?** | Required before any claim of private execution, and the long-term home for customer secrets. | `docs/secure-onboarding-gap-analysis.md` G-8 |
| **Accept ADR-0036 and the AI learning strategy?** Order (lab and retrieval first, fine-tuning on evidence, no training from scratch), Experiment 0001 (approved 2026-11-24, ~$25–40), new lab applications, a research budget ceiling. | Decides whether and how the learning programme starts. | ADR-0036, `docs/ai-learning/strategy.md` |
| **Will customer-derived experience ever be used beyond the tenant it came from?** | The most valuable data is the most sensitive; the strategy proposes never by default, opt-in only. | `docs/ai-learning/security-and-governance.md` |
| **May the research workspace use Python (PyTorch, Transformers, TRL)?** | Fine-tuning tooling is Python; CLAUDE.md requires approval for non-.NET tools. Only needed from AI learning Phase 4. | `docs/ai-learning/learning-and-models.md` |
| **How should read-only exploration improve?** | It is a customer site's default until an Owner allows acting, and it measures 36 of 66 against 62 of 66. | ADR-0020, ADR-0032 |

## Technical debt and known limits

| Item | Effect | Source |
|---|---|---|
| Downloaded tests do not carry the test-account guard. | A downloaded test can pass by finding the account's own value; inside Autestiq's runner the same plan ends inconclusive. | ADR-0033 |
| G-2's minimized AI mode is not built. | Every plan sends the full description of the explored site; the owner's rules want minimal data. Marked "before the first pilot". | `docs/secure-onboarding-gap-analysis.md` G-2 |
| Evidence and the key ring live on local disk. | Not fit for more than one machine; waits on hosting. | ADR-0031, ADR-0033 |
| Exploring signed in changes the test account's data. | Shared test accounts drift (eShopOnWeb's demo basket filled up), which hurts repeatability. | ADR-0033, ADR-0020 |
| On an application the benchmark cannot reset, one exploration sees what earlier explorations did. | Exploring may act; the benchmark now plans everything before running anything, but explorations still follow one another. | ADR-0020 |
| The benchmark tool has no automated tests. | Changes to it are checked by replaying recorded plans, by hand. | PR #72 |
| The owner search on PetClinic, eShopOnWeb's quantity field: unlabelled fields cannot be named. | The vocabulary names elements by test id, role and name, or text; a field with none of them cannot be typed into. | ADR-0024, `tools/Autestiq.Benchmark/PetclinicScenarios.cs` |
| Email delivery is deferred. | Invitation and reset links go to the log; the application refuses to start that way outside development. | `docs/roadmap.md` §Inventory |
