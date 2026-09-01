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
assert.match(indexHtml, /How text embeddings work/);
assert.match(indexHtml, /id="embeddingStatus"[^>]*role="status"[^>]*hidden/);
assert.match(indexHtml, /You shall know a word by the company it keeps!/);
assert.match(indexHtml, /id="embeddingStaticSource"/);
assert.match(indexHtml, /id="embeddingLiveSource"/);
assert.match(indexHtml, /id="embeddingTerminologyTechnical"/);
assert.match(indexHtml, /id="embeddingTerminologyAnalogy"/);

assert.match(appJs, /\/api\/embeddings\/manifest/);
assert.match(appJs, /\/api\/embeddings\/analogy/);
assert.match(appJs, /\/api\/embeddings\/providers/);
assert.match(appJs, /\/api\/embeddings\/live-compare/);
assert.match(appJs, /Nearest neighbours by cosine similarity/);
assert.match(appJs, /GloVe learns word vectors from global word-word co-occurrence statistics/);
assert.match(appJs, /context-dependent hidden states/);
assert.match(appJs, /The API returns one embedding vector for each input text/);
assert.match(appJs, /Cosine similarity compares vector direction/);
assert.match(appJs, /full vectors stay server-side/);
assert.match(appJs, /terminology: "technical"/);
assert.match(appJs, /Analogy: imagine each word as a point on a map/);
assert.match(appJs, /Words learn from the company they keep/);
assert.match(appJs, /A fixed word map/);
assert.match(appJs, /The complete text receives a map location/);
assert.match(appJs, /Training creates the table once\./);
assert.match(appJs, /Runtime tokenisation applies that fixed table/);
assert.doesNotMatch(appJs, /The training data contains sentences, not a rule/);
assert.doesNotMatch(appJs, /Observed, not scripted/);
assert.doesNotMatch(appJs, /The genesis, not the full mechanism/);
assert.doesNotMatch(appJs, /Calculated \$\{request\.positiveA\}/);
assert.doesNotMatch(appJs, /Azure returned four embeddings/);
assert.match(appJs, /embeddingDemo\.liveRequest = \{ \.\.\.LIVE_EMBEDDING_DEFAULTS \};\s+state\.embeddingResult = null;\s+state\.liveEmbeddingResult = null;/);
assert.match(appJs, /trapEmbeddingFocus/);
assert.match(appJs, /prefers-reduced-motion|candidate-bar/);

const presetBlock = appJs.match(/const EMBEDDING_PRESETS = \[[\s\S]*?\n\];/);
assert.ok(presetBlock, "could not locate EMBEDDING_PRESETS");
for (const expected of ["king", "queen", "father", "mother", "paris", "rome"]) {
    assert.match(presetBlock[0], new RegExp(`"${expected}"`));
}

assert.match(
    appJs,
    /const LIVE_EMBEDDING_STEP_ORDER = \["context", "analogy", "vectors", "relationships", "bridge"\];/,
);

console.log("embedding-demo.test.mjs: all assertions passed");
