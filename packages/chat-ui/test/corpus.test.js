// D3SKHAND's own corpora (spec/corpus): labels, tool summaries and markdown. D3sk.ChatUi passes them too.
process.env.TZ = "UTC";
import { test } from "node:test";
import assert from "node:assert/strict";
import { readJson } from "./helpers.js";
import spec from "../src/generated/spec.js";
import {
  chatStatusLabel, workingActivity, workingPhrase, workingWord, QUEUED_LINE, SENDING_LINE, TERMINAL_WORKING_LINE,
} from "../src/labels.js";
import { toolSummary } from "../src/tools.js";
import { renderMarkdown } from "../src/markdown.js";

const cases = (name) => readJson("spec", "corpus", "ui", `${name}.json`).cases;
const NOW = 1_700_000_000;

test("status pills", () => {
  for (const c of cases("status-pills")) assert.deepEqual(chatStatusLabel(c.input, NOW), c.output, JSON.stringify(c.input));
});
test("working activity", () => {
  for (const c of cases("working-activity")) assert.equal(workingActivity(c.input), c.output, String(c.input));
});
test("working phrase", () => {
  for (const c of cases("working-phrase")) assert.equal(workingPhrase(c.input), c.output, JSON.stringify(c.input));
});
test("working word", () => {
  for (const c of cases("working-word")) assert.equal(workingWord(c.input), c.output, String(c.input));
});
test("working lists equal the spec", () => {
  const [{ output }] = cases("working-lists");
  assert.deepEqual(spec.labels.working.phrases, output.phrases);
  assert.deepEqual(spec.labels.working.longHaul, output.longHaul);
  assert.deepEqual(spec.labels.working.words, output.words);
  assert.equal(QUEUED_LINE, output.queued);
  assert.equal(SENDING_LINE, output.sending);
  assert.equal(TERMINAL_WORKING_LINE, output.terminal);
});
// The corpus was captured with the project at /work/app (as D3sk.ChatUi's ToolSummaryTests).
test("tool summary", () => {
  for (const c of cases("tool-summary")) {
    assert.equal(toolSummary(c.input.kind, c.input.args, "/work/app"), c.output, JSON.stringify(c.input));
  }
});
test("markdown corpus", () => {
  const corpus = readJson("spec", "corpus", "markdown", "cases.json");
  assert.ok(corpus.cases.length >= corpus.min_cases);
  for (const c of corpus.cases) {
    const html = renderMarkdown(c.input);
    if (c.html !== undefined) assert.equal(html, c.html, c.name);
    for (const part of c.includes || []) assert.ok(html.includes(part), `${c.name}: has ${part}`);
    for (const part of c.excludes || []) assert.ok(!html.includes(part), `${c.name}: lacks ${part}`);
  }
});
