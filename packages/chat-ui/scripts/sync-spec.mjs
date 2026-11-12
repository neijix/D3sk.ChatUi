// Copies the shared spec (../../spec/chat-ui.json) into the package as an ES module, so the
// browser loads it with no JSON import attribute and npm ships it. Runs before test and pack;
// the generated files are gitignored: spec/chat-ui.json at the repo root is the only source.
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const source = join(here, "..", "..", "..", "spec", "chat-ui.json");
const text = readFileSync(source, "utf8");
const spec = JSON.parse(text);
mkdirSync(join(here, "..", "src", "generated"), { recursive: true });
writeFileSync(
  join(here, "..", "src", "generated", "spec.js"),
  `// Generated from spec/chat-ui.json by scripts/sync-spec.mjs: edit that file, not this one.\nexport default Object.freeze(${JSON.stringify(spec, null, 2)});\n`,
);
mkdirSync(join(here, "..", "spec"), { recursive: true });
writeFileSync(join(here, "..", "spec", "chat-ui.json"), text);
