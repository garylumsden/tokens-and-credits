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

const opus = { id: "claude-opus-4.8", label: "Claude Opus 4.8", input: 500, cacheRead: 50, cacheWrite: 625, output: 2500 };
const studio = { basic: 0.1, standard: 1.5, premium: 10 };

// 1) Token-class mapping + reasoning billed as output, no overhead.
{
    const usage = { prompt: 1_000_000, cached: 200_000, output: 1_000_000, reasoning: 500_000, total: 2_500_000 };
    const r = computeCredits(usage, opus, studio, 0);
    assert.equal(r.github.input, 400);       // 0.8M * 500
    assert.equal(r.github.cacheRead, 10);    // 0.2M * 50
    assert.equal(r.github.cacheWrite, 0);    // always 0
    assert.equal(r.github.output, 3750);     // 1.5M * 2500 (incl. reasoning)
    assert.equal(r.github.total, 4160);
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

// 3) Copilot Studio tiers apply per 1000 tokens to (total + studio overhead).
{
    const usage = { prompt: 0, cached: 0, output: 0, reasoning: 0, total: 10_000 };
    const r = computeCredits(usage, opus, studio, 0, 0);
    assert.equal(r.studio.basic, 1);
    assert.equal(r.studio.standard, 15);
    assert.equal(r.studio.premium, 100);
}

// 4) Switching billing model changes only the GitHub figure, not Studio.
{
    const usage = { prompt: 1_000_000, cached: 0, output: 1_000_000, reasoning: 0, total: 2_000_000 };
    const mini = { id: "gpt-5-mini", label: "GPT-5 mini", input: 25, cacheRead: 2.5, cacheWrite: 0, output: 200 };
    const a = computeCredits(usage, opus, studio, 0, 0);
    const b = computeCredits(usage, mini, studio, 0, 0);
    assert.equal(a.github.total, 3000);
    assert.equal(b.github.total, 225);
    assert.deepEqual(a.studio, b.studio);
}

console.log("credits.test.mjs: all assertions passed");
