"use strict";

const state = {
    models: new Map(),
    creditRates: null,
    billingModelId: null,
    lastUsage: null,
};

const el = (id) => document.getElementById(id);

function debounce(fn, ms) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), ms);
    };
}

function setStatus(message, isError = false) {
    const node = el("status");
    node.textContent = message || "";
    node.classList.toggle("error", isError);
}

// Make whitespace visible without losing readability. Token text is always set via
// textContent (never innerHTML), so user/model content can't inject markup.
function visibleText(value) {
    return value.replace(/ /g, "\u00B7").replace(/\n/g, "\u23CE").replace(/\t/g, "\u21E5");
}

// Set text on an element, rendering **double-asterisk** spans as bold via real <strong> nodes
// (no innerHTML). Used for UI copy that needs emphasis without ALL-CAPS.
function setRichText(element, text) {
    element.replaceChildren();
    const parts = String(text).split(/\*\*([^*]+)\*\*/);
    parts.forEach((part, i) => {
        if (part === "") {
            return;
        }
        if (i % 2 === 1) {
            const strong = document.createElement("strong");
            strong.textContent = part;
            element.appendChild(strong);
        } else {
            element.appendChild(document.createTextNode(part));
        }
    });
}

const ENCODING_DEFINITION =
    "An encoding is the fixed dictionary of tokens plus the merge rules a model uses to split text. Same text + same encoding always gives the same tokens.";
const APPROXIMATE_ENCODING_NOTE =
    "This model's own tokenizer isn't available locally, so the local breakdown uses OpenAI's o200k_base as a stand-in and may differ from the model. The exact token count is shown after you run the model.";

function createHelpBadge(label, help) {
    const helpNode = document.createElement("span");
    helpNode.className = "help";
    helpNode.textContent = "?";
    helpNode.title = help;
    helpNode.tabIndex = 0;
    helpNode.setAttribute("role", "img");
    helpNode.setAttribute("aria-label", `${label}: ${help}`);
    return helpNode;
}

function normalizedEncodingLabel(encoding) {
    return String(encoding || "unknown").replace(/\s*\(approx\)\s*$/i, "");
}

function isApproximateEncoding(encoding, exact = true) {
    return !exact || /\(approx\)\s*$/i.test(String(encoding || ""));
}

function encodingDescription(encoding) {
    const label = normalizedEncodingLabel(encoding);
    const lower = label.toLowerCase();

    if (lower === "o200k_base") {
        return "OpenAI's ~200k-token vocabulary used by GPT-4o and GPT-4.1.";
    }

    if (lower === "cl100k_base") {
        return "OpenAI's ~100k-token vocabulary used by GPT-3.5 and the original GPT-4.";
    }

    if (lower.includes("qwen")) {
        return "Qwen's own byte-level BPE (shared by Qwen2, Qwen2.5 and Qwen3), bundled with this app so the count is exact without a model call.";
    }

    if (lower.includes("model files")) {
        return "Loaded from this model's own tokenizer files, so the count is exact.";
    }

    return "The token dictionary + merge rules this model uses to split text.";
}

function encodingHelpText(encoding, exact = true) {
    const parts = [ENCODING_DEFINITION, encodingDescription(encoding)];

    if (isApproximateEncoding(encoding, exact)) {
        parts.push(APPROXIMATE_ENCODING_NOTE);
    }

    return parts.join(" ");
}

function renderEncodingLine(encoding, exact = true) {
    const node = el("localEncoding");
    const label = normalizedEncodingLabel(encoding);
    const approximate = isApproximateEncoding(encoding, exact);
    node.classList.toggle("encoding-warn", approximate);

    if (approximate) {
        const warn = document.createElement("strong");
        warn.textContent = "\u26A0 Approximate \u2014 ";
        const rest = document.createTextNode(
            `not this model's tokenizer. The local count uses OpenAI's ${label} as a stand-in; the exact count is shown after you run the model.`);
        node.replaceChildren(warn, rest, document.createTextNode(" "),
            createHelpBadge("encoding", encodingHelpText(encoding, exact)));
        return;
    }

    const text = `Encoding: ${label} \u00B7 exact for this model`;
    node.replaceChildren(
        document.createTextNode(text),
        document.createTextNode(" "),
        createHelpBadge(text, encodingHelpText(encoding, exact)),
    );
}

function renderTokens(container, tokens, context = {}) {
    container.replaceChildren();
    tokens.forEach((token) => {
        const span = document.createElement("span");
        span.className = `tok tok-c${token.index % 8}`;
        span.tabIndex = 0;
        span.setAttribute("aria-label", `Explain token ${token.index}, vocab id ${token.id}`);
        span.appendChild(document.createTextNode(visibleText(token.value)));

        const popover = document.createElement("span");
        popover.className = "token-popover";
        popover.id = `tok-popover-${Math.random().toString(36).slice(2)}`;
        popover.setAttribute("role", "tooltip");
        span.setAttribute("aria-describedby", popover.id);
        renderBasicTokenPopover(popover, token);
        span.appendChild(popover);

        let explanationPromise = null;
        const loadExplanation = () => {
            if (!context.modelId || typeof context.text !== "string") {
                return;
            }
            explanationPromise ??= postJson("/api/explain-token", {
                modelId: context.modelId,
                text: context.text,
                tokenIndex: token.index,
            })
                .then((explanation) => renderTokenPopover(popover, explanation))
                .catch((err) => renderTokenPopoverError(popover, token, err));
        };

        span.addEventListener("mouseenter", loadExplanation);
        span.addEventListener("focus", loadExplanation);
        container.appendChild(span);
    });
}

function renderBasicTokenPopover(popover, token) {
    popover.replaceChildren();
    appendPopoverHeader(popover, `Token #${token.index}`);
    appendKeyValue(popover, "Text", visibleText(token.value));
    appendKeyValue(popover, "Vocab id", String(token.id));
    appendKeyValue(popover, "Chars", `${token.start}\u2013${token.end}`);
}

function renderTokenPopoverError(popover, token, err) {
    popover.replaceChildren();
    appendPopoverHeader(popover, `Token #${token.index}`);
    appendPopoverNote(popover, `Explanation failed: ${err.message}`);
}

function renderTokenPopover(popover, explanation) {
    popover.replaceChildren();
    appendPopoverHeader(popover, `Token #${explanation.index}`);
    appendKeyValue(popover, "Text", visibleText(explanation.value));
    appendKeyValue(popover, "Vocab id / rank", String(explanation.id));
    appendKeyValue(
        popover,
        "Encoding",
        `${normalizedEncodingLabel(explanation.encoding)}${isApproximateEncoding(explanation.encoding, explanation.exact) ? " (approx)" : ""}`,
        encodingHelpText(explanation.encoding, explanation.exact),
    );
    appendKeyValue(popover, "Chars", `${explanation.start}\u2013${explanation.end}`);
    appendByteBreakdown(popover, explanation.bytes || []);
    appendPopoverNote(
        popover,
        explanation.leadingSpace
            ? "Leading-space token: space is part of this token (byte-level marker Ġ), so dog and ·dog differ."
            : "No leading-space marker in this token.",
    );
    appendSectionText(popover, "Why it ends here", explanation.why);

    if (explanation.mergeSteps && explanation.mergeSteps.length > 0) {
        appendMergeSteps(popover, explanation.mergeSteps);
    } else if (explanation.splitProofs && explanation.splitProofs.length > 0) {
        appendSplitProofs(popover, explanation.splitProofs);
    } else {
        appendPopoverNote(popover, "No merge chain available for this encoding.");
    }
}

function appendPopoverHeader(parent, text) {
    const header = document.createElement("strong");
    header.className = "token-popover-title";
    header.textContent = text;
    parent.appendChild(header);
}

function appendKeyValue(parent, key, value, help = null) {
    const row = document.createElement("span");
    row.className = "token-popover-row";
    const label = document.createElement("span");
    label.className = "token-popover-label";
    label.textContent = key;
    const val = document.createElement("span");
    val.textContent = value;
    if (help) {
        val.append(" ", createHelpBadge(`${key} ${value}`, help));
    }
    row.append(label, val);
    parent.appendChild(row);
}

function appendPopoverNote(parent, text) {
    const note = document.createElement("span");
    note.className = "token-popover-note";
    note.textContent = text;
    parent.appendChild(note);
}

function appendSectionText(parent, title, text) {
    const section = document.createElement("span");
    section.className = "token-popover-section";
    const heading = document.createElement("span");
    heading.className = "token-popover-label";
    heading.textContent = title;
    const body = document.createElement("span");
    body.textContent = text;
    section.append(heading, body);
    parent.appendChild(section);
}

function appendByteBreakdown(parent, bytes) {
    const section = document.createElement("span");
    section.className = "token-popover-section";
    const heading = document.createElement("span");
    heading.className = "token-popover-label";
    heading.textContent = "Bytes";
    const list = document.createElement("span");
    list.className = "token-byte-list";
    bytes.forEach((b) => {
        const item = document.createElement("span");
        item.className = "token-byte";
        item.textContent = `${b.hex} ${b.utf8} \u2192 ${b.byteLevel}`;
        list.appendChild(item);
    });
    if (bytes.length === 0) {
        list.textContent = "(no bytes)";
    }
    section.append(heading, list);
    parent.appendChild(section);
}

function appendMergeSteps(parent, steps) {
    const section = document.createElement("span");
    section.className = "token-popover-section";
    const heading = document.createElement("span");
    heading.className = "token-popover-label";
    heading.textContent = "Merge chain";
    const list = document.createElement("ol");
    list.className = "token-merge-list";
    steps.slice(0, 12).forEach((step) => {
        const item = document.createElement("li");
        item.textContent = `rank ${step.rank}: ${step.left} + ${step.right} \u2192 ${step.result}`;
        list.appendChild(item);
    });
    if (steps.length > 12) {
        const item = document.createElement("li");
        item.textContent = `${steps.length - 12} more merge step(s)`;
        list.appendChild(item);
    }
    section.append(heading, list);
    parent.appendChild(section);
}

