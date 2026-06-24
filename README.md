# AI Tokens & AI Credits

**AI Tokens & AI Credits** is an educational web app that makes two invisible things concrete:
how a large language model actually *reads* your text, and how that reading turns into *cost*.
Every model call is metered in **tokens** — the sub-word pieces a tokenizer splits text into —
and tokens are what you pay for, whether as raw API spend or as the **AI Credits** that meter
GitHub Copilot and Microsoft Copilot Studio. The app shows both halves end to end: it tokenizes
your prompt locally (no model call, no cost), runs it against a real model to surface the exact
usage, then estimates what those tokens would cost in AI Credits across Microsoft's billing
surfaces.

It runs against Azure AI Foundry (cloud) and local runtimes (Foundry Local, LM Studio, Ollama),
and for any prompt it shows:

1. **Per-token segmentation** — exactly which substring of your text is which token
   (e.g. `breakdown` &rarr; `break` + `down`), rendered as hoverable colored chips using real
   character offsets.
2. **Token usage** from the live model response, split into **prompt / reasoning / output /
   cached** (+ total).
3. **Logprob confidence** for capable models (`gpt-4o`, `gpt-4.1`): a separate output-token
   heatmap shows chosen-token probability and hover/focus alternatives, plus perplexity and
   average confidence. The markdown-rendered answer stays separate so markdown formatting is not
   broken by token spans.
4. **Why a token is a token** — hover (or keyboard-focus) any token chip for a deterministic,
   local explanation: byte breakdown, leading-space marker, vocab id, *why the split lands here*,
   and — for Foundry Local BPE models — the full greedy **merge chain** rebuilt from
   `vocab.json` + `merges.txt`. For tiktoken models (no merge file) the split is proven by
   re-encoding the neighbouring span. Tokenization is local and deterministic — **not** the model.
5. **Deep stats** after a call (tokens-only, no cost): latency, tokens/sec, chars-per-token,
   prompt:output ratio, reasoning/cache shares, unique-vs-repeated tokens, and whitespace /
   punctuation token counts.
6. **Streaming + time-to-first-token (TTFT)**: text models stream tokens live over SSE; the UI
   shows TTFT and reconciles the final frame with markdown, the logprob heatmap and deep stats.
7. **Image-generation token usage** (`gpt-image-1.5`): generate an image and see how its output
   is billed **in tokens** — input *text* tokens for the prompt plus output *image* tokens that
   scale with size/quality. There is no flat per-image fee; it is all tokens.
8. **"How tokenisation works" explainer**: a header button opens a step-by-step walkthrough
   (manual Next/Back — it does not auto-advance) that replays the **real** greedy BPE merge
   sequence for an example word, reconstructed live from the selected model's tokenizer
   (`/api/merge-trace`) and verified against its own output, then explains how tokenization
   differs across vendors/families — OpenAI GPT, Qwen and Llama 3 use byte-level BPE; Google
   Gemini/Gemma use SentencePiece/Unigram; Anthropic and Microsoft MAI tokenizers are not public.
