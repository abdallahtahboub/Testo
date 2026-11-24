# Data sources: applications, benchmarks and datasets

> **Research findings, 2026-11-24.** Licences were read from each project's own `LICENSE` file on its default branch on
> that date unless marked *verify*. A licence that permits running and modifying software says nothing about using
> third-party *content* inside it, or about training on data collected from it; those are noted separately.
> **This is not legal advice**; anything moving into commercial training needs a lawyer's review.

## 1. Principles

- **Self-hostable, resettable, modifiable.** The lab runs applications it started, in isolation, and puts them back
  between scenarios. Hosted demos on the internet are out: testing someone else's server is not authorised by its
  licence.
- **Evaluation and training never share an application family** for the figures that decide anything. A family used
  for training is useless as a held-out test.
- **Public benchmarks are evaluation, not training.** Their tasks are public; training on them makes their scores
  meaningless, for us and for comparison with others.

## 2. Applications

| Application | Licence (checked) | Stack | Workflows | Realism | Fault opportunities | Reset | Recommendation |
|---|---|---|---|---|---|---|---|
| **Demo shop, clinic, invoice desk** (ours) | Ours | .NET, in-process | Shop, booking, invoicing | Small, purposeful | 21 switches exist | In-process reset | **Keep** as the lab's core, ground truth built in |
| **eShopOnWeb** ([repo](https://github.com/dotnet-architecture/eShopOnWeb)) | MIT | .NET | Catalogue, basket, sign-in, orders | Medium | Proxy, DOM; code is ours to fork | Container per run (L2) | **Keep**; already tuned on, so **training/validation only**, never held-out again |
| **Spring PetClinic** ([repo](https://github.com/spring-projects/spring-petclinic)) | Apache-2.0 | Java, Thymeleaf | Owners, pets, visits, vets | Small | Proxy, DOM, fork for switches | Container per run | **Keep as held-out** (clean measurement); never train on it |
| **RealWorld / Conduit** ([repo](https://github.com/gothinkster/realworld)) | MIT | Spec + many front/back implementations | Articles, auth, follow, comments | Medium | Swap implementations: same app, different DOMs | Container | **Add**: the same spec in several stacks is ideal for testing *generalisation across implementations* |
| **InvenTree** ([repo](https://github.com/inventree/InvenTree)) | MIT | Python/Django | Inventory, parts, stock, purchasing | High | Stock and order logic, proxy | Container + seed | **Add**: realistic business logic, permissive licence |
| **Keycloak** ([repo](https://github.com/keycloak/keycloak)) | Apache-2.0 | Java | Login, sessions, MFA, account | High (identity) | Expired sessions, lockout, redirects | Container + realm import | **Add for auth and session faults** |
| **ParaBank** ([repo](https://github.com/parasoft/parabank)) | Apache-2.0 | Java | Banking simulator: accounts, transfers, bill pay | Medium | Transfer logic, proxy | Container | **Candidate** for finance workflows |
| **Saleor** ([repo](https://github.com/saleor/saleor)) | BSD-3-Clause | Python, GraphQL, React dashboard | E-commerce + admin | High | Many | Heavier (Postgres, Redis) | **Later**: realistic but costly to host |
| **Medusa** ([repo](https://github.com/medusajs/medusa)) | MIT | Node, Postgres | E-commerce + admin | High | Many | Heavier | **Later**, as Saleor |
| **OWASP Juice Shop** ([repo](https://github.com/juice-shop/juice-shop)) | MIT | Node, Angular | Shop with deliberate vulnerabilities | Medium | Built-in flaws | Container | **Evaluation of security awareness only**; deliberately insecure, run isolated; its "bugs" are vulnerabilities, not QA-typical defects |
| **TodoMVC** ([repo](https://github.com/tastejs/todomvc)) | MIT | Many frameworks | One small app in many stacks | Low | DOM variety | Static | **Smoke tests only**; too simple to teach QA |
| **Odoo Community** ([repo](https://github.com/odoo/odoo)) | LGPL-3.0 | Python | ERP, CRM, accounting | Very high | Many | Heavy | **Later**; copyleft is fine for running, review before modifying and distributing |
| **Paperless-ngx** ([repo](https://github.com/paperless-ngx/paperless-ngx)) | GPL-3.0 | Python | Document management | Medium | Upload, tagging, search | Container | **Candidate**; GPL is fine for running internally |
| **OpenEMR** ([repo](https://github.com/openemr/openemr)) | GPL-3.0 | PHP | Healthcare records | High | Many | Heavy | **Later**; healthcare realism, but synthetic data only, ever |
| **Firefly III** ([repo](https://github.com/firefly-iii/firefly-iii)), **Twenty CRM** ([repo](https://github.com/twentyhq/twenty)) | AGPL-3.0 | PHP / TypeScript | Finance / CRM | High | Many | Container | **Candidates with care**: AGPL obligations attach to modified network services; fine to run unmodified internally, review before modifying |

**Proposed first additions (Phase 1, decision 3):** RealWorld (two implementations), InvenTree, Keycloak. Each brings a
missing dimension: same-spec generalisation, realistic business logic, and auth/session behaviour. Each gets scenarios
written from its source before any run, as PetClinic did.

## 3. Public agent benchmarks and environments

| Benchmark | Licence | What it is | Fit for Autestiq | Recommendation |
|---|---|---|---|---|
| **WebArena** ([repo](https://github.com/web-arena-x/webarena)) | Code Apache-2.0; bundled apps keep their own licences and content (e.g. GitLab, a Magento shop, a forum, a map; *verify each*) | Self-hosted sites with task goals and functional checks | Task completion, not QA verdicts; heavy images | **Evaluation only, later**; adapt a few tasks into QA scenarios with defects; never train on it |
| **BrowserGym** ([repo](https://github.com/ServiceNow/BrowserGym)) | Apache-2.0 | A gym interface over MiniWoB, WebArena, WorkArena and others | Useful harness ideas; WorkArena needs a ServiceNow instance under its own terms | **Study the interface**; do not adopt as our engine (it would be a second engine) |
| **MiniWoB++** ([repo](https://github.com/Farama-Foundation/miniwob-plusplus)) | MIT | Hundreds of tiny synthetic web tasks | Low realism; good for locator-robustness unit tests | **Optional** micro-evaluation |
| **Mind2Web** ([repo](https://github.com/OSU-NLP-Group/Mind2Web)) | Code MIT; *dataset terms: verify the dataset card*; recorded content belongs to the real websites | Offline action prediction on snapshots of real sites | Predicts clicks, not verdicts; content from third-party sites | **Evaluation of target choice only**, if the dataset terms allow; not training |

## 4. Bug and failure datasets

| Source | Licence | What it gives | Verified? | Fit | Recommendation |
|---|---|---|---|---|---|
| **SWE-bench** ([repo](https://github.com/SWE-bench/SWE-bench)) | MIT (harness); task repos keep their licences | Real GitHub issues with failing tests and fixes | Yes, by tests | Code repair, not UI QA | **Not a priority**: wrong task |
| **Defects4J** ([repo](https://github.com/rjust/defects4j)) | *verify* | Reproducible real Java bugs with tests | Yes | Unit-level, not UI | Not a priority |
| **BugsInPy** ([repo](https://github.com/soarsmu/BugsInPy)) | *verify* | Reproducible Python bugs | Yes | Unit-level | Not a priority |
| **IDoFT** ([repo](https://github.com/TestingResearchIllinois/idoft)) | *verify* | A large catalogue of flaky tests with fixes in open-source Java projects | Partly | Flakiness classification | **Candidate** for a flaky-vs-defect classifier's evaluation |
| **GitHub issues and PRs of web apps** (e.g. Playwright, Selenium, the apps above) | Per repository; issue text is user content | Real symptom reports, sometimes with repro and fix | Rarely | Realistic language; low verifiability | **Mine only for scenario ideas**, re-created as lab faults with ground truth; never use issue text as verified training data |

## 5. Acquisition process (for any source)

1. **Licence and terms check**, recorded in the source's provenance entry. Unclear means evaluation-only until cleared.
2. **Reproduction in the lab**, on a pinned version, with a reset.
3. **Ground truth set before any model run**: which verdict is correct, how it is checked.
4. **Baseline check**: a hand-written plan distinguishes the fault from the clean app.
5. **Deduplication**: by fault mechanism and affected workflow, not by text. Near-duplicates collapse to one skill
   instance.
6. **Split assignment** at application-family level, recorded in the manifest before collection.
7. **Review**: realism and the product-defect versus environment-fault judgement.

**Cost per usable example** is dominated by steps 2–4 for public sources (human time) and near zero for lab faults on
apps with switches (automatic). Estimates are in [cost-and-infrastructure.md](cost-and-infrastructure.md) §4.

## 6. Risks specific to data

- **Contamination:** a public benchmark's tasks in training. Mitigated by keeping public benchmarks evaluation-only.
- **Poisoning:** a page's text trying to steer the planner (R-9, permanent). Lab apps are ours or pinned; customer pages
  never enter training without consent and sanitisation.
- **Licence drift:** a project relicenses. Each record pins the version and licence at acquisition time.
- **Memorisation:** too few families. Report cross-family results first; grow families before growing volume.
