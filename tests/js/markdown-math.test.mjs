// Standalone tests for markdown math integration in wwwroot/app.js.
// Run with: node tests/js/markdown-math.test.mjs
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const appJs = readFileSync(resolve(here, "../../src/TokensAndCredits.Web/wwwroot/app.js"), "utf8");
const require = createRequire(import.meta.url);
const katex = require("../../src/TokensAndCredits.Web/wwwroot/vendor/katex/katex.min.js");

assert.equal(katex.version, "0.18.1");

const formula = katex.renderToString("\\frac{8\\pi G}{3}\\rho", {
    displayMode: true,
    throwOnError: false,
    trust: false,
});
assert.match(formula, /class="katex-display"/);
assert.match(formula, /<math/);

const untrusted = katex.renderToString("\\href{javascript:alert(1)}{click}", {
    throwOnError: false,
    trust: false,
});
assert.doesNotMatch(untrusted, /<a[^>]+href=/);

const renderMathMatch = appJs.match(/function renderMath\([\s\S]*?\r?\n}\r?\n/);
assert.ok(renderMathMatch, "could not locate renderMath in app.js");
// eslint-disable-next-line no-new-func
const renderMath = new Function(`${renderMathMatch[0]}; return renderMath;`)();

let capturedOptions;
globalThis.window = {
    renderMathInElement(_container, options) {
        capturedOptions = options;
    },
};
renderMath({});
delete globalThis.window;

assert.equal(capturedOptions.throwOnError, false);
assert.equal(capturedOptions.trust, false);
assert.deepEqual(
    capturedOptions.delimiters.map(({ left, right, display }) => [left, right, display]),
    [
        ["$$", "$$", true],
        ["\\[", "\\]", true],
        ["\\(", "\\)", false],
        ["$", "$", false],
    ]);
assert.ok(capturedOptions.ignoredTags.includes("code"));
assert.ok(capturedOptions.ignoredTags.includes("pre"));

const appendInlineMatch = appJs.match(/function appendInline\([\s\S]*?\r?\n}\r?\n/);
assert.ok(appendInlineMatch, "could not locate appendInline in app.js");
// eslint-disable-next-line no-new-func
const appendInline = new Function(`${appendInlineMatch[0]}; return appendInline;`)();

const createdElements = [];
globalThis.document = {
    createTextNode(text) {
        return { kind: "text", text };
    },
    createElement(tag) {
        const node = { kind: tag, textContent: "", children: [], appendChild(child) { this.children.push(child); } };
        createdElements.push(node);
        return node;
    },
};
const parent = { children: [], appendChild(child) { this.children.push(child); } };
appendInline(parent, "Use $a_i + b_i$ and **bold**.");
delete globalThis.document;

assert.equal(parent.children[1].text, "$a_i + b_i$");
assert.equal(createdElements.filter(node => node.kind === "em").length, 0);
assert.equal(createdElements.filter(node => node.kind === "strong").length, 1);

console.log("markdown-math.test.mjs: all assertions passed");
