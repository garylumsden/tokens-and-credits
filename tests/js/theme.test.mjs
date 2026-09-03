import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const appJs = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/app.js"), "utf8");
const indexHtml = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/index.html"), "utf8");
const styles = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/styles.css"), "utf8");
const themeJs = readFileSync(resolve(root, "src/TokensAndCredits.Web/wwwroot/theme.js"), "utf8");

assert.match(indexHtml, /<script src="theme\.js\?v=theme-\d+"><\/script>[\s\S]*<link rel="stylesheet"/);
assert.match(indexHtml, /id="themeToggle"/);
assert.match(indexHtml, /id="themeToggleIcon"[^>]*aria-hidden="true"/);
assert.match(indexHtml, /id="themeToggleLabel"/);
assert.match(indexHtml, /id="themeColor" name="theme-color"/);

assert.match(themeJs, /tokens-and-credits-theme/);
assert.match(themeJs, /prefers-color-scheme: light/);
assert.match(themeJs, /document\.documentElement\.dataset\.theme = theme/);

assert.match(appJs, /function applyTheme\(theme, persist = true\)/);
assert.match(appJs, /function wireThemeToggle\(\)/);
assert.match(appJs, /localStorage\.setItem\(THEME_STORAGE_KEY, theme\)/);
assert.match(appJs, /wireThemeToggle\(\);/);

assert.match(styles, /html\[data-theme="light"\]/);
for (const variable of ["--bg", "--card", "--card-2", "--text", "--muted", "--border", "--accent"]) {
    const occurrences = styles.match(new RegExp(variable, "g")) || [];
    assert.ok(occurrences.length >= 2, `${variable} must be defined for both themes`);
}
assert.match(styles, /button:focus-visible/);

console.log("theme.test.mjs: all assertions passed");