function appendSplitProofs(parent, proofs) {
    const section = document.createElement("span");
    section.className = "token-popover-section";
    const heading = document.createElement("span");
    heading.className = "token-popover-label";
    heading.textContent = "Split proof";
    const list = document.createElement("ul");
    list.className = "token-proof-list";
    proofs.forEach((proof) => {
        const item = document.createElement("li");
        item.textContent = `${proof.direction}: ${visibleText(proof.extendedText)} \u2192 ids ${proof.tokenIds.join(", ")}. ${proof.explanation}`;
        list.appendChild(item);
    });
    section.append(heading, list);
    parent.appendChild(section);
}

// Render a (trusted-but-treated-as-untrusted) markdown string into `container`.
// Every piece of text is set via textContent and elements are created with
// createElement, so model content can never inject markup (no innerHTML).
function renderMarkdown(container, text) {
    container.replaceChildren();
    container.classList.add("markdown");

    const lines = String(text).replace(/\r\n/g, "\n").split("\n");
    let i = 0;
    let paragraph = [];

    const flushParagraph = () => {
        if (paragraph.length === 0) {
            return;
        }
        const p = document.createElement("p");
        appendInline(p, paragraph.join(" "));
        container.appendChild(p);
        paragraph = [];
    };

    while (i < lines.length) {
        const line = lines[i];

        // Fenced code block: ``` ... ```
        const fence = line.match(/^\s*```(.*)$/);
        if (fence) {
            flushParagraph();
            const code = [];
            i += 1;
            while (i < lines.length && !/^\s*```\s*$/.test(lines[i])) {
                code.push(lines[i]);
                i += 1;
            }
            i += 1; // skip closing fence
            const pre = document.createElement("pre");
            const codeEl = document.createElement("code");
            codeEl.textContent = code.join("\n");
            pre.appendChild(codeEl);
            container.appendChild(pre);
            continue;
        }

        // Heading: # .. ######
        const heading = line.match(/^(#{1,6})\s+(.*)$/);
        if (heading) {
            flushParagraph();
            const level = heading[1].length;
            const h = document.createElement(`h${level}`);
            appendInline(h, heading[2].trim());
            container.appendChild(h);
            i += 1;
            continue;
        }

        // List (unordered: -, *, +  |  ordered: 1.), with indent-based nesting.
        if (/^\s*([-*+]|\d+\.)\s+/.test(line)) {
            flushParagraph();
            i = consumeList(lines, i, container);
            continue;
        }

        // Blank line => paragraph break
        if (line.trim() === "") {
            flushParagraph();
            i += 1;
            continue;
        }

        paragraph.push(line.trim());
        i += 1;
    }

    flushParagraph();
}

// Consume consecutive list lines starting at `start`, building nested <ul>/<ol>
// based on indentation, and append the resulting list to `container`.
// Returns the index of the first line that is not part of the list.
function consumeList(lines, start, container) {
    const itemRe = /^(\s*)([-*+]|\d+\.)\s+(.*)$/;
    const stack = []; // { indent, el }
    let i = start;

    while (i < lines.length) {
        const m = lines[i].match(itemRe);
        if (!m) {
            break;
        }
        const indent = m[1].length;
        const ordered = /\d+\./.test(m[2]);

        while (stack.length > 1 && indent < stack[stack.length - 1].indent) {
            stack.pop();
        }

        let cur = stack[stack.length - 1];
        if (!cur || indent > cur.indent) {
            const list = document.createElement(ordered ? "ol" : "ul");
            if (cur) {
                const lastLi = cur.el.lastElementChild;
                (lastLi || cur.el).appendChild(list);
            } else {
                container.appendChild(list);
            }
            cur = { indent, el: list };
            stack.push(cur);
        }

        const li = document.createElement("li");
        appendInline(li, m[3]);
        cur.el.appendChild(li);
        i += 1;
    }

    return i;
}

// Parse inline markdown (`code`, **bold**, *italic*) into `parent` as DOM nodes.
function appendInline(parent, text) {
    const pattern = /(`[^`]+`)|(\*\*[^*]+\*\*|__[^_]+__)|(\*[^*]+\*|_[^_]+_)/g;
    let lastIndex = 0;
    let match;

    while ((match = pattern.exec(text)) !== null) {
        if (match.index > lastIndex) {
            parent.appendChild(document.createTextNode(text.slice(lastIndex, match.index)));
        }
        if (match[1]) {
            const code = document.createElement("code");
            code.textContent = match[1].slice(1, -1);
            parent.appendChild(code);
        } else if (match[2]) {
            const strong = document.createElement("strong");
            strong.textContent = match[2].slice(2, -2);
            parent.appendChild(strong);
        } else if (match[3]) {
            const em = document.createElement("em");
            em.textContent = match[3].slice(1, -1);
            parent.appendChild(em);
        }
        lastIndex = pattern.lastIndex;
    }

    if (lastIndex < text.length) {
        parent.appendChild(document.createTextNode(text.slice(lastIndex)));
    }
}

function selectedModel() {
    return state.models.get(el("model").value);
}

function isImageModel(model) {
    return model && model.modality === "Image";
}

function renderBadges(model) {
    const badges = el("badges");
    badges.replaceChildren();
    if (!model) {
        return;
    }

    applyModelDefaults(model);

    const items = [
        { text: model.device, cls: "device" },
        { text: model.source === "AzureFoundry" ? "cloud" : "on-device", cls: "" },
        { text: model.modality === "Image" ? "image" : "text", cls: model.modality === "Image" ? "on" : "" },
        { text: `tokens: ${model.exact ? "exact" : "approx"}`, cls: model.exact ? "on" : "" },
        { text: `reasoning ${model.supportsReasoning ? "\u2713" : "\u2717"}`, cls: model.supportsReasoning ? "on" : "" },
        { text: `caching ${model.supportsCaching ? "\u2713" : "\u2717"}`, cls: model.supportsCaching ? "on" : "" },
        { text: `logprobs ${model.supportsLogprobs ? "\u2713" : "\u2717"}`, cls: model.supportsLogprobs ? "on" : "" },
    ];

    items.forEach((item) => {
        const span = document.createElement("span");
        span.className = `badge ${item.cls}`.trim();
        span.textContent = item.text;
        badges.appendChild(span);
    });

    el("cacheDemo").disabled = !model.supportsCaching;
    setModelMode(model);
}

// Reasoning models need a big budget (thinking + answer); others a modest default.
function applyModelDefaults(model) {
    if (isImageModel(model)) {
        return;
    }

    el("maxTokens").value = model.supportsReasoning ? 10000 : 1024;
}

function setModelMode(model) {
    const imageMode = isImageModel(model);
    el("textControls").classList.toggle("hidden", imageMode);
    el("imageControls").classList.toggle("hidden", !imageMode);
    el("textLocalOutputSection").classList.toggle("hidden", imageMode);
    el("textModelResult").classList.toggle("hidden", imageMode);
    el("imageResult").classList.toggle("hidden", !imageMode);
    el("prompt").placeholder = imageMode
        ? "Describe the image to generate — prompt tokens update live, with no model call."
        : "Type a prompt — tokens update live, with no model call.";

    if (imageMode) {
        renderTokens(el("localOutputTokens"), []);
        el("localOutputCount").textContent = "";
        el("finishReason").textContent = "";
        el("comparison").classList.add("hidden");
        el("cacheResult").classList.add("hidden");
    }
}

function usageCard(label, value, isTotal = false, help = null) {
    const card = document.createElement("div");
    card.className = `usage-card${isTotal ? " total" : ""}`;

    const labelNode = document.createElement("div");
    labelNode.className = "label";
    labelNode.textContent = label;

    if (help) {
        labelNode.append(" ", createHelpBadge(label, help));
    }

    const valueNode = document.createElement("div");
    if (value === null || value === undefined) {
        valueNode.className = "value na";
        valueNode.textContent = "N/A";
    } else {
        valueNode.className = "value";
        valueNode.textContent = String(value);
    }

    card.append(labelNode, valueNode);
    return card;
}

function renderUsage(container, usage, logprobStats) {
    const cards = [
        usageCard("Prompt", usage.prompt, false, "Input tokens billed for your prompt. Usually higher than the raw local count because the service wraps it in a chat template and adds system/special tokens."),
        usageCard("Reasoning", usage.reasoning, false, "Hidden \u201cthinking\u201d tokens a reasoning model spends before the visible answer. N/A if the model doesn't report them."),
        usageCard("Output", usage.output, false, "Visible answer tokens (completion tokens minus any reasoning tokens)."),
        usageCard("Cached", usage.cached, false, "Prompt tokens served from the provider's cache at reduced cost. N/A if the model doesn't support caching."),
        usageCard("Total", usage.total, true, "Prompt + reasoning + output tokens billed for this call."),
    ];

    if (logprobStats) {
        cards.push(
            usageCard("Avg conf", formatPercent(logprobStats.averageConfidence), false, "Average probability the model gave to the tokens it actually chose. Higher = the model was more certain."),
            usageCard("Perplexity", formatPerplexity(logprobStats.perplexity), false, "How \u201csurprised\u201d the model was by its own output: exp(\u2212average log-probability). 1 = perfectly certain; higher = less certain."),
        );
    }

    container.replaceChildren(...cards);
}

// ---- AI Credits ------------------------------------------------------------------------------
// Pure credit maths, mirrored by the C# CreditEstimator. GitHub and Copilot Studio are billed
// very differently and take independent overhead inputs (they wrap prompts differently):
//   GitHub: input = (prompt − cached) + githubOverhead, cache-read = cached, cache-write = 0
//           (Azure/local report reads only), output = output + reasoning (billed as output).
//   Copilot Studio: per-1,000-token tiers applied to (total + studioOverhead).
function computeCredits(usage, model, studioRates, githubOverhead = 0, studioOverhead = 0) {
    const prompt = Number(usage.prompt) || 0;
    const cached = Number(usage.cached) || 0;
    const output = (Number(usage.output) || 0) + (Number(usage.reasoning) || 0);
    const ghOverhead = Math.max(0, Number(githubOverhead) || 0);
    const csOverhead = Math.max(0, Number(studioOverhead) || 0);
    const inputNonCached = Math.max(0, prompt - cached) + ghOverhead;
    const studioTotal = (Number(usage.total) || 0) + csOverhead;

    const githubInput = (inputNonCached / 1_000_000) * Number(model.input);
    const githubCacheRead = (cached / 1_000_000) * Number(model.cacheRead);
    const githubCacheWrite = 0; // Azure OpenAI / local report cache reads only.
    const githubOutput = (output / 1_000_000) * Number(model.output);

    const thousands = studioTotal / 1000;

    return {
        github: {
            modelId: model.id,
            modelLabel: model.label,
            input: githubInput,
            cacheRead: githubCacheRead,
            cacheWrite: githubCacheWrite,
            output: githubOutput,
            total: githubInput + githubCacheRead + githubCacheWrite + githubOutput,
        },
        studio: {
            total: studioTotal,
            basic: thousands * Number(studioRates.basic),
            standard: thousands * Number(studioRates.standard),
            premium: thousands * Number(studioRates.premium),
        },
    };
}

function formatCredits(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        return "N/A";
    }
    if (value === 0) {
        return "0";
    }
    if (value >= 100) {
        return value.toFixed(0);
    }
    if (value >= 1) {
        return value.toFixed(2);
    }
    // Small fractional credits: keep a few significant places, then trim trailing zeros.
    return value.toFixed(4).replace(/0+$/, "").replace(/\.$/, "");
}

function selectedBillingModel() {
    const rates = state.creditRates;
    if (!rates) {
        return null;
    }
    return rates.github.models.find((m) => m.id === state.billingModelId)
        || rates.github.models[0]
        || null;
}

function renderCredits() {
    const section = el("creditsSection");
    const usage = state.lastUsage;
    const rates = state.creditRates;
    const model = selectedBillingModel();

    if (!usage || !rates || !model) {
        section.classList.add("hidden");
        return;
    }
    section.classList.remove("hidden");

    const githubOverhead = Math.max(0, Number(el("githubOverhead").value) || 0);
    const studioOverhead = Math.max(0, Number(el("studioOverhead").value) || 0);
    const result = computeCredits(usage, model, rates.copilotStudio, githubOverhead, studioOverhead);
    const g = result.github;

    // ---- GitHub Copilot section ----
    const githubHelp = [
        `Input (non-cached, +${githubOverhead} overhead): ${formatCredits(g.input)}`,
        `Cache read: ${formatCredits(g.cacheRead)}`,
        `Cache write: ${formatCredits(g.cacheWrite)} (Azure/local report cache reads only)`,
        `Output (incl. reasoning): ${formatCredits(g.output)}`,
    ].join("\n");
    el("githubCreditCards").replaceChildren(
        usageCard(model.label, formatCredits(g.total), true, githubHelp),
    );
    setRichText(
        el("githubCreditsNote"),
        "Priced against the selected **billing model** \u2014 its own input / cache-read / "
        + "cache-write / output rate (reasoning billed as output; cache **reads** discounted, cache "
        + "**writes** a premium, but Azure OpenAI and local runtimes report only reads, so "
        + "cache-write is 0). Overhead adds **input** tokens (system prompt, tool definitions, "
        + "custom instructions, retrieved context).",
    );

    // ---- Copilot Studio section ----
    const studioHelp = (tier) =>
        `${tier} tier: ${formatCredits(result.studio.total / 1000)} \u00D7 1k-token rate. `
        + "Copilot Studio bills per 1,000 tokens; the tier is set by the model the AI tool uses.";
    el("studioCreditCards").replaceChildren(
        usageCard("Basic", formatCredits(result.studio.basic), false, studioHelp("Basic")),
        usageCard("Standard", formatCredits(result.studio.standard), false, studioHelp("Standard")),
        usageCard("Premium", formatCredits(result.studio.premium), false, studioHelp("Premium")),
    );
    setRichText(
        el("studioCreditsNote"),
        "Billed **per 1,000 tokens** of the total; the **Basic / Standard / Premium** tier is set "
        + "by the model the AI tool uses. Overhead adds to the **total** (the agent's own system "
        + "prompt, instructions and knowledge grounding). Ignores per-message and agent-action "
        + "charges, so real usage may be **higher**; for Microsoft 365 Copilot\u2013licensed "
        + "employee use, these token costs are **included**.",
    );

    setRichText(
        el("creditsDisclaimer"),
        `Estimated, at $0.01/credit (rates as of ${rates.asOf}). The billing model prices your `
        + "prompt's tokens **as if it ran there**, independent of the model that generated the reply. "
        + "GitHub Copilot and Copilot Studio are billed differently, so each section takes its own "
        + "**overhead tokens**.",
    );
}

function populateBillingModels(rates) {
    const select = el("billingModel");
    select.replaceChildren();
    rates.github.models.forEach((m) => {
        const option = document.createElement("option");
        option.value = m.id;
        option.textContent = m.label;
        select.appendChild(option);
    });
    const initial = rates.github.models.some((m) => m.id === rates.github.defaultId)
        ? rates.github.defaultId
        : (rates.github.models[0] && rates.github.models[0].id) || "";
    state.billingModelId = initial;
    select.value = initial;
    el("billingModelHelp").replaceChildren(
        createHelpBadge(
            "Billing model",
            "Prices your prompt's actual tokens against this GitHub Copilot model's rates, even if "
            + "the app can't run it. Changing it recomputes instantly from the last run's usage \u2014 "
            + "no new model call.",
        ),
    );
    el("githubOverheadHelp").replaceChildren(
        createHelpBadge(
            "Overhead tokens (GitHub Copilot)",
            "Extra input tokens a real Copilot agent adds that this app doesn't measure \u2014 system "
            + "prompt, tool/function definitions, custom instructions, retrieved context. Added to "
            + "the GitHub input class before pricing. Enter e.g. 10000.",
        ),
    );
    el("studioOverheadHelp").replaceChildren(
        createHelpBadge(
            "Overhead tokens (Copilot Studio)",
            "Extra tokens a Copilot Studio agent adds \u2014 its own system prompt, instructions and "
            + "knowledge-source grounding. Added to the total tokens Copilot Studio meters per 1,000. "
            + "Enter e.g. 10000.",
        ),
    );
}

async function loadCreditRates() {
    try {
        const response = await fetch("/api/credit-rates");
        if (!response.ok) {
            return;
        }
        const rates = await response.json();
        if (!rates || !rates.github || !Array.isArray(rates.github.models) || !rates.github.models.length) {
            return;
        }
        state.creditRates = rates;
        populateBillingModels(rates);
    } catch {
        // Credits are a non-essential overlay; ignore fetch failures.
    }
}

function formatPercent(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        return "N/A";
    }

    return `${Math.round(value * 100)}%`;
}

function formatPerplexity(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        return "N/A";
    }

    // Healthy perplexity is small (≈1–100). Keep big/pathological values compact and readable
    // instead of dumping a 280-digit float.
    if (value >= 10000) {
        return value.toExponential(1);
    }

    if (value >= 100) {
        return value.toFixed(0);
    }

    return value.toFixed(2);
}

function confidenceClass(probability) {
    if (typeof probability !== "number" || !Number.isFinite(probability)) {
        return "confidence-unknown";
    }

    if (probability >= 0.9) {
        return "confidence-high";
    }

    if (probability >= 0.7) {
        return "confidence-mid";
    }

    if (probability >= 0.4) {
        return "confidence-low";
    }

    return "confidence-very-low";
}

// Decode a single token's raw UTF-8 bytes to text where they form a complete, valid sequence
// (e.g. a whole-character alternative). Falls back to the provider's escaped token string for
// partial byte fragments that can't stand alone.
function decodeStandaloneToken(item) {
    if (Array.isArray(item.bytes) && item.bytes.length > 0) {
        const text = new TextDecoder("utf-8").decode(Uint8Array.from(item.bytes));
        if (text && !text.includes("\uFFFD")) {
            return visibleText(text);
        }
    }

    return visibleText(item.token);
}

function renderLogprobs(modelBlock) {
    const heatmap = el("logprobHeatmap");
    const note = el("logprobNote");
    heatmap.replaceChildren();

    if (!modelBlock.supportsLogprobs) {
        heatmap.classList.add("hidden");
        note.textContent = "Logprobs not supported by this model.";
        return;
    }

    const logprobs = Array.isArray(modelBlock.logprobs) ? modelBlock.logprobs : [];
    if (logprobs.length === 0) {
        heatmap.classList.add("hidden");
        note.textContent = "Logprobs unavailable for this response.";
        return;
    }

    heatmap.classList.remove("hidden");
    note.textContent = "Separate token-confidence view; markdown output above is preserved.";

    // Streaming decoder reassembles multi-token characters (e.g. an emoji whose UTF-8 bytes
    // are split across several tokens). decode(..., {stream:true}) returns "" until a byte
    // sequence completes, then yields the whole glyph on the completing token.
    const decoder = new TextDecoder("utf-8");

    logprobs.forEach((item, index) => {
        const token = document.createElement("span");
        token.className = `logprob-token ${confidenceClass(item.prob)}`;
        token.tabIndex = 0;
        token.setAttribute("aria-label", `Token ${index + 1}, confidence ${formatPercent(item.prob)}`);

        let display;
        if (Array.isArray(item.bytes) && item.bytes.length > 0) {
            display = decoder.decode(Uint8Array.from(item.bytes), { stream: true });
        } else {
            display = item.token;
        }

        if (display === "") {
            // A fragment of a multi-token character; the full glyph renders on the token that
            // completes the byte sequence. Keep the cell visible so its confidence still shows.
            token.classList.add("logprob-cont");
            token.title = "Part of a multi-token character (completes on a later token).";
            token.appendChild(document.createTextNode("\u2026"));
        } else {
            token.appendChild(document.createTextNode(visibleText(display)));
        }

        const popover = document.createElement("span");
        popover.className = "logprob-popover";

        const chosen = document.createElement("span");
        chosen.className = "logprob-popover-title";
        chosen.textContent = `Chosen ${formatPercent(item.prob)}`;
        popover.appendChild(chosen);

        const alternatives = Array.isArray(item.top) ? item.top : [];
        if (alternatives.length === 0) {
            const empty = document.createElement("span");
            empty.textContent = "No alternatives returned";
            popover.appendChild(empty);
        } else {
            alternatives.forEach((alternative) => {
                const row = document.createElement("span");
                row.textContent = `${decodeStandaloneToken(alternative)} ${formatPercent(alternative.prob)}`;
                popover.appendChild(row);
            });
        }

        token.appendChild(popover);
        heatmap.appendChild(token);
    });
}

function renderLogprobPlaceholder(model) {
    const heatmap = el("logprobHeatmap");
    const note = el("logprobNote");
    heatmap.replaceChildren();
    heatmap.classList.add("hidden");
    note.textContent = model?.supportsLogprobs
        ? "Run this logprobs-capable model to see token confidence."
        : "Logprobs not supported by this model.";
}

function formatDecimal(value, digits = 2) {
    if (!Number.isFinite(value)) {
        return null;
    }

    return value.toFixed(digits);
}

function formatRatioPercent(numerator, denominator) {
    if (!Number.isFinite(numerator) || !Number.isFinite(denominator) || denominator <= 0) {
        return null;
    }

    return `${formatDecimal((numerator / denominator) * 100, 1)}%`;
}

function totalTokenChars(tokens) {
    return tokens.reduce((sum, token) => sum + token.value.length, 0);
}

function charsPerToken(tokens) {
    if (tokens.length === 0) {
        return null;
    }

    return formatDecimal(totalTokenChars(tokens) / tokens.length, 2);
}

function uniqueRepeated(tokens) {
    const unique = new Set(tokens.map((token) => token.id)).size;
    return `${unique} / ${tokens.length - unique}`;
}

function countWhitespaceTokens(tokens) {
    return tokens.filter((token) => token.value.length > 0 && token.value.trim().length === 0).length;
}

function countPunctuationTokens(tokens) {
    return tokens.filter((token) => {
        const value = token.value.trim();
        return value.length > 0 && /^[\p{P}\p{S}]+$/u.test(value);
    }).length;
}

function tokensPerSecond(outputTokens, latencyMs, ttftMs) {
    if (!Number.isFinite(outputTokens) || !Number.isFinite(latencyMs) || latencyMs <= 0) {
        return null;
    }

    const generationMs = Number.isFinite(ttftMs) && latencyMs > ttftMs
        ? latencyMs - ttftMs
        : latencyMs;
    return `${formatDecimal(outputTokens / (generationMs / 1000), 2)} tok/s`;
}

function renderDeepStats(container, result) {
    const promptTokens = result.local.promptTokens;
    const outputTokens = result.local.outputTokens;
    const usage = result.model.usage;
    const latencyMs = result.model.latencyMs;
    const ttftMs = result.model.ttftMs;

    const cards = [
        usageCard("Latency", Number.isFinite(latencyMs) ? `${latencyMs} ms` : null, false, "Total wall-clock time for the model call."),
        usageCard("TTFT", Number.isFinite(ttftMs) ? `${ttftMs} ms` : null, false, "Time to first token \u2014 how long until the first streamed output token arrived."),
        usageCard("Tokens/sec", tokensPerSecond(usage.output, latencyMs, ttftMs), false, "Output tokens generated per second, measured after the first token."),
        usageCard("Prompt chars/tok", charsPerToken(promptTokens), false, "Average characters per prompt token. Higher means denser tokens (more text per token)."),
        usageCard("Output chars/tok", charsPerToken(outputTokens), false, "Average characters per output token."),
        usageCard("Prompt:Output", usage.output > 0 ? `${formatDecimal(usage.prompt / usage.output, 2)}:1` : null, false, "Ratio of prompt tokens to output tokens."),
        usageCard("Prompt uniq/repeat", uniqueRepeated(promptTokens), false, "Distinct token ids vs repeated ones in the prompt."),
        usageCard("Output uniq/repeat", uniqueRepeated(outputTokens), false, "Distinct token ids vs repeated ones in the output."),
        usageCard("Prompt whitespace", countWhitespaceTokens(promptTokens), false, "Number of prompt tokens that are whitespace."),
        usageCard("Prompt punctuation", countPunctuationTokens(promptTokens), false, "Number of prompt tokens that are punctuation."),
        usageCard("Output whitespace", countWhitespaceTokens(outputTokens), false, "Number of output tokens that are whitespace."),
        usageCard("Output punctuation", countPunctuationTokens(outputTokens), false, "Number of output tokens that are punctuation."),
    ];

    if (result.model.supportsReasoning && usage.reasoning !== null && usage.reasoning !== undefined) {
        cards.push(usageCard("Reasoning share", formatRatioPercent(usage.reasoning, usage.total), false, "Share of total tokens spent on hidden reasoning."));
    }

    if (result.model.supportsCaching && usage.cached !== null && usage.cached !== undefined) {
        cards.push(usageCard("Cache hit", formatRatioPercent(usage.cached, usage.prompt), false, "Share of prompt tokens served from the provider's cache."));
    }

    container.replaceChildren(...cards);
}

function renderImageUsage(container, usage) {
    const values = usage || {};
    const total = values.inputTokens !== null && values.inputTokens !== undefined &&
        values.outputTokens !== null && values.outputTokens !== undefined
        ? values.inputTokens + values.outputTokens
        : null;

    container.replaceChildren(
        usageCard("Input total", values.inputTokens, false, "All input tokens billed: prompt text tokens plus any input image tokens."),
        usageCard("Prompt text", values.textTokens, false, "Tokens from your text prompt."),
        usageCard("Input image", values.imageTokens, false, "Tokens from any input/reference image (0 for text-to-image)."),
        usageCard("Output image", values.outputTokens, false, "Tokens billed for the generated image. Scales with size and quality \u2014 there is no flat per-image fee."),
        usageCard("Total", total, true, "Input + output tokens billed for this image generation."),
    );
}

function renderImageExplanation(usage, size, quality) {
    const node = el("imageExplanation");
    const values = usage || {};
    const output = values.outputTokens === null || values.outputTokens === undefined
        ? "N/A"
        : String(values.outputTokens);
    const input = values.textTokens === null || values.textTokens === undefined
        ? "N/A"
        : String(values.textTokens);

    const text = document.createElement("p");
    text.textContent =
        `Prompt text becomes input text tokens (${input}). ` +
        `Rendered image becomes output image tokens (${output}); output token count scales with size ${size} and quality ${quality}. ` +
        "There is no flat per-image fee in this demo path — billing is token-based.";

    node.replaceChildren(text);
}

async function postJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
    });
    if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `${response.status} ${response.statusText}`);
    }
    return response.json();
}

function parseSseEvent(block) {
    let eventName = "message";
    const data = [];
    block.split("\n").forEach((line) => {
        if (line.startsWith("event:")) {
            eventName = line.slice(6).trim();
        } else if (line.startsWith("data:")) {
            data.push(line.slice(5).replace(/^ /, ""));
        }
    });

    return {
        event: eventName,
        data: data.length > 0 ? JSON.parse(data.join("\n")) : null,
    };
}

function consumeSseBuffer(buffer, onEvent) {
    const normalized = buffer.replace(/\r\n/g, "\n");
    let start = 0;
    let boundary = normalized.indexOf("\n\n", start);
    while (boundary !== -1) {
        const block = normalized.slice(start, boundary);
        if (block.trim().length > 0) {
            onEvent(parseSseEvent(block));
        }
        start = boundary + 2;
        boundary = normalized.indexOf("\n\n", start);
    }

    return normalized.slice(start);
}

async function streamAnalyze(request, handlers) {
    const response = await fetch("/api/analyze-stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
    if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `${response.status} ${response.statusText}`);
    }
    if (!response.body || !response.body.getReader) {
        throw new Error("Streaming response body unavailable.");
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let donePayload = null;
    let sawDelta = false;

    const handleEvent = ({ event, data }) => {
        if (event === "meta" && data?.local) {
            handlers.onMeta(data.local);
        } else if ((event === "delta" || event === "token") && typeof data?.text === "string") {
            sawDelta = true;
            handlers.onDelta(data.text);
        } else if (event === "done") {
            donePayload = data;
        } else if (event === "error") {
            const err = new Error(data?.message || "Streaming model call failed.");
            err.partial = sawDelta;
            throw err;
        }
    };

    try {
        for (;;) {
            const { value, done } = await reader.read();
            if (done) {
                break;
            }
            buffer = consumeSseBuffer(buffer + decoder.decode(value, { stream: true }), handleEvent);
        }
        buffer = consumeSseBuffer(buffer + decoder.decode(), handleEvent);
    } catch (err) {
        err.partial = err.partial || sawDelta;
        throw err;
    }

    if (!donePayload) {
        const err = new Error("Streaming ended before final response.");
        err.partial = sawDelta;
        throw err;
    }

    return donePayload;
}

function renderInitialStreamState() {
    const out = el("modelOutput");
    out.classList.remove("markdown");
    out.replaceChildren();
    renderTokens(el("localOutputTokens"), []);
    el("localOutputCount").textContent = "";
    el("usageCards").replaceChildren();
    el("deepStats").replaceChildren();
    el("creditsSection").classList.add("hidden");
    el("finishReason").textContent = "Streaming…";
    el("comparison").classList.add("hidden");
}

function renderAnalyzeResult(result, model, prompt) {
    renderTokens(el("localPromptTokens"), result.local.promptTokens, { modelId: model.id, text: prompt });
    el("localPromptCount").textContent = `(${result.local.promptTokenCount})`;
    renderEncodingLine(result.local.encoding, result.local.exact);
    renderTokens(el("localOutputTokens"), result.local.outputTokens, { modelId: model.id, text: result.model.output || "" });
    el("localOutputCount").textContent = `(${result.local.outputTokens.length})`;

    if (result.model.output) {
        renderMarkdown(el("modelOutput"), result.model.output);
    } else {
        const out = el("modelOutput");
        out.classList.remove("markdown");
        out.textContent = "(empty)";
    }
    renderUsage(el("usageCards"), result.model.usage, result.model.logprobStats);
    renderDeepStats(el("deepStats"), result);
    state.lastUsage = result.model.usage;
    renderCredits();
    renderLogprobs(result.model);

    const finish = result.model.finishReason;
    const truncated = finish && finish.toLowerCase().includes("length");
    el("finishReason").textContent = finish
        ? `Finish reason: ${finish}${truncated ? " — output was cut off at the Max output tokens cap." : ""}`
        : "";

    renderComparison(result.local.promptTokenCount, result.model.usage.prompt);

    if (result.model.usage.reasoning && !result.model.output) {
        setStatus("Reasoning model used its whole output budget thinking (0 visible answer tokens). Raise Max output tokens.", true);
    } else if (truncated) {
        setStatus("Output hit the Max output tokens cap and was truncated. Raise Max output tokens for a complete answer.", true);
    } else {
        setStatus("Done.");
    }
}

async function runModelFallback(request, model, prompt, reason) {
    setStatus(`Streaming failed (${reason}); retrying non-streaming analyze…`, true);
    const result = await postJson("/api/analyze", request);
    renderAnalyzeResult(result, model, prompt);
}

const tokenizeLive = debounce(async () => {
    const model = selectedModel();
    const text = el("prompt").value;
    if (!model || text.length === 0) {
        renderTokens(el("localPromptTokens"), []);
        el("localPromptCount").textContent = "";
        el("localEncoding").replaceChildren();
        return;
    }

    try {
        const result = await postJson("/api/tokenize", { text, modelId: model.id });
        renderTokens(el("localPromptTokens"), result.tokens, { modelId: model.id, text });
        el("localPromptCount").textContent = `(${result.count})`;
        renderEncodingLine(result.encoding, result.exact);
    } catch (err) {
        setStatus(`Tokenize failed: ${err.message}`, true);
    }
}, 300);

async function runModel() {
    const model = selectedModel();
    if (isImageModel(model)) {
        await generateImage();
        return;
    }

    const prompt = el("prompt").value.trim();
    if (!model || prompt.length === 0) {
        setStatus("Enter a prompt first.", true);
        return;
    }

    el("run").disabled = true;
    renderLogprobPlaceholder(model);
    renderInitialStreamState();
    setStatus(`Streaming ${model.label}\u2026 (this costs tokens)`);
    try {
        const max = parseInt(el("maxTokens").value, 10) || undefined;
        const request = { prompt, modelId: model.id, maxOutputTokens: max };
        const result = await streamAnalyze(request, {
            onMeta: (local) => {
                renderTokens(el("localPromptTokens"), local.promptTokens, { modelId: model.id, text: prompt });
                el("localPromptCount").textContent = `(${local.promptTokenCount})`;
                renderEncodingLine(local.encoding, local.exact);
            },
            onDelta: (text) => {
                el("modelOutput").appendChild(document.createTextNode(text));
            },
        });
        renderAnalyzeResult(result, model, prompt);
    } catch (err) {
        if (!err.partial) {
            try {
                const max = parseInt(el("maxTokens").value, 10) || undefined;
                await runModelFallback({ prompt, modelId: model.id, maxOutputTokens: max }, model, prompt, err.message);
            } catch (fallbackErr) {
                setStatus(`Model call failed: ${fallbackErr.message}`, true);
            }
        } else {
            setStatus(`Streaming model call failed: ${err.message}`, true);
        }
    } finally {
        el("run").disabled = false;
    }
}

async function generateImage() {
    const model = selectedModel();
    const prompt = el("prompt").value.trim();
    if (!model || prompt.length === 0) {
        setStatus("Enter an image prompt first.", true);
        return;
    }

    const size = el("imageSize").value;
    const quality = el("imageQuality").value;
    el("generateImage").disabled = true;
    setStatus(`Generating image with ${model.label}… (this costs image output tokens)`);
    try {
        const result = await postJson("/api/generate-image", { modelId: model.id, prompt, size, quality });

        renderTokens(el("localPromptTokens"), result.local.promptTokens);
        el("localPromptCount").textContent = `(${result.local.promptTokenCount})`;
        renderEncodingLine(result.local.encoding, result.local.exact);

        const image = el("generatedImage");
        image.src = `data:${result.imageMediaType || "image/png"};base64,${result.imageBase64}`;
        image.alt = `Generated image from ${model.label}`;
        el("imagePlaceholder").classList.add("hidden");

        renderImageUsage(el("imageUsageCards"), result.usage);
        renderImageExplanation(result.usage, size, quality);
        setStatus("Image done.");
    } catch (err) {
        setStatus(`Image generation failed: ${err.message}`, true);
    } finally {
        el("generateImage").disabled = false;
    }
}

function renderComparison(localCount, modelPrompt) {
    const node = el("comparison");
    node.classList.remove("hidden");
    const delta = modelPrompt - localCount;
    node.textContent =
        `Local counted ${localCount} token(s) in your raw prompt text. ` +
        `The model billed prompt_tokens = ${modelPrompt}` +
        (delta !== 0
            ? ` — ${delta > 0 ? "+" + delta : delta} from the chat template + system/special tokens the service adds before counting.`
            : " — they happen to match here.");
}

async function cacheDemo() {
    const model = selectedModel();
    if (!model || !model.supportsCaching) {
        return;
    }

    el("cacheDemo").disabled = true;
    setStatus("Running prompt-cache demo (two identical calls)\u2026");
    try {
        const result = await postJson("/api/cache-demo", { modelId: model.id });
        el("cacheResult").classList.remove("hidden");

        const cards = el("cacheCards");
        cards.replaceChildren(
            usageCard("1st cached", result.firstUsage.cached, false, "Cached prompt tokens reported on the first of two identical calls (usually 0 — nothing is cached yet)."),
            usageCard("2nd cached", result.secondUsage.cached, false, "Cached prompt tokens reported on the second identical call — the provider served the shared prefix from cache."),
            usageCard("Prefix tok", result.prefixTokenCount, false, "Length of the identical shared prefix (must be large enough — typically ≥1024 tokens — for caching to kick in)."),
            usageCard("2nd prompt", result.secondUsage.prompt, false, "Total prompt tokens billed on the second call (cached + uncached)."),
            usageCard("Cache hit", result.cachedTokens, true, "Prompt tokens served from cache on the second call — these are billed at a reduced rate."),
        );
        el("cacheNote").textContent =
            `Two identical requests with a ${result.prefixTokenCount}-token shared prefix. ` +
            `The second call reused ${result.cachedTokens} cached prompt token(s).`;
        setStatus("Cache demo done.");
    } catch (err) {
        setStatus(`Cache demo failed: ${err.message}`, true);
    } finally {
        el("cacheDemo").disabled = !model.supportsCaching;
    }
}

function populateModels(models) {
    const select = el("model");
    select.replaceChildren();

    const groups = {
        AzureFoundry: { label: "Azure Foundry (cloud)", node: null },
        FoundryLocal: { label: "Foundry Local (on-device)", node: null },
        LmStudio: { label: "LM Studio (local)", node: null },
        Ollama: { label: "Ollama (local)", node: null },
    };

    models.forEach((model) => {
        state.models.set(model.id, model);
        const group = groups[model.source] || groups.AzureFoundry;
        group.node ??= document.createElement("optgroup");
        group.node.label = group.label;

        const option = document.createElement("option");
        option.value = model.id;
        option.textContent = `${model.label} \u2014 ${model.device}`;
        group.node.appendChild(option);
    });

    Object.values(groups).forEach((group) => {
        if (group.node) {
            select.appendChild(group.node);
        }
    });
}

// ---- Tokenisation explainer (animated walkthrough) ----

const explainer = { index: 0, subTimers: [] };

// Real, verified o200k_base (GPT-4o/4.1) merge trace for "lowest" — used until the live
// per-model trace loads, or if it can't be fetched. Includes the candidate field per step and
// the rejected pairs, so the fallback animation explains the decision exactly like the live one.
const MERGE_FALLBACK_TRACE = {"word":"lowest","encoding":"o200k_base","verified":true,"idIsRank":true,"finalTokens":[{"text":"lowest","id":183722}],"rejectedPairs":[],"steps":[{"rank":268,"left":"e","right":"s","result":"es","candidates":[{"left":"e","right":"s","rank":268,"chosen":true},{"left":"s","right":"t","rank":302,"chosen":false},{"left":"o","right":"w","rank":384,"chosen":false},{"left":"l","right":"o","rank":746,"chosen":false},{"left":"w","right":"e","rank":854,"chosen":false}]},{"rank":376,"left":"es","right":"t","result":"est","candidates":[{"left":"es","right":"t","rank":376,"chosen":true},{"left":"o","right":"w","rank":384,"chosen":false},{"left":"l","right":"o","rank":746,"chosen":false},{"left":"w","right":"es","rank":55466,"chosen":false}]},{"rank":384,"left":"o","right":"w","result":"ow","candidates":[{"left":"o","right":"w","rank":384,"chosen":true},{"left":"l","right":"o","rank":746,"chosen":false},{"left":"w","right":"est","rank":14914,"chosen":false}]},{"rank":14739,"left":"l","right":"ow","result":"low","candidates":[{"left":"l","right":"ow","rank":14739,"chosen":true}]},{"rank":183722,"left":"low","right":"est","result":"lowest","candidates":[{"left":"low","right":"est","rank":183722,"chosen":true}]}]};

// Real, verified o200k_base per-word merge traces for "I love tokenization" (3 words -> 4
// tokens) — used until the live per-model phrase trace loads, or if it can't be fetched.
const MERGE_PHRASE_FALLBACK = {
    phrase: "I love tokenization",
    encoding: "o200k_base",
    verified: true,
    tokenCount: 4,
    words: [{"word":"I","steps":[],"finalTokens":[{"text":"I","id":40}],"rejectedPairs":[],"verified":true},{"word":" love","finalTokens":[{"text":" love","id":3047}],"rejectedPairs":[],"verified":true,"steps":[{"rank":305,"left":" ","right":"l","result":" l","candidates":[{"left":" ","right":"l","rank":305,"chosen":true},{"left":"o","right":"v","rank":569,"chosen":false},{"left":"v","right":"e","rank":737,"chosen":false},{"left":"l","right":"o","rank":746,"chosen":false}]},{"rank":569,"left":"o","right":"v","result":"ov","candidates":[{"left":"o","right":"v","rank":569,"chosen":true},{"left":"v","right":"e","rank":737,"chosen":false},{"left":" l","right":"o","rank":1445,"chosen":false}]},{"rank":1048,"left":"ov","right":"e","result":"ove","candidates":[{"left":"ov","right":"e","rank":1048,"chosen":true},{"left":" l","right":"ov","rank":7106,"chosen":false}]},{"rank":3047,"left":" l","right":"ove","result":" love","candidates":[{"left":" l","right":"ove","rank":3047,"chosen":true}]}]},{"word":" tokenization","finalTokens":[{"text":" token","id":6602},{"text":"ization","id":2860}],"rejectedPairs":[{"left":" token","right":"ization","glued":" tokenization"}],"verified":true,"steps":[{"rank":260,"left":" ","right":"t","result":" t","candidates":[{"left":" ","right":"t","rank":260,"chosen":true},{"left":"e","right":"n","rank":262,"chosen":false},{"left":"o","right":"n","rank":263,"chosen":false},{"left":"a","right":"t","rank":266,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":262,"left":"e","right":"n","result":"en","candidates":[{"left":"e","right":"n","rank":262,"chosen":true},{"left":"o","right":"n","rank":263,"chosen":false},{"left":"a","right":"t","rank":266,"chosen":false},{"left":" t","right":"o","rank":316,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":263,"left":"o","right":"n","result":"on","candidates":[{"left":"o","right":"n","rank":263,"chosen":true},{"left":"a","right":"t","rank":266,"chosen":false},{"left":" t","right":"o","rank":316,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":266,"left":"a","right":"t","result":"at","candidates":[{"left":"a","right":"t","rank":266,"chosen":true},{"left":"i","right":"on","rank":294,"chosen":false},{"left":" t","right":"o","rank":316,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":294,"left":"i","right":"on","result":"ion","candidates":[{"left":"i","right":"on","rank":294,"chosen":true},{"left":" t","right":"o","rank":316,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":316,"left":" t","right":"o","result":" to","candidates":[{"left":" t","right":"o","rank":316,"chosen":true},{"left":"at","right":"ion","rank":387,"chosen":false},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":387,"left":"at","right":"ion","result":"ation","candidates":[{"left":"at","right":"ion","rank":387,"chosen":true},{"left":"i","right":"z","rank":482,"chosen":false}]},{"rank":482,"left":"i","right":"z","result":"iz","candidates":[{"left":"i","right":"z","rank":482,"chosen":true},{"left":"k","right":"en","rank":2144,"chosen":false}]},{"rank":2144,"left":"k","right":"en","result":"ken","candidates":[{"left":"k","right":"en","rank":2144,"chosen":true},{"left":"iz","right":"ation","rank":2860,"chosen":false}]},{"rank":2860,"left":"iz","right":"ation","result":"ization","candidates":[{"left":"iz","right":"ation","rank":2860,"chosen":true},{"left":" to","right":"ken","rank":6602,"chosen":false}]},{"rank":6602,"left":" to","right":"ken","result":" token","candidates":[{"left":" to","right":"ken","rank":6602,"chosen":true}]}]}],
};

// How long to hold a frame before advancing, scaled to its caption length so there is always
// enough time to read it (longer explanations stay up longer).
function frameHold(frame) {
    const len = (frame && frame.note ? frame.note.length : 0);
    return Math.min(6300, Math.max(2550, 1800 + len * 42));
}

// Render the first frame immediately, then schedule the rest with read-time-based holds.
function scheduleFrames(frames, render) {
    render(frames[0]);
    let elapsed = 0;
    for (let i = 1; i < frames.length; i++) {
        elapsed += frameHold(frames[i - 1]);
        const frame = frames[i];
        explainer.subTimers.push(setTimeout(() => render(frame), elapsed));
    }
}

function explainerChip(text, colorIndex, idText, flip) {
    const chip = document.createElement("span");
    chip.className = `explainer-chip tok-c${colorIndex % 8}${flip ? " explainer-flip" : ""}`;

    const label = document.createElement("span");
    label.className = "explainer-chip-text";
    label.textContent = visibleText(text);
    chip.appendChild(label);

    if (idText) {
        const id = document.createElement("span");
        id.className = "explainer-chip-id";
        id.textContent = `id ${idText}`;
        chip.appendChild(id);
    }

    return chip;
}

function explainerRow(stage, chips, flip) {
    const row = document.createElement("div");
    row.className = "explainer-row";
    chips.forEach((c, i) => {
        const chip = explainerChip(c.text, c.color, c.id, flip);
        chip.style.animationDelay = `${i * 0.18}s`;
        row.appendChild(chip);
    });
    stage.appendChild(row);
}

const EXPLAINER_STEPS = [
    {
        caption: "Watch the tokenizer split the word \u201clowest\u201d using Byte Pair Encoding (BPE). Each round it looks up every adjacent pair in the model\u2019s fixed merge table. Pairs the table knows have a \u201crank\u201d (a number = priority). It compares those ranks and merges the pair with the **smallest** number, then repeats \u2014 stopping when no remaining pair exists in the table.",
        build: buildMergeAnimation,
    },
    {
        caption() {
            return currentMergeTrace().idIsRank
                ? "Those final pieces are the tokens. Each rank you saw is simply that chunk's id in the model's fixed dictionary \u2014 lower rank = learned earlier = more common, so common pieces merge first."
                : "Those final pieces are the tokens. This model's merge ranks aren't public, so the numbers shown are each chunk's vocabulary **id** used as a stand-in for rank \u2014 the final split is exact, but treat the step order as a faithful reconstruction, not the model's exact merge priority.";
        },
        build(stage) {
            const trace = currentMergeTrace();
            explainerRow(stage, trace.finalTokens.map((t, i) => ({ text: t.text, color: i, id: String(t.id) })), true);
        },
    },
    {
        caption: "Now a whole phrase \u2014 \u201cI love tokenization\u201d. Each word is tokenised on its own, and the space before a word is a real character that merges **into** that word's first token (not a separator). Watch 3 words become 4 tokens.",
        build: buildPhraseAnimation,
    },
    {
        caption: "Tokenizers are vendor-specific. Most model families use deterministic byte-level BPE, but vocab size and published files differ; Google’s SentencePiece/Unigram path is the big algorithmic contrast.",
        build(stage) {
            const panel = document.createElement("div");
            panel.className = "explainer-vendor-panel";

            const intro = document.createElement("p");
            intro.className = "explainer-note";
            intro.textContent = "This app asks the selected model for its live merge trace when available; the animation above follows that model rather than a single-vendor rule.";

            const list = document.createElement("ul");
            list.className = "explainer-bullets explainer-vendors";
            [
                { name: "OpenAI GPT", detail: "tiktoken byte-level BPE. cl100k_base (~100k) and o200k_base (~200k) store mergeable ranks; no separate merges file." },
                { name: "Qwen (Alibaba)", detail: "GPT-2/tiktoken-style byte-level BPE with public vocab and merge data, commonly vocab.json + merges.txt or .tiktoken ranks depending release." },
                { name: "Meta Llama", detail: "Llama 3 uses tiktoken-style byte-level BPE with ~128k tokens; Llama 1/2 used SentencePiece BPE." },
                { name: "Google Gemini/Gemma", detail: "SentencePiece/Unigram, not greedy BPE: it scores candidate segmentations and chooses the most likely split." },
                { name: "Anthropic Claude", detail: "BPE-family/proprietary. Claude 3+ tokenizer files are not public; use Anthropic token counting for exact counts." },
                { name: "Microsoft MAI", detail: "Microsoft in-house models. Tokenizer details are largely not public, so treat counts as vendor-specific rather than assuming shared GPT rules." },
            ].forEach((vendorInfo) => {
                const item = document.createElement("li");
                const name = document.createElement("strong");
                name.textContent = `${vendorInfo.name}: `;
                const detail = document.createElement("span");
                detail.textContent = vendorInfo.detail;
                item.append(name, detail);
                list.appendChild(item);
            });

            panel.append(intro, list);
            stage.appendChild(panel);
        },
    },
    {
        caption: "Rules of thumb:",
        build(stage) {
            const list = document.createElement("ul");
            list.className = "explainer-bullets";
            [
                "Token count is driven by **frequency**, not length: a sequence is a single token only if it was common enough in training to earn its own vocabulary entry.",
                "So a long common word can be 1 token, while a short rare, misspelled or made-up string can split into many.",
                "\u201c~4 characters \u2248 1 token\u201d is only a rough average for typical English \u2014 handy for estimating cost, but not a rule.",
                "You are billed per token \u2014 both input and output.",
                "Capitalisation, spaces and punctuation each change the split (they change the byte sequence being looked up).",
                "Hover any token chip in the app to see its real merge chain.",
            ].forEach((line) => {
                const item = document.createElement("li");
                setRichText(item, line);
                list.appendChild(item);
            });
            stage.appendChild(list);
        },
    },
];

function currentMergeTrace() {
    const trace = state.mergeTrace;
    return trace && Array.isArray(trace.steps) && trace.steps.length > 0 ? trace : MERGE_FALLBACK_TRACE;
}

function q(s) {
    return `\u201c${visibleText(s)}\u201d`;
}

function buildMergeFrames(trace) {
    const frames = [];
    let symbols = trace.word.split("");
    frames.push({ symbols: symbols.slice(), note: "Start: each character is a separate symbol. We also have a fixed table of known pairs, each with a rank \u2014 a number where smaller means \u201clearned earlier / more common\u201d." });

    trace.steps.forEach((step, s) => {
        let idx = -1;
        for (let i = 0; i < symbols.length - 1; i++) {
            if (symbols[i] === step.left && symbols[i + 1] === step.right) { idx = i; break; }
        }
        if (idx < 0) { return; }
        frames.push({
            symbols: symbols.slice(),
            highlight: [idx, idx + 1],
            candidates: step.candidates,
            note: `Round ${s + 1}: check every neighbouring pair, look up its rank, and pick the **smallest** number. ${q(step.left)}+${q(step.right)} (${step.rank}) wins.`,
        });
        symbols = symbols.slice(0, idx).concat([step.result], symbols.slice(idx + 2));
        frames.push({ symbols: symbols.slice(), merged: idx, note: `Merge the winner into one piece: ${q(step.result)}. Then do it all again.` });
    });

    const rejected = Array.isArray(trace.rejectedPairs) ? trace.rejectedPairs : [];
    if (symbols.length > 1 && rejected.length > 0) {
        frames.push({
            symbols: symbols.slice(),
            highlight: [0, symbols.length - 1],
            rejected,
            note: "Now check the leftover neighbours. Glued together they are **not** in the table (no rank), so they cannot merge \u2014 this is where it stops.",
        });
    }

    const doneNote = symbols.length > 1
        ? `Result: ${trace.finalTokens.length} tokens. They couldn't merge any further \u2014 so a word can be several tokens.`
        : "Result: it all merged into one entry the table knows \u2192 just 1 token.";
    const verifiedNote = trace.verified
        ? (trace.idIsRank
            ? ` (This is the real ${trace.encoding} tokenizer \u2713)`
            : ` (Verified \u2713 \u2014 the final tokens match the real ${trace.encoding} tokenizer. This model's merge ranks aren't public, so the numbers are vocabulary ids used as a stand-in: the final split is exact, the step order is a faithful reconstruction.)`)
        : "";
    frames.push({
        symbols: symbols.slice(),
        done: true,
        note: `${doneNote}${verifiedNote}`,
    });
    return frames;
}

// Render the decision visually: a ranked leaderboard of candidate pairs (winner highlighted),
// or the rejected leftover pair(s). Built with DOM/textContent only.
function renderDecisionPanel(panel, frame) {
    panel.replaceChildren();
    const cands = Array.isArray(frame.candidates) ? frame.candidates : [];
    const rejected = Array.isArray(frame.rejected) ? frame.rejected : [];

    if (cands.length > 0) {
        const label = document.createElement("div");
        label.className = "panel-label";
        label.textContent = "Pairs found in the table \u2014 smallest rank wins:";
        panel.appendChild(label);

        const list = document.createElement("div");
        list.className = "cand-list";
        cands.slice(0, 6).forEach((c) => {
            const pill = document.createElement("div");
            pill.className = `cand-pill ${c.chosen ? "cand-win" : "cand-lose"}`;

            const pair = document.createElement("span");
            pair.className = "cand-pair";
            pair.textContent = `${visibleText(c.left)}+${visibleText(c.right)}`;

            const rank = document.createElement("span");
            rank.className = "cand-rank";
            rank.textContent = `rank ${c.rank}`;

            pill.append(pair, rank);
            if (c.chosen) {
                const badge = document.createElement("span");
                badge.className = "cand-badge";
                badge.textContent = "\u2713 lowest";
                pill.appendChild(badge);
            }
            list.appendChild(pill);
        });
        if (cands.length > 6) {
            const more = document.createElement("div");
            more.className = "cand-more";
            more.textContent = `+${cands.length - 6} more`;
            list.appendChild(more);
        }
        panel.appendChild(list);
        return;
    }

    if (rejected.length > 0) {
        const label = document.createElement("div");
        label.className = "panel-label";
        label.textContent = "Try to merge the leftovers \u2014 look them up:";
        panel.appendChild(label);

        const list = document.createElement("div");
        list.className = "cand-list";
        rejected.forEach((r) => {
            const pill = document.createElement("div");
            pill.className = "reject-pill";

            const pair = document.createElement("span");
            pair.className = "cand-pair";
            pair.textContent = `${visibleText(r.left)}+${visibleText(r.right)} = ${visibleText(r.glued)}`;

            const badge = document.createElement("span");
            badge.className = "reject-badge";
            badge.textContent = "\u2717 not in table";

            pill.append(pair, badge);
            list.appendChild(pill);
        });
        panel.appendChild(list);
    }
}

function renderMergeFrame(row, panel, note, frame, trace) {
    row.replaceChildren();
    frame.symbols.forEach((symbol, i) => {
        const chip = document.createElement("span");
        let cls = `explainer-chip tok-c${i % 8}`;
        if (frame.highlight && (i === frame.highlight[0] || i === frame.highlight[1])) { cls += " pair-highlight"; }
        if (frame.merged === i) { cls += " just-merged"; }
        chip.className = cls;

        const label = document.createElement("span");
        label.className = "explainer-chip-text";
        label.textContent = symbol;
        chip.appendChild(label);

        if (frame.done && trace.finalTokens[i]) {
            const id = document.createElement("span");
            id.className = "explainer-chip-id";
            id.textContent = `id ${trace.finalTokens[i].id}`;
            chip.appendChild(id);
        }

        row.appendChild(chip);
    });
    renderDecisionPanel(panel, frame);
    setRichText(note, frame.note);
}

function buildMergeAnimation(stage) {
    const trace = currentMergeTrace();
    const wrap = document.createElement("div");
    wrap.className = "merge-stack";
    const row = document.createElement("div");
    row.className = "explainer-row";
    const panel = document.createElement("div");
    panel.className = "merge-panel";
    const note = document.createElement("div");
    note.className = "explainer-substep";
    wrap.append(row, panel, note);
    stage.appendChild(wrap);

    const frames = buildMergeFrames(trace);
    scheduleFrames(frames, (frame) => renderMergeFrame(row, panel, note, frame, trace));
}

function currentPhraseTrace() {
    const t = state.phraseTrace;
    return t && Array.isArray(t.words) && t.words.length > 0 ? t : MERGE_PHRASE_FALLBACK;
}

// Build a cell list for one phrase frame: already-finished words as locked token chips, the
// active word's evolving pieces, and upcoming words as faded plain text. `extra` may carry
// `candidates`/`rejected` for the decision panel.
function phraseFrame(locked, activeSymbols, highlight, mergedIdx, future, color, note, extra) {
    const cells = [];
    locked.forEach((c) => cells.push({ kind: "locked", text: c.text, id: c.id, color: c.color }));
    activeSymbols.forEach((s, i) => cells.push({
        kind: "active",
        text: s,
        color,
        highlight: Boolean(highlight) && (i === highlight[0] || i === highlight[1]),
        merged: mergedIdx === i,
    }));
    future.forEach((f) => cells.push({ kind: "future", text: f }));
    return { cells, note, candidates: extra && extra.candidates, rejected: extra && extra.rejected };
}

function buildPhraseFrames(phrase) {
    const frames = [];
    const locked = [];

    phrase.words.forEach((wt, w) => {
        const color = w;
        const future = phrase.words.slice(w + 1).map((x) => x.word);
        let symbols = wt.word.split("");

        frames.push(phraseFrame(locked.slice(), symbols.slice(), null, null, future, color,
            w === 0
                ? `Word 1: \u201c${visibleText(wt.word)}\u201d \u2014 each word is tokenised on its own.`
                : `Next word \u201c${visibleText(wt.word)}\u201d \u2014 its leading space (\u00B7) is part of the word and merges in too.`));

        wt.steps.forEach((step, s) => {
            let idx = -1;
            for (let i = 0; i < symbols.length - 1; i++) {
                if (symbols[i] === step.left && symbols[i + 1] === step.right) { idx = i; break; }
            }
            if (idx < 0) { return; }
            frames.push(phraseFrame(locked.slice(), symbols.slice(), [idx, idx + 1], null, future, color,
                `Round ${s + 1}: smallest rank wins \u2192 ${q(step.left)}+${q(step.right)} (${step.rank}).`,
                { candidates: step.candidates }));
            symbols = symbols.slice(0, idx).concat([step.result], symbols.slice(idx + 2));
            frames.push(phraseFrame(locked.slice(), symbols.slice(), null, idx, future, color,
                `Merge \u2192 ${q(step.result)}.`));
        });

        const rejected = Array.isArray(wt.rejectedPairs) ? wt.rejectedPairs : [];
        if (symbols.length > 1 && rejected.length > 0) {
            frames.push(phraseFrame(locked.slice(), symbols.slice(), [0, symbols.length - 1], null, future, color,
                "Leftovers glued together aren't in the table, so they can't merge \u2014 stop.",
                { rejected }));
        }

        wt.finalTokens.forEach((ft) => locked.push({ text: ft.text, id: ft.id, color }));
        frames.push(phraseFrame(locked.slice(), [], null, null, future, color,
            symbols.length > 1
                ? `${q(wt.word)} can't merge further \u2192 ${wt.finalTokens.length} tokens.`
                : `${q(wt.word)} \u2192 ${wt.finalTokens.length} token.`));
    });

    frames.push({
        cells: locked.map((c) => ({ kind: "locked", text: c.text, id: c.id, color: c.color })),
        note: `${phrase.words.length} words \u2192 ${locked.length} tokens. Word count \u2260 token count!`,
    });
    return frames;
}

function renderPhraseFrame(row, panel, note, frame) {
    row.replaceChildren();
    frame.cells.forEach((cell) => {
        if (cell.kind === "future") {
            const span = document.createElement("span");
            span.className = "phrase-future";
            span.textContent = visibleText(cell.text);
            row.appendChild(span);
            return;
        }

        const chip = document.createElement("span");
        let cls = `explainer-chip tok-c${cell.color % 8}`;
        if (cell.kind === "locked") { cls += " phrase-locked"; }
        if (cell.highlight) { cls += " pair-highlight"; }
        if (cell.merged) { cls += " just-merged"; }
        chip.className = cls;

        const label = document.createElement("span");
        label.className = "explainer-chip-text";
        label.textContent = visibleText(cell.text);
        chip.appendChild(label);

        if (cell.id !== null && cell.id !== undefined) {
            const id = document.createElement("span");
            id.className = "explainer-chip-id";
            id.textContent = `id ${cell.id}`;
            chip.appendChild(id);
        }

        row.appendChild(chip);
    });
    renderDecisionPanel(panel, frame);
    setRichText(note, frame.note);
}

function buildPhraseAnimation(stage) {
    const phrase = currentPhraseTrace();
    const wrap = document.createElement("div");
    wrap.className = "merge-stack";
    const row = document.createElement("div");
    row.className = "explainer-row";
    const panel = document.createElement("div");
    panel.className = "merge-panel";
    const note = document.createElement("div");
    note.className = "explainer-substep";
    wrap.append(row, panel, note);
    stage.appendChild(wrap);

    const frames = buildPhraseFrames(phrase);
    scheduleFrames(frames, (frame) => renderPhraseFrame(row, panel, note, frame));
}

function explainerClearSub() {
    explainer.subTimers.forEach((timer) => clearTimeout(timer));
    explainer.subTimers = [];
}

function explainerRender() {
    explainerClearSub();
    const step = EXPLAINER_STEPS[explainer.index];
    const stage = el("explainerStage");
    stage.replaceChildren();
    step.build(stage);
    const caption = typeof step.caption === "function" ? step.caption() : step.caption;
    setRichText(el("explainerCaption"), caption);

    const dots = el("explainerDots");
    dots.replaceChildren();
    EXPLAINER_STEPS.forEach((_, i) => {
        const dot = document.createElement("span");
        dot.className = `dot${i === explainer.index ? " active" : ""}`;
        dots.appendChild(dot);
    });

    el("explainerPrev").disabled = explainer.index === 0;
    el("explainerNext").disabled = explainer.index === EXPLAINER_STEPS.length - 1;
}

function explainerStopAuto() {
    explainerClearSub();
}

function explainerGo(index) {
    explainer.index = Math.max(0, Math.min(EXPLAINER_STEPS.length - 1, index));
    explainerRender();
}

function explainerReplay() {
    explainerStopAuto();
    explainer.index = 0;
    explainerRender();
}

async function loadMergeTrace() {
    const model = selectedModel();
    if (!model) {
        state.mergeTrace = null;
        state.phraseTrace = null;
        return;
    }
    try {
        state.mergeTrace = await postJson("/api/merge-trace", { modelId: model.id, word: "lowest" });
    } catch {
        state.mergeTrace = null;
    }
    try {
        state.phraseTrace = await postJson("/api/merge-trace-phrase", { modelId: model.id, phrase: "I love tokenization" });
    } catch {
        state.phraseTrace = null;
    }
}

async function openExplainer() {
    explainerStopAuto();
    explainer.index = 0;
    await loadMergeTrace();
    el("explainer").classList.remove("hidden");
    document.body.classList.add("modal-open");
    el("explainerClose").focus();
    explainerRender();
}

function closeExplainer() {
    explainerStopAuto();
    el("explainer").classList.add("hidden");
    document.body.classList.remove("modal-open");
    el("explainerOpen").focus();
}

function wireExplainer() {
    el("explainerOpen").addEventListener("click", openExplainer);
    el("explainerClose").addEventListener("click", closeExplainer);
    el("explainer").addEventListener("click", (event) => {
        if (event.target === el("explainer")) {
            closeExplainer();
        }
    });
    el("explainerPrev").addEventListener("click", () => { explainerStopAuto(); explainerGo(explainer.index - 1); });
    el("explainerNext").addEventListener("click", () => { explainerStopAuto(); explainerGo(explainer.index + 1); });
    el("explainerPlay").addEventListener("click", explainerReplay);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !el("explainer").classList.contains("hidden")) {
            closeExplainer();
        }
    });
}

