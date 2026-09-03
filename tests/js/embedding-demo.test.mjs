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
assert.match(indexHtml, /A Synopsis of Linguistic Theory/);
assert.match(indexHtml, /pp\. 1&ndash;32, p\. 11/);
assert.match(indexHtml, /https:\/\/languagelog\.ldc\.upenn\.edu\/myl\/Firth1957\.pdf/);
assert.match(indexHtml, /target="_blank" rel="noopener noreferrer"/);
assert.match(indexHtml, /id="embeddingStaticSource"/);
assert.match(indexHtml, /id="embeddingLiveSource"/);
assert.match(indexHtml, /id="embeddingTerminologyTechnical"/);
assert.match(indexHtml, /id="embeddingTerminologyAnalogy"/);
assert.match(indexHtml, /Technical terms/);
assert.match(indexHtml, /Plain language/);
assert.doesNotMatch(indexHtml, /id="embeddingSteps"/);
assert.doesNotMatch(indexHtml, /id="embeddingPrev"/);
assert.doesNotMatch(indexHtml, /id="embeddingNext"/);

assert.match(appJs, /\/api\/embeddings\/manifest/);
assert.match(appJs, /\/api\/embeddings\/nearest-neighbours/);
assert.match(appJs, /\/api\/embeddings\/relationship/);
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
assert.match(appJs, /No measurement yet\. Press the button/);
assert.match(appJs, /No search yet\. Examples fill the fields only/);
assert.match(appJs, /No model call yet\. Generate embeddings when you are ready\./);
assert.match(appJs, /What to take away/);
assert.match(appJs, /embeddingDemo\.liveRequest = \{ \.\.\.LIVE_EMBEDDING_DEFAULTS \};[\s\S]*state\.embeddingRelationshipResult = null;[\s\S]*state\.embeddingAnalogyResult = null;[\s\S]*state\.liveEmbeddingResult = null;/);
assert.match(appJs, /trapEmbeddingFocus/);
assert.match(appJs, /prefers-reduced-motion|candidate-bar/);
assert.match(appJs, /let elapsed = 900;/);
assert.match(appJs, /return 1700;/);

const presetBlock = appJs.match(/const EMBEDDING_PRESETS = \[[\s\S]*?\n\];/);
assert.ok(presetBlock, "could not locate EMBEDDING_PRESETS");
for (const expected of ["king", "queen", "father", "mother", "paris", "rome"]) {
    assert.match(presetBlock[0], new RegExp(`"${expected}"`));
}

const openBlock = appJs.match(/async function openEmbeddingExplainer\(\) \{[\s\S]*?\n\}/);
assert.ok(openBlock, "could not locate openEmbeddingExplainer");
assert.doesNotMatch(openBlock[0], /runEmbeddingAnalogy|runEmbeddingRelationship|runLiveEmbeddingCompare/);

console.log("embedding-demo.test.mjs: all assertions passed");
