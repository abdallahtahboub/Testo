# Cost and infrastructure

> **Prices are research findings captured on 2026-11-24 from the sources listed in §6, which disagree with one another
> and change often.** Everything derived from them is an **estimate**, with its assumptions stated. Nothing here has
> been bought or provisioned. **No paid resource starts without the owner's explicit approval.**

## 1. Model API (what we pay today)

- Price book (ADR-0029, seeded 2026-11-16): **Claude Sonnet 5.5, $2.00 per million input tokens, $10.00 per million
  output tokens**.
- **Measured** cost per plan in the benchmark: **$0.024–$0.053** (unseen sets, eShopOnWeb, PetClinic; ADR-0020).
  Retrieval would add input tokens: each retrieved example of ~1,500 tokens costs ~$0.003 per plan (estimate).

## 2. GPU rental (on-demand, per GPU-hour, USD)

| GPU | VRAM | Runpod | Lambda | Vast.ai (marketplace) |
|---|---|---|---|---|
| RTX 4090 | 24 GB | $0.89 Secure, $0.34 Community | — | from $0.27, median ~$0.50 |
| RTX 5090 | 32 GB | $1.39 Secure (raised 13 Nov 2026, per one source) | — | from $0.37, median ~$0.63 |
| A100 | 40/80 GB | $2.09 PCIe, $2.19 SXM | ~$1.99 (40 GB), ~$2.79 (80 GB) | varies |
| H100 | 80 GB | $2.89 PCIe, $4.39 SXM | ~$3.99–4.29 SXM | from $1.73, median ~$1.93 |

Marketplace prices are the cheapest or median offers from individual hosts: cheaper, less reliable, and unsuitable for
anything holding customer data. For lab-only research on public or synthetic data they are acceptable.

## 3. Hardware purchase

| GPU | Price, November 2026 | Note |
|---|---|---|
| RTX 5090 | List $1,999; **street ~$6,000–7,000** in the US (reports of shortage; one EU report above €5,000) | Volatile |
| H100 80 GB | **~$27,000–40,000** new; less on the secondary market | Plus a server, power, cooling |

**Rent or buy (estimate).** An RTX 5090 at $6,500 against a $0.63/hour median rental breaks even after ~10,000 GPU-hours,
over a year of round-the-clock use, before power (~$0.17/hour at 575 W and $0.30/kWh, an assumption). An H100 at $30,000
against $1.93/hour needs ~15,500 hours, nearly two years non-stop, before the server and its running costs. **Buying is
only economical at sustained high utilisation over more than a year. Nothing in Phases 0–4 comes close: rent.**

## 4. Labour and verification (estimates)

| Item | Assumption | Estimate |
|---|---|---|
| Human verification of a public-source example (reproduce, set ground truth, check baseline) | 3–10 minutes at $40–80/hour | **$2–13 per example**; 10,000 examples ≈ $20,000–130,000 |
| Lab-fault example on an app with a switch | Automatic verification | Near zero marginal; the model call (~$0.03–0.05) and machine time |
| Writing scenarios for a new application from its source | As PetClinic: 11 scenarios, about half a day | Engineering time, not money |
| **Engineering of the lab and experiments** | Weeks of work per phase | **The largest cost by far**; stated in engineer-weeks in [roadmap.md](roadmap.md) |

## 5. Scenarios by budget

| Budget | What it buys (estimate) | Recommended use |
|---|---|---|
| **$500** | Experiment 0001 (~$25–40, two passes); its repeats and a second app (~$50); the re-measurements; a first QLoRA trial on a rented 24–32 GB GPU if justified (a 7–8B model on ~30M training tokens ≈ 2–10 GPU-hours ≈ $5–15, plus evaluation runs) | **Phase 0–2.** Enough to learn whether retrieval helps |
| **$2,000** | Several hundred to a few thousand lab experiences across 5–6 apps; repeated evaluations; small human-verified sets (~100 examples) | Phase 2–3, after a positive Experiment 0001 |
| **$5,000** | A controlled pilot of a specialist (failure classifier) with serving for evaluation; ~300 human-verified examples | Phase 4, gated |
| **$10,000–25,000** | Larger human-verified sets (1,000–3,000), multi-family lab, several fine-tuning rounds, continuous serving of one small model (~$250–1,000/month on one rented GPU) | Phase 4–5, only on evidence |
| **More** | RL research, larger models | Only with results that justify it |

What the budgets **exclude**: engineering salaries, legal review of licences and data terms, hosting the product itself
(waits on the hosting decision), and object storage.

## 6. Sources (retrieved 2026-11-24)

- Runpod: [product pricing page](https://www.runpod.io/product/cloud-gpus), [Thunder Compute comparison (checked 19 Nov 2026, notes the 13 Nov increase)](https://www.thundercompute.com/blog/runpod-pricing-vs-thunder-compute), [getdeploying](https://getdeploying.com/runpod)
- Lambda: [instances](https://lambda.ai/instances), [pricing](https://www.lambda.ai/pricing), [Spheron (Lambda H100, 2026)](https://www.spheron.network/blog/lambda-cloud-h100-pricing-2026/)
- Vast.ai: [live pricing guide](https://vast.ai/article/how-much-does-it-cost-to-rent-a-gpu-in-the-cloud-live-pricing-guide), [Markaicode snapshot 16 Nov 2026](https://markaicode.com/pricing/vast-ai-pricing/), [Beam on RTX 5090](https://www.beam.cloud/blog/nvidia-rtx-5090-pricing)
- RTX 5090 street price: [Tech Insider, Nov 2026](https://tech-insider.org/nvidia-rtx-50-prices-rise-november-2026-rtx-5090-3340/), [GPU Poet, Nov 2026](https://gpupoet.com/gpu/learn/price/november-2026/nvidia-geforce-rtx-5090), [Guru3D (EU)](https://www.guru3d.com/story/geforce-rtx-5090-street-prices-in-eu-now-exceed/)
- H100 purchase: [IntuitionLabs data-center GPU prices](https://intuitionlabs.ai/articles/data-center-gpu-prices), [Jarvislabs H100 price guide](https://jarvislabs.ai/blog/h100-price), [CloudZero](https://cloudzero.com/blog/h100-gpu-cost/)
- Model licences overview (secondary; check each model card): [IntuitionLabs on open-weight licences](https://intuitionlabs.ai/articles/open-weight-ai-model-licenses), [WZ-IT on commercial use](https://wz-it.com/en/knowledge/ki/open-llm-licenses-commercial-use/)
