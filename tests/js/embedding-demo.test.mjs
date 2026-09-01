// Structural and copy tests for the tokenizer and embedding explainers.
// Run with: node tests/js/embedding-demo.test.mjs
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const appJs = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/app.js"), "utf8");
const indexHtml = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/index.html"), "utf8");

assert.match(indexHtml, /id="embeddingExplainer"/);
assert.match(indexHtml, /aria-labelledby="embeddingTitle"/);
assert.match(indexHtml, /aria-describedby="embeddingIntro"/);
assert.match(indexHtml, /You shall know a word by the company it keeps!/);
assert.match(indexHtml, /id="embeddingStaticSource"/);
assert.match(indexHtml, /id="embeddingLiveSource"/);

assert.match(appJs, /\/api\/embeddings\/manifest/);
assert.match(appJs, /\/api\/embeddings\/analogy/);
assert.match(appJs, /\/api\/embeddings\/providers/);
assert.match(appJs, /\/api\/embeddings\/live-compare/);
assert.match(appJs, /Nearest in the bundled vocabulary/);
assert.match(appJs, /Observed, not scripted\./);
assert.match(appJs, /Static embeddings make the early geometric idea visible\./);
assert.match(appJs, /Training creates the table once\./);
assert.match(appJs, /Runtime tokenisation applies that fixed table/);
assert.match(appJs, /A modern embedding model turns each complete text/);
assert.match(appJs, /Modern embedding models optimize semantic similarity, not classic word arithmetic/);
assert.match(appJs, /full vectors stay server-side/);
assert.match(appJs, /embeddingDemo\.liveRequest = \{ \.\.\.LIVE_EMBEDDING_DEFAULTS \};\s+state\.embeddingResult = null;\s+state\.liveEmbeddingResult = null;/);
assert.match(appJs, /trapEmbeddingFocus/);
assert.match(appJs, /prefers-reduced-motion|candidate-bar/);

const presetBlock = appJs.match(/const EMBEDDING_PRESETS = \[[\s\S]*?\n\];/);
assert.ok(presetBlock, "could not locate EMBEDDING_PRESETS");
for (const expected of ["king", "queen", "father", "mother", "paris", "rome"]) {
    assert.match(presetBlock[0], new RegExp(`"${expected}"`));
}

console.log("embedding-demo.test.mjs: all assertions passed");
