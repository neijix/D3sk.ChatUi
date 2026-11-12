// Fills each case's "snapshots" from this package's session: run it after adding a case or
// changing a rule on purpose, read the diff, then make D3sk.ChatUi pass the same file.
import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { CONFORMANCE, caseFiles, readCase, replay } from "./conformance-lib.mjs";

for (const name of caseFiles()) {
  const testCase = readCase(name);
  testCase.snapshots = replay(testCase);
  writeFileSync(join(CONFORMANCE, "cases", name), `${JSON.stringify(testCase, null, 2)}\n`);
  console.log(`wrote ${name}`);
}