// Pre-baked example prompts spanning creative, coding and scientific-research registers, with a
// complexity rating shown in the dropdown. Chosen to exercise tokenization: multilingual scripts,
// emoji, symbols, code, and dense technical vocabulary.
const SAMPLE_PROMPTS = [
    {
        title: "Wandering astronomer (multilingual + emoji)",
        complexity: "Complex",
        text: `Tell me a short story about a wandering astronomer who follows strange signs across deserts, forests, and ruined cities. Along the way, they encounter unusual symbols carved into stone — some in unfamiliar scripts like καλημέρα, здравствуй, 不思議, and مرحبا — and they try to interpret what each one means. These are examples - use any script and word you wish. The journey should include moments of calm, moments of danger, and at least one surreal encounter with a creature that defies normal description. Feel free to use expressive language, complex scientific language, occasional emojis (🌒✨🌀🔥), and varied punctuation such as “!? — … !!”. At some point, the astronomer discovers a mysterious sequence of numbers etched into metal: 41‑09‑77‑203‑Δ‑Ω. End the story with the astronomer realising something profound about their place in the universe.`,
    },
    {
        title: "Token, defined",
        complexity: "Simple",
        text: `What is a token in a large language model? Answer in two sentences.`,
    },
    {
        title: "Haiku about the night sky",
        complexity: "Simple",
        text: `Write a haiku about the night sky.`,
    },
    {
        title: "Explain a rainbow to a child",
        complexity: "Simple",
        text: `Explain how a rainbow forms, in language a 10-year-old would understand.`,
    },
    {
        title: "Polite email rewrite",
        complexity: "Moderate",
        text: `Rewrite this message to be polite and professional, keeping it under 60 words: "hey, need that report today, you're late again."`,
    },
    {
        title: "Python: nth Fibonacci",
        complexity: "Moderate",
        text: `Write a Python function nth_fibonacci(n) that returns the nth Fibonacci number. Include a docstring, handle n = 0 and negative input, and add two doctests.`,
    },
    {
        title: "Node.js: callbacks → async/await",
        complexity: "Moderate",
        text: `Refactor this callback-based Node.js code to use async/await with proper error handling:

fs.readFile('a.txt', (err, a) => {
  if (err) throw err;
  fs.readFile('b.txt', (err, b) => {
    if (err) throw err;
    console.log(a + b);
  });
});`,
    },
    {
        title: "Go: thread-safe generic LRU cache",
        complexity: "Complex",
        text: `Implement a thread-safe, generic LRU cache in Go using generics. Provide Get and Put with O(1) average time using a hash map plus a doubly linked list, make it safe for concurrent use, and explain the time and space complexity of each operation.`,
    },
    {
        title: "CRISPR-Cas9 mechanism",
        complexity: "Complex",
        text: `Explain the molecular mechanism of CRISPR-Cas9 genome editing, including guide-RNA targeting, PAM recognition, double-strand break formation, and the difference between the NHEJ and HDR repair pathways. Discuss the main sources of off-target activity and one strategy to reduce it.`,
    },
    {
        title: "Derive the Friedmann equations",
        complexity: "Advanced",
        text: `Starting from the Einstein field equations with a Friedmann–Lemaître–Robertson–Walker metric, derive the two Friedmann equations for a homogeneous, isotropic universe. Discuss the role of the cosmological constant Λ in late-time accelerated expansion and relate it to the equation-of-state parameter w of dark energy.`,
    },
    {
        title: "Oxidative phosphorylation",
        complexity: "Advanced",
        text: `Describe oxidative phosphorylation in eukaryotic mitochondria: the electron transport chain (complexes I–IV), the chemiosmotic coupling hypothesis, generation of the proton-motive force, and ATP synthesis by F₀F₁-ATP synthase. Include approximate proton stoichiometry and the ATP yield per NADH and FADH₂.`,
    },
];

