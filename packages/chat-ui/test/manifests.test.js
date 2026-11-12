// The copied files (AgentCli streams, D3SKHAND corpora, AgentCliBridge's box) are frozen copies.
import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { REPO } from "./helpers.js";

const PACKAGE = join(dirname(fileURLToPath(import.meta.url)), "..");

function check(dir, pattern) {
  const lines = readFileSync(join(dir, pattern.manifest), "utf8").split("\n").filter((line) => /^[0-9a-f]{64}  /.test(line));
  const listed = lines.map((line) => line.slice(66));
  for (const line of lines) {
    const file = line.slice(66);
    const hash = createHash("sha256").update(readFileSync(join(dir, file))).digest("hex");
    assert.equal(hash, line.slice(0, 64), `${file} differs from its manifest`);
  }
  const present = readdirSync(dir).filter((name) => pattern.files.test(name)).sort();
  assert.deepEqual(present, [...listed].sort());
}

test("AgentCli streams match their manifest", () => check(join(REPO, "spec", "conformance", "agentcli"), { manifest: "manifest.txt", files: /\.jsonl$/ }));
test("D3SKHAND's labels corpus matches its manifest", () => check(join(REPO, "spec", "corpus", "ui"), { manifest: "manifest.txt", files: /\.json$/ }));
test("AgentCliBridge's box matches its manifest", () => check(join(PACKAGE, "vendor", "acb"), { manifest: "MANIFEST.txt", files: /\.(js|css)$/ }));
