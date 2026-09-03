import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const appJs = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/app.js"), "utf8");
const indexHtml = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/index.html"), "utf8");

const tableMatch = appJs.match(/const IMAGE_OUTPUT_TOKEN_ESTIMATES = (\{[\s\S]*?\n\});/);
assert.ok(tableMatch, "could not locate IMAGE_OUTPUT_TOKEN_ESTIMATES");
const estimates = Function(`"use strict"; return (${tableMatch[1]});`)();

assert.deepEqual(estimates["1024x1024"], { low: 272, medium: 1056, high: 4160 });
assert.deepEqual(estimates["1024x1536"], { low: 408, medium: 1584, high: 6240 });
assert.deepEqual(estimates["1536x1024"], { low: 400, medium: 1568, high: 6208 });
assert.equal(estimates.auto, undefined);

assert.match(indexHtml, /id="imageLocalOutputSection" class="hidden"/);
assert.match(indexHtml, /id="imageOutputEstimateCount"/);
assert.match(indexHtml, /id="imageOutputEstimateLead"/);
assert.match(indexHtml, /View the GPT Image token table/);
assert.match(indexHtml, /developers\.openai\.com\/api\/docs\/guides\/image-generation#cost-and-latency/);

assert.match(appJs, /known after generation/);
assert.match(appJs, /Prompt complexity and image file size do not change it/);
assert.match(appJs, /Unreturned text output/);
assert.match(appJs, /does not expose its content or document its purpose/);
assert.match(appJs, /Output total/);
assert.match(appJs, /outputImageTokens/);
assert.match(appJs, /outputTextTokens/);
assert.match(appJs, /Visual complexity and file size do not change the fixed image-token component/);
assert.match(appJs, /Image generation uses a separate token system/);
assert.match(appJs, /imageSize"\)\.addEventListener\("change"/);
assert.match(appJs, /imageQuality"\)\.addEventListener\("change"/);

console.log("image-output-estimate.test.mjs: all assertions passed");
