// Every case of spec/conformance replays to its stored snapshots. D3sk.ChatUi's ConformanceTests
// replay the same files: a change here that the C# side does not make fails over there.
import { test } from "node:test";
import assert from "node:assert/strict";
import { caseFiles, readCase, replay } from "../scripts/conformance-lib.mjs";

for (const name of caseFiles()) {
  test(`conformance: ${name}`, () => {
    const testCase = readCase(name);
    assert.ok(Object.keys(testCase.snapshots).length, "the case has no snapshot: run npm run conformance:write");
    assert.deepEqual(replay(testCase), testCase.snapshots);
  });
}