9. **Local model discovery (Foundry Local + LM Studio + Ollama)**: models from a running
   **Foundry Local** daemon, **LM Studio** (`localhost:1234`) or **Ollama** (`localhost:11434`)
   appear in the selector automatically. Discovery fails silently when a runtime isn't running.
   See [Local models](#local-models).
10. **AI Credits (estimated)**: after a call, the same token usage is priced as **AI Credits**
    (1 credit = $0.01) for both **GitHub Copilot** and **Copilot Studio**. Pick any **billing
    model** from a configurable catalogue (Claude, GPT-5.x, Gemini, MAI) to price your prompt's
    tokens *as if it ran there* — independent of the model that generated the reply, so you can
    compare models the app can't run. Each platform has its **own overhead-tokens field**
    (GitHub Copilot and Copilot Studio bill differently and wrap prompts differently). See
    [AI Credits](#ai-credits-estimated) below.

The UI keeps the two data sources clearly separated:

| Panel | Origin | What it shows | Cost |
| --- | --- | --- | --- |
| 🖥 **Local** | computed on this machine (`Microsoft.ML.Tokenizers`) | token ids, values, offsets, local count | none |
| ☁ **Model** | the live model `usage` object | prompt / reasoning / output / cached counts + output text | tokens |

It works against **Azure AI Foundry** deployments (cloud), **Foundry Local** models
(on-device, e.g. NPU), and any **LM Studio** or **Ollama** models served on their local
OpenAI-compatible endpoints — all selectable from a dropdown.

> Teaching point the UI makes explicit: your local token count of the *raw* prompt text is
> usually **lower** than the model's billed `prompt_tokens`, because the service wraps your
> messages in a chat template and adds system/special tokens before counting.

---

## Prerequisites

- **.NET SDK 10** (`global.json` pins `10.0.301`).
- For the **cloud** path: **Azure CLI** + **Azure Developer CLI (azd)**, and an Azure
  subscription with model quota.
- For the **local** path (optional): any of **Foundry Local**, **LM Studio**, or **Ollama**
  running with at least one model available. See [Local models](#local-models).

---

## Run locally

The app is **cross-platform** (portable .NET 10, no runtime identifier pinned) and runs on
Windows, macOS and Linux:

```powershell
cd src/TokensAndCredits.Web
dotnet run
```

Open <http://localhost:5041>. Type in the prompt box — tokens update live (no model call,
no cost). Pick a model and press **Run model** to get a real completion + usage. The **Sample
prompt** dropdown loads ready-made prompts (each tagged with a complexity rating) spanning
creative, coding and scientific-research text — handy for seeing how different content tokenizes.

The app authenticates to Azure with `DefaultAzureCredential`, so sign in first for the
cloud path:

```powershell
az login
```

The cloud path is optional — you can run entirely against [local models](#local-models)
without any Azure setup.

---

## Cloud models

The Azure AI Foundry deployments are configured in
[`appsettings.json`](src/TokensAndCredits.Web/appsettings.json) (the azd template provisions
them — see [Provision Azure infra with azd](#provision-azure-infra-with-azd)). Each one is chosen
to demonstrate a *different* facet of token usage, and its capability flags drive which parts of
the UI light up:

| Deployment | Type | Why it's here / what's different | Capability flags |
| --- | --- | --- | --- |
| **gpt-4o** | Multimodal chat (flagship) | General-purpose baseline. Reports **logprobs**, so it powers the confidence heatmap, and supports **prompt caching** for the cache demo. | logprobs ✓, caching ✓ |
| **gpt-4.1** | Chat (stronger coding / instruction-following) | A second non-reasoning model so you can compare tokenization and usage against gpt-4o. Also logprob- and cache-capable. | logprobs ✓, caching ✓ |
| **o4-mini** | Reasoning model (o-series) | Reports hidden **reasoning tokens**, so the **Reasoning** usage card becomes non-N/A — showing how a model can spend its whole output budget "thinking". o-series models don't return logprobs. | reasoning ✓, caching ✓, logprobs ✗ |
| **gpt-image-1.5** | Image generation | Switches the UI into image mode and demonstrates **token-billed image output** — input *text* tokens for the prompt plus output *image* tokens that scale with size/quality (no flat per-image fee). | image modality, no caching/logprobs/reasoning |

All four use the `o200k_base` encoding for local tokenization. Trim or extend the list in
`infra/main.bicep` (deployments) and `appsettings.json` (capability flags) to match your quota —
the image feature only appears when a `gpt-image-1.5` deployment is present.

---

## Local models

Alongside Azure AI Foundry (cloud), the app discovers models from three local runtimes and lists
them in the same selector. **Discovery is best-effort and time-bounded**: each runtime is probed
on startup/refresh, and if it isn't running the app simply shows nothing for it (the cloud and
other local paths still work). No configuration is required — just start the runtime.

| Runtime | How it's reached | Default endpoint | Requirement |
| --- | --- | --- | --- |
| **Foundry Local** | `foundry` CLI + its OpenAI-compatible daemon | resolved from the CLI | `foundry` on `PATH`, model downloaded |
| **LM Studio** | OpenAI-compatible HTTP | `http://localhost:1234/v1` | local server started |
| **Ollama** | OpenAI-compatible HTTP | `http://localhost:11434/v1` | `ollama` running, model pulled |

### Foundry Local (on-device)

If the Foundry Local daemon is running, its models appear automatically under **Foundry Local
(on-device)**. The app:

- lists downloaded models (and boots the daemon if needed) via `foundry cache list --output json`,
- resolves the daemon's OpenAI-compatible endpoint via `foundry server status --output json`,
- loads a model on demand (`foundry model load <id>`) before the first call.

Download a model first; the app boots the daemon and loads the model on demand, so no manual
server start is needed:

```powershell
foundry model download qwen2.5-coder-1.5b   # download a model
foundry cache list                           # verify it's downloaded (also boots the daemon)
```

### LM Studio

Models served by LM Studio's OpenAI-compatible server (`http://localhost:1234/v1`) are discovered
from its `/v1/models` list. **Start the server first** — load a model in the LM Studio UI and
enable the local server, or run:

```powershell
lms server start
```

If no models appear, the server isn't running (loading a model in the UI alone is not enough).

### Ollama

Models pulled into Ollama are discovered from its OpenAI-compatible endpoint
(`http://localhost:11434/v1/models`). Pull a model, then make sure Ollama is running:

```powershell
ollama pull qwen3:4b
ollama serve            # usually already running as a background service
```

> **Local usage + tokenization caveats.** Local runtimes typically report only
> `prompt`/`output`/`total` tokens, so **reasoning** and **cached** show as **N/A** (they are
> Azure features). For tokenization, any **Qwen** model is tokenized exactly with the bundled Qwen
> BPE; other local models have no exact local tokenizer, so the count falls back to `o200k_base`
> and is flagged **approximate** until the model's own usage comes back after you run it.

---

## Provision Azure infra with azd

The template is **infra-only** — it provisions Azure AI Foundry + model deployments + keyless
RBAC, but does **not** deploy the web app (you run it locally).

```powershell
azd auth login
azd up        # prompts for environment name + location, then provisions
```

`azd up` creates (100% identity-based — no keys anywhere):

- an **AI Services (Foundry)** account with **local auth disabled** (Entra-only) and a
  system-assigned identity,
- a **Foundry project** under the account (portal experience: playground, agents, evals),
- the model **deployments** (default: `gpt-4o`, `gpt-4.1`, `o4-mini` — trim for quota; add
  `gpt-image-1.5` to enable the image-generation feature),
- role assignments for your signed-in identity, granted at account scope (they **inherit**
  to the project and the model deployments):
  - **Cognitive Services OpenAI User** — keyless model inference,
  - **Azure AI Developer** — Foundry project + model use,
  - **Cognitive Services Contributor** — view/manage account, project, deployments.

> **Re-provisioning an existing account:** enabling project management + the system-assigned
> identity on an account that was first created without them can be rejected as an in-place
> update. If `azd provision` errors on the account, run `azd down` then `azd up` to recreate.

Tear everything down with:

```powershell
azd down
```

### Wire the outputs into the app — automatic

The azd **postprovision hook** (`infra/hooks/postprovision.ps1`) writes the provisioned
endpoint into git-ignored `appsettings.Development.json` for you. The deployment names and
capability flags are already in `appsettings.json`. So the flow is just:

```powershell
azd auth login    # (or: az login) — for keyless DefaultAzureCredential
azd up            # provision + auto-write the endpoint
cd src/TokensAndCredits.Web
dotnet run
```

> **Heads-up — things that can still block a first run:**
> - **Quota:** the region you pick must have capacity for `gpt-4o`, `gpt-4.1` and `o4-mini`
>   (`GlobalStandard`). If not, `azd up` fails on a deployment — trim `deployments` in
>   `infra/main.bicep` or choose another region.
> - **RBAC propagation:** the role assignment can take a few minutes to take effect; an early
>   call may return 401/403 until it does.
> - **Sign-in:** `DefaultAzureCredential` needs a local Azure login (`az login` or
>   `azd auth login`).

If you'd rather configure it by hand, put this in
`src/TokensAndCredits.Web/appsettings.Development.json`:

```json
{
  "AzureFoundry": {
    "Endpoint": "https://<your-account>.openai.azure.com/"
  }
}
```

Capability flags drive the UI: `SupportsReasoning` shows the reasoning usage card,
`SupportsCaching` enables the **Cache demo** button and the cached usage card, and
`SupportsLogprobs` gates logprob requests and the confidence heatmap. `Modality` (`Text` or
`Image`) switches the UI into image-generation mode for `gpt-image-1.5`. They live in
`appsettings.json`; edit them there if you change which models you deploy.

> **Image generation** needs a `gpt-image-1.5` deployment (token-billed, `GlobalStandard`).
> Pick a region with `gpt-image-1.5` quota; the app talks to it via the same keyless Azure
> OpenAI endpoint and reads the response `usage` to break the cost into input *text* tokens and
> output *image* tokens.

---

## How it works

```
Browser (wwwroot) ──► /api/tokenize        (local only: tokenizer → ids + offsets)
                 ──► /api/explain-token    (local only: byte breakdown + merge chain / split proof)
                 ──► /api/merge-trace      (local only: real greedy BPE merge sequence for one word)
                 ──► /api/analyze          (local tokenization + live model call)
                 ──► /api/analyze-stream   (SSE: live token deltas + TTFT, then final usage/logprobs)
                 ──► /api/generate-image   (gpt-image-1.5: image output + token-usage breakdown)
                 ──► /api/cache-demo       (two identical ≥1,024-token prompts)
                 ──► /api/credit-rates     (local only: AI-credit rate catalogue for the selector)
                 ──► /api/models           (Azure deployments + Foundry Local models)
```

- **Tokenizers** (`Services/Tokenize`): tiktoken (`o200k_base`/`cl100k_base`) for OpenAI
  families. For any **Qwen** model (Foundry Local, LM Studio or Ollama) it uses a **bundled Qwen
  byte-level BPE** (`Resources/qwen`, shared by Qwen2/2.5/3) so the count is exact with no model
  call; Foundry Local also uses a model's own cached `vocab.json`/`merges.txt` when present. Any
  other local model has no exact local tokenizer, so it falls back to `o200k_base` flagged
  clearly as approximate (the exact count then comes from the model's own usage after you run it).
  `EncodeToTokens` gives each token's id, value and character offset; `TokenExplainer` reconstructs
  the greedy BPE merge chain (Qwen) or proves a split by re-encoding (tiktoken) for the "why" popover.
- **Chat** (`Services/Chat`): a single `Microsoft.Extensions.AI.IChatClient` for both backends
  (Azure OpenAI keyless, or the local OpenAI-compatible endpoint), so usage is reported
  uniformly via `UsageDetails` (`InputTokenCount`, `OutputTokenCount`, `CachedInputTokenCount`,
  `ReasoningTokenCount`, `TotalTokenCount`). For logprob-capable models, provider-specific
  OpenAI chat options are attached through `ChatOptions.RawRepresentationFactory` and read back
  from the raw `ChatCompletion`. A streaming path (`GetStreamingResponseAsync`) measures TTFT
  and streams deltas over SSE.
- **Image** (`Services/Image`): an OpenAI `ImageClient` (keyless, same Azure endpoint) calls
  `gpt-image-1.5`; the response `usage` (`input_tokens` / `output_tokens` /
  `input_tokens_details`) is surfaced so the UI can explain token-billed image output.
- **Cache demo**: prompt caching needs a shared prefix of ≥1,024 identical tokens; the second
  of two identical calls reports `cached_tokens`.
- **Credits** (`Services/Credits`): `GET /api/credit-rates` serves a configurable rate
  catalogue; the live estimate is computed in the browser (`computeCredits` in `app.js`) so
  changing the billing model recomputes instantly with no re-run. `CreditEstimator` mirrors the
  same maths in C# for unit tests.

---

## AI Credits (estimated)

After a model call, the same token usage is converted into **AI Credits** (1 credit = $0.01)
for two Microsoft billing surfaces. The estimate is driven by a **user-selected billing model**,
*not* the model that produced the reply — so you can price your prompt's tokens against a model
the app can't even run (e.g. Claude Opus 4.8 or GPT-5.5). Changing the selector recomputes
instantly from the last run's usage.

The two surfaces are shown as **separate sections** because they are billed differently, and
each has its **own overhead-tokens field**:

- **GitHub Copilot** — per-model, four token classes: **input / cache-read / cache-write /
  output** (reasoning is billed at the output rate). Cache **reads** are discounted and cache
  **writes** cost a premium, but Azure OpenAI and local runtimes report only cache *reads*, so
  cache-write shows **0** here. Its overhead field adds **input** tokens (system prompt, tool
  definitions, custom instructions, retrieved context).
- **Copilot Studio** — "Text and generative AI tools" billed **per 1,000 tokens** at three tiers
  (**Basic 0.1 / Standard 1.5 / Premium 10** credits/1k). The tier is set by the model the AI
  tool uses, so all three are shown. Its overhead field adds to the **total** tokens metered (the
  agent's own system prompt, instructions and knowledge grounding). The estimate still ignores
  per-message / agent-action charges and retries, so real usage is typically **higher**; for
  Microsoft 365 Copilot–licensed employee use, Copilot Studio token costs are **included**.

Token-class mapping from the model's usage: `input = prompt − cached`, `cache-read = cached`,
`cache-write = 0`, `output = output + reasoning`.

Rates are list prices that change, so they live in the **`Credits`** section of
`appsettings.json` with an `AsOf` label and are fully editable:

```jsonc
"Credits": {
  "AsOf": "June 2026",
  "CopilotStudio": { "Basic": 0.1, "Standard": 1.5, "Premium": 10 },
  "GitHub": {
    "DefaultId": "gpt-5.4",
    "Models": [
      { "Id": "gpt-5.4", "Label": "GPT-5.4",
        "InputPerMillion": 250, "CacheReadPerMillion": 25,
        "CacheWritePerMillion": 0, "OutputPerMillion": 1500 }
      // … Claude, GPT-5.x, Gemini, MAI
    ]
  }
}
```

**AI Credits documentation (rates as of June 2026):**

- **GitHub Copilot** — [About billing for GitHub Copilot](https://docs.github.com/en/copilot/concepts/billing/about-billing-for-github-copilot)
  and the per-model [Models and pricing](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing)
  (input / cached / cache-write / output AI Credit rates).
- **Microsoft Copilot Studio** — [Copilot Credits billing rates](https://learn.microsoft.com/microsoft-copilot-studio/requirements-messages-management#copilot-credits-billing-rates)
  (per-1,000-token Basic / Standard / Premium tiers and non-token charges).

---

## Security notes

- Keyless throughout (`DefaultAzureCredential`); the Foundry account has local auth disabled.
- No secrets in the repo; Azure config lives in git-ignored `appsettings.Development.json`.
- Same-origin only, restrictive CSP + `X-Content-Type-Options` / `X-Frame-Options`.
- Token text is rendered via DOM `textContent` (never `innerHTML`), so echoed input can't
  inject markup. Prompt length is capped server-side.

---

## Project layout

```
azure.yaml                      azd config (infra-only)
infra/                          Bicep: AI Foundry account + deployments + RBAC
src/TokensAndCredits.Web/
  Program.cs                    DI, endpoints, security headers, config validation
  Api/                          minimal API endpoints + DTOs + security headers
  Services/Models/              ModelDescriptor, UsageBreakdown, TokenInfo, TokenLogprob, …
  Services/Tokenize/            tokenizer resolution + offset analysis + TokenExplainer
  Services/Catalog/             Azure + Foundry Local + LM Studio + Ollama discovery
  Services/Chat/                unified IChatClient + usage extraction + streaming
  Services/Image/               gpt-image-1.5 generation + image token-usage extraction
  Services/Credits/             AI-credit rate options + CreditEstimator (parity tests)
  Services/CacheDemo/           prompt-cache demonstration
  wwwroot/                      single-page UI (index.html, app.js, styles.css)
tests/TokensAndCredits.Web.Tests/   xUnit tests (tokenizer/merge trace, Qwen, streaming usage,
                                    token explainer, credit estimator + rate catalogue)
tests/js/credits.test.mjs           node test for the client-side credit maths
```

---

## License

Released under the [MIT License](LICENSE) © 2026 Gary Lumsden.

The bundled Qwen tokenizer data in `src/TokensAndCredits.Web/Resources/qwen`
(`vocab.json`, `merges.txt`, `tokenizer_config.json`) is from
[Qwen/Qwen3-0.6B](https://huggingface.co/Qwen/Qwen3-0.6B) and is licensed under
Apache-2.0; it is redistributed here for exact local tokenization of Qwen models.