function populateSamplePrompts() {
    const select = el("samplePrompt");
    SAMPLE_PROMPTS.forEach((sample, i) => {
        const option = document.createElement("option");
        option.value = String(i);
        option.textContent = `${sample.title} \u2014 ${sample.complexity}`;
        select.appendChild(option);
    });
}

function applySamplePrompt() {
    const select = el("samplePrompt");
    const sample = SAMPLE_PROMPTS[Number(select.value)];
    select.value = ""; // reset so the same sample can be re-selected
    if (!sample) {
        return;
    }
    el("prompt").value = sample.text;
    tokenizeLive();
}

async function init() {
    el("model").addEventListener("change", () => {
        renderBadges(selectedModel());
        renderLogprobPlaceholder(selectedModel());
        tokenizeLive();
    });
    el("prompt").addEventListener("input", tokenizeLive);
    el("samplePrompt").addEventListener("change", applySamplePrompt);
    populateSamplePrompts();
    el("run").addEventListener("click", runModel);
    el("generateImage").addEventListener("click", generateImage);
    el("cacheDemo").addEventListener("click", cacheDemo);
    el("billingModel").addEventListener("change", () => {
        state.billingModelId = el("billingModel").value;
        renderCredits();
    });
    el("githubOverhead").addEventListener("input", renderCredits);
    el("studioOverhead").addEventListener("input", renderCredits);
    wireExplainer();

    loadCreditRates();

    try {
        const response = await fetch("/api/models");
        const models = await response.json();
        if (!models.length) {
            setStatus("No models available. Configure Azure Foundry or start Foundry Local.", true);
            return;
        }
        populateModels(models);
        renderBadges(selectedModel());
        renderLogprobPlaceholder(selectedModel());
        setStatus(`${models.length} model(s) available.`);
    } catch (err) {
        setStatus(`Failed to load models: ${err.message}`, true);
    }
}

init();
