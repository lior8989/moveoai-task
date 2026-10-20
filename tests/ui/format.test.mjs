// Unit tests for the chat UI's presentation helpers. Run: node --test "tests/ui/*.test.mjs"
import { test } from "node:test";
import assert from "node:assert/strict";
import { formatNumber, formatDuration, healthClass, humanize, describeProblem } from "../../src/WeatherRisk.Api/wwwroot/js/format.js";

test("formatNumber keeps integers and rounds to 2 decimals", () => {
  assert.equal(formatNumber(70), "70");
  assert.equal(formatNumber(64.6), "64.6");
  assert.equal(formatNumber(97.8156), "97.82");
  assert.equal(formatNumber(undefined), "–");
  assert.equal(formatNumber(Number.NaN), "–");
});

test("formatDuration switches from ms to seconds at 1000", () => {
  assert.equal(formatDuration(850.4), "850 ms");
  assert.equal(formatDuration(4314), "4.3 s");
  assert.equal(formatDuration(null), "–");
});

test("healthClass maps statuses to indicator colours", () => {
  assert.equal(healthClass("Healthy"), "ok");
  assert.equal(healthClass("Degraded"), "warn");
  assert.equal(healthClass("Unhealthy"), "bad");
  assert.equal(healthClass(undefined), "bad");
});

test("humanize splits PascalCase identifiers", () => {
  assert.equal(humanize("SevereStorm"), "Severe storm");
  assert.equal(humanize("WeatherStatistics"), "Weather statistics");
  assert.equal(humanize("Ranking"), "Ranking");
});

test("describeProblem uses ProblemDetails fields and the hint", () => {
  assert.deepEqual(
    describeProblem(503, { title: "Assistant unavailable", detail: "All failed", hint: "Use /api/risk/rankings" }),
    { title: "Assistant unavailable", detail: "All failed", hint: "Use /api/risk/rankings" });
});

test("describeProblem gives friendly messages for rate limits and network errors", () => {
  assert.equal(describeProblem(429, { detail: "Retry after 10 second(s)." }).title, "Too many questions");
  assert.equal(describeProblem(0, null).title, "Network error");
  assert.equal(describeProblem(500, null).title, "Request failed (500)");
});
