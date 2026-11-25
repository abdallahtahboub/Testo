# Learning approaches and model strategy

> **Assessment and proposal, 2026-11-24.** Each approach is judged separately, against Autestiq's actual problem:
> writing a correct, checkable plan from a description of an application, within the boundary of ADR-0019.

## Stage A: learning through experience retrieval

**What:** before planning, find verified prior experiences similar to the current task (same kind of workflow, same kind
of failure, same application family) and put a few of them in the request, labelled as prior verified examples.

**Why first:**
- No training, no hosting, no new model. It changes what the planner is told, and it can be switched off.
- It is measurable with the harness as it is: same scenarios, with and without retrieval, against ground truth.
- It works with any foundation model, including a better one next year.

**Design constraints:**
- **Retrieval is data, not instruction.** Retrieved examples go in the request's data section, like the application's
  content, and the validator treats the resulting plan exactly as any other. A retrieved example can never widen what a
  plan may do.
- **Tenant scope.** A tenant's own experience can inform only that tenant's plans. Shared experience comes only from the
  lab and from consented sources ([security-and-governance.md](security-and-governance.md)).
- **No held-out leakage.** Tier 5 records are never retrievable ([data-architecture.md](data-architecture.md)).
- **Similarity first by structure, then text:** failure class, action kinds, workflow type. Embeddings come later, only
  if structured matching proves too coarse.

**What it will not do:** fix observation gaps. A state the explorer never reached is not in the snapshot, whatever is
retrieved. It may, though, help with planning patterns the model gets wrong repeatedly, such as how to verify a search,
what a confirmation usually looks like, or how to plan around an unlabelled field.

## Stage B: supervised fine-tuning

**What:** adapt an existing open-weight model to a narrow task with LoRA/QLoRA on verified examples.

**Candidate tasks, in order of fit:**

| Task | Why it fits | Data needed |
|---|---|---|
| **Failure classification** (product defect / environment fault / plan error / inconclusive) from a failed run's evidence | Narrow output, frequent, cheaply checkable, high cost if done by a large model at scale | Hundreds to low thousands of labelled runs; the lab produces them automatically with fault kinds |
| **Target choice** among the explorer's candidates for a described element | Constrained output, checkable against the browser | Lab snapshots with known correct targets |
| **Plan repair proposal** from a failed run | Valuable, but open-ended | Thousands of verified repairs; later |
| Full planning | Open-ended, where frontier models lead by far | Not recommended |

**Tooling (all Apache-2.0, checked 2026-11-24):** PyTorch, Hugging Face Transformers, TRL, PEFT, Unsloth, vLLM for
serving, MLflow for tracking. **Python is a non-.NET tool and needs the owner's approval** (CLAUDE.md); it would live
in a separate research workspace, not in the product.

**Base models:** prefer **Apache-2.0** open-weight families for fine-tuning and commercial serving (*research finding*:
many Qwen 3.x and Mistral releases; licences vary by checkpoint, so each model card must be checked). Llama and Gemma
releases carry community or custom terms with usage thresholds, acceptable-use policies, and for some Llama versions
regional exclusions; they need legal review before use. Sizes: start at **1–8B parameters**, which fit QLoRA on one
24–32 GB GPU.

**When it is justified:** only after Stage A has shown which experience helps, and when a task is frequent enough that
a cheaper specialist matters. The first fine-tune is an **experiment with a pre-registered comparison**, not a product
change.

## Stage C: reinforcement learning

**What it would need:** resettable environments (we have them for our apps), trajectories, and rewards from verified
outcomes.

**Assessment:**
- **Reward hacking is the dominant risk.** The obvious reward ("the plan passed") teaches weak checks. A usable reward
  must penalise false positives heavily, reward detection on broken variants, and treat inconclusive as zero, exactly
  the benchmark's asymmetry. Even then, an agent can learn the lab's apps.
- **Cost and expertise** are an order of magnitude above Stage B (many rollouts, each a browser run), and training is
  unstable.
- **Not needed in the near term.** Revisit only when a fine-tuned specialist exists, the lab has many families, and a
  clear task has plateaued under Stage A and B.

## Stage D: training from scratch

**Assessment:** a useful language model from random initialisation needs on the order of hundreds of billions to
trillions of training tokens, a research team, and compute budgets in the millions for anything competitive with open
models that can be downloaded for free. Autestiq's advantage is verified QA experience and the infrastructure around
it, which an existing model can be specialised on. **Not recommended**, and ADR-0036 proposes recording that decision
so it is not re-litigated without new evidence.

## Model strategy

| Option | Quality | Cost | Privacy | Dependency | Verdict |
|---|---|---|---|---|---|
| 1. External foundation model only (today) | Highest for planning | Per call, ~$0.02–0.05 per plan (measured) | Data leaves to the provider, under the project's AI permission | High | **Keep for planning** |
| 2. + experience retrieval | Possibly higher | Slightly more tokens | Same; retrieval stays tenant-scoped | Same | **Next step, if Experiment 0001 shows a gain** |
| 3. + local fine-tuned specialists | Same planning; cheaper narrow tasks | Hosting a small model | Better for those tasks | Lower for those tasks | **Later**, per task, evidence-gated |
| 4. Entirely self-hosted open models | Lower for planning today | Hosting, operations | Best | Lowest | **Not now**; revisit for enterprise private AI (G-12) |
| 5. Customer-specific private deployments | Depends | High | Best | Varies | **With the private agent (G-8) and private AI (G-12)**, much later |

**Division of labour, the target shape:** a frontier model for planning; small specialists for classification and
target choice where measured as good enough; deterministic software for execution, permissions, locators and verdicts;
independent verification (ground truth, baselines, people) for anything that decides a number. **Not every task needs
the most expensive model, and no model decides what software must enforce.**
