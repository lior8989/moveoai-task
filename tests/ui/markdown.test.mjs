// Unit tests for the chat UI's Markdown renderer. Run: node --test "tests/ui/*.test.mjs"
import { test } from "node:test";
import assert from "node:assert/strict";
import { renderMarkdown, escapeHtml } from "../../src/WeatherRisk.Api/wwwroot/js/markdown.js";

test("escapeHtml escapes all HTML-significant characters", () => {
  assert.equal(escapeHtml(`<a href="x">'&'</a>`), "&lt;a href=&quot;x&quot;&gt;&#39;&amp;&#39;&lt;/a&gt;");
  assert.equal(escapeHtml(null), "");
});

test("model output cannot inject markup or script", () => {
  const html = renderMarkdown(`<script>alert(1)</script> **<img src=x onerror=alert(1)>**`);
  assert.ok(!html.includes("<script>"));
  assert.ok(!html.includes("<img"));
  assert.ok(html.includes("&lt;script&gt;"));
  assert.ok(html.includes("<strong>&lt;img src=x onerror=alert(1)&gt;</strong>"));
});

test("bold, italic and code are rendered", () => {
  assert.equal(renderMarkdown("**High** and *rising* or _falling_ with `code`"),
    "<p><strong>High</strong> and <em>rising</em> or <em>falling</em> with <code>code</code></p>");
});

test("formatting inside code spans is left alone", () => {
  assert.equal(renderMarkdown("`**not bold**`"), "<p><code>**not bold**</code></p>");
});

test("bullet and numbered lists become ul/ol", () => {
  assert.equal(renderMarkdown("- a\n- b"), "<ul><li>a</li><li>b</li></ul>");
  assert.equal(renderMarkdown("1. a\n2. b"), "<ol><li>a</li><li>b</li></ol>");
  assert.equal(renderMarkdown("* a\n1. b"), "<ul><li>a</li></ul><ol><li>b</li></ol>");
});

test("headings are demoted below the page title", () => {
  assert.equal(renderMarkdown("# Title\n## Sub"), "<h3>Title</h3><h4>Sub</h4>");
});

test("paragraphs split on blank lines; single newlines become <br>", () => {
  assert.equal(renderMarkdown("one\ntwo\n\nthree"), "<p>one<br>two</p><p>three</p>");
});

test("asterisk multiplication-like text is not turned into emphasis", () => {
  assert.equal(renderMarkdown("score 2 * 3 = 6"), "<p>score 2 * 3 = 6</p>");
});

test("CRLF input and trailing list close correctly", () => {
  assert.equal(renderMarkdown("intro\r\n- x\r\n- y"), "<p>intro</p><ul><li>x</li><li>y</li></ul>");
});
