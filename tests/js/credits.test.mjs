// Standalone test for the frontend credit maths in wwwroot/app.js.
// Run with:  node tests/js/credits.test.mjs
// Extracts computeCredits from app.js (without executing the browser bootstrap) and asserts the
// token-class mapping, overhead handling, and Copilot Studio tiers. Mirrors CreditEstimatorTests.cs.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const appJs = readFileSync(resolve(here, "../../src/TokensAndCredits.Web/wwwroot/app.js"), "utf8");

// The function's closing brace sits in column 0; inner object braces are indented.
const match = appJs.match(/function computeCredits\([\s\S]*?\r?\n}\r?\n/);
assert.ok(match, "could not locate computeCredits in app.js");
// eslint-disable-next-line no-new-func
const computeCredits = new Function(`${match[0]}; return computeCredits;`)();
const formatMatch = appJs.match(/function formatCredits\([\s\S]*?\r?\n}\r?\n/);
assert.ok(formatMatch, "could not locate formatCredits in app.js");
// eslint-disable-next-line no-new-func
const formatCredits = new Function(`${formatMatch[0]}; return formatCredits;`)();

const opus = { id: "claude-opus-5", label: "Claude Opus 5", input: 500, cacheRead: 50, cacheWrite: 625, output: 2500 };
const studio = { basic: 0.1, standard: 1.5, premium: 10 };

// 1) Prompt includes fresh input, cache reads, and cache writes.
{
    const usage = { prompt: 1_000_000, cached: 200_000, output: 1_000_000, reasoning: 500_000, total: 2_500_000 };
    const r = computeCredits(usage, opus, studio, 0, 0, 300_000);
    assert.equal(r.github.inputTokens, 500_000);
    assert.equal(r.github.input, 250);       // 0.5M * 500
    assert.equal(r.github.cacheRead, 10);    // 0.2M * 50
    assert.equal(r.github.cacheWrite, 187.5);// 0.3M * 625
    assert.equal(r.github.output, 3750);     // 1.5M * 2500 (incl. reasoning)
    assert.equal(r.github.total, 4197.5);
    assert.equal(r.github.input + r.github.cacheRead + r.github.cacheWrite + r.github.output, r.github.total);
}

// 2) GitHub and Copilot Studio take INDEPENDENT overhead inputs.
{
    const usage = { prompt: 0, cached: 0, output: 0, reasoning: 0, total: 0 };
    // GitHub overhead only: adds to GitHub input, NOT to Studio total.
    const ghOnly = computeCredits(usage, opus, studio, 1_000_000, 0);
    assert.equal(ghOnly.github.input, 500);   // 1M overhead * 500
    assert.equal(ghOnly.github.total, 500);
    assert.equal(ghOnly.studio.premium, 0);   // studio overhead is 0 → no studio cost

    // Studio overhead only: adds to Studio total, NOT to GitHub input.
    const csOnly = computeCredits(usage, opus, studio, 0, 1_000_000);
    assert.equal(csOnly.github.total, 0);     // github overhead is 0
    assert.equal(csOnly.studio.premium, 10_000); // total → 1M; 1000 thousands * 10
}

// 3) Copilot Studio rounds the total up to whole 1K-token billing units.
{
    const usage = { prompt: 0, cached: 0, output: 0, reasoning: 0, total: 10_001 };
    const r = computeCredits(usage, opus, studio, 0, 0);
    assert.equal(r.studio.units, 11);
    assert.equal(r.studio.basic, 1.1);
    assert.equal(r.studio.standard, 16.5);
    assert.equal(r.studio.premium, 110);
}

// 4) The published 4,200-token example consumes five Basic units, or 0.5 credits.
{
    const usage = { prompt: 4_000, cached: 0, output: 200, reasoning: 0, total: 4_200 };
    const r = computeCredits(usage, opus, studio, 0, 0);
    assert.equal(r.studio.units, 5);
    assert.equal(r.studio.basic, 0.5);
}

// 5) Switching billing model changes only the GitHub figure, not Studio.
{
    const usage = { prompt: 1_000_000, cached: 0, output: 1_000_000, reasoning: 0, total: 2_000_000 };
    const mini = { id: "gpt-5-mini", label: "GPT-5 mini", input: 25, cacheRead: 2.5, cacheWrite: 0, output: 200 };
    const a = computeCredits(usage, opus, studio, 0, 0);
    const b = computeCredits(usage, mini, studio, 0, 0);
    assert.equal(a.github.total, 3000);
    assert.equal(b.github.total, 225);
    assert.deepEqual(a.studio, b.studio);
}

// 6) Long-context models switch rates above the published input threshold.
{
    const tiered = {
        id: "tiered",
        label: "Tiered",
        input: 100,
        cacheRead: 10,
        cacheWrite: 0,
        output: 200,
        longContextThreshold: 200_000,
        longContextInput: 200,
        longContextCacheRead: 20,
        longContextCacheWrite: 0,
        longContextOutput: 300,
    };
    const usage = { prompt: 200_001, cached: 0, output: 1_000_000, reasoning: 0, total: 1_200_001 };
    const r = computeCredits(usage, tiered, studio, 0, 0);
    assert.equal(r.github.tier, "Long context");
    assert.equal(r.github.input, 40.0002);
    assert.equal(r.github.output, 300);
    assert.equal(r.github.total, 340.0002);
}

// 7) The shared Opus 5 summary totals cannot produce 432.36 AIC at pure Opus rates.
{
    const usage = { prompt: 2_870_278, cached: 2_166_224, output: 57_891, reasoning: 0, total: 2_928_169 };
    const floor = computeCredits(usage, opus, studio, 0, 0, 0);
    const ceiling = computeCredits(usage, opus, studio, 0, 0, 704_054);
    assert.ok(Math.abs(floor.github.total - 605.0657) < 1e-10);
    assert.ok(Math.abs(ceiling.github.total - 693.07245) < 1e-10);
    assert.equal(formatCredits(floor.github.total), "605.07");
    assert.equal(formatCredits(432.36), "432.36");
}

console.log("credits.test.mjs: all assertions passed");
