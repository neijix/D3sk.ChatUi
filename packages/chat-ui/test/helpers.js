import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const REPO = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
export const readJson = (...parts) => JSON.parse(readFileSync(join(REPO, ...parts), "utf8"));
