// Replays spec/conformance/cases/*.json on a session: the same steps D3sk.ChatUi's
// ConformanceTests run, so both front-ends must reach the same snapshots.
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createSession } from "../src/session.js";

export const CONFORMANCE = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "spec", "conformance");

export function caseFiles() {
  return readdirSync(join(CONFORMANCE, "cases")).filter((name) => name.endsWith(".json")).sort();
}

export function readCase(name) {
  return JSON.parse(readFileSync(join(CONFORMANCE, "cases", name), "utf8"));
}

function streamEvents(path) {
  return readFileSync(join(CONFORMANCE, path), "utf8").split("\n").filter((line) => line.trim()).map((line) => JSON.parse(line));
}

export function caseAgents(testCase) {
  return testCase.agents || JSON.parse(readFileSync(join(CONFORMANCE, "agents.json"), "utf8"));
}

/**
 * Runs a case; returns { name: snapshot } in step order. `session` defaults to a fresh one;
 * `onSnapshot(name, at)` runs at each snapshot step (the DOM test checks the page there).
 */
export function replay(testCase, { session: given, onSnapshot } = {}) {
  const session = given || createSession({ agents: caseAgents(testCase), projectRoot: testCase.projectRoot || "", clock: () => 0 });
  const snapshots = {};
  for (const step of testCase.steps) {
    const at = step.at ?? 0;
    if (step.frame) session.feed(step.frame[0], step.frame[1], at);
    else if (step.event) session.feedEvent(step.event, at);
    else if (step.stream) {
      const every = step.every ?? 0;
      let events = streamEvents(step.stream);
      if (step.skip) events = events.slice(step.skip);
      if (step.take !== undefined) events = events.slice(0, step.take);
      events.forEach((event, index) => session.feedEvent(event, at + index * every));
    } else if (step.resume) session.resume(step.resume, at);
    else if (step.sending) session.markSending(step.sending);
    else if (step.cancelSending) session.cancelSending();
    else if (step.dismissApproval) session.dismissApproval(step.dismissApproval);
    else if (step.snapshot) {
      snapshots[step.snapshot] = session.snapshot(at);
      onSnapshot?.(step.snapshot, at, snapshots[step.snapshot]);
    }
    else throw new Error(`unknown step: ${JSON.stringify(step)}`);
  }
  return snapshots;
}
