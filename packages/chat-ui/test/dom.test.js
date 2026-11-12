// The web chat draws exactly the conformance snapshots: every case is replayed into a mounted
// chat (happy-dom) and, at each snapshot, the page's rows, parts, badges and working line are read
// back and compared with the stored snapshot. Then the host API: send, stop, approvals, resume.
import { after, test } from "node:test";
import assert from "node:assert/strict";
import { Window } from "happy-dom";
import { mountChat } from "../src/chat.js";
import { renderMarkdown } from "../src/markdown.js";
import { caseAgents, caseFiles, readCase, replay } from "../scripts/conformance-lib.mjs";

const mounted = [];
after(async () => {
  for (const { chat, window } of mounted) {
    try { chat.destroy(); } catch { /* destroyed by its test */ }
    await window.happyDOM.close();
  }
});

function mount(options = {}) {
  const window = new Window({ url: "http://localhost/" });
  const container = window.document.createElement("div");
  window.document.body.append(container);
  const clock = { now: 0 };
  const chat = mountChat(container, { clock: () => clock.now, ...options });
  mounted.push({ chat, window });
  return { window, container, chat, clock };
}

const text = (node) => node?.textContent ?? null;

/** What the page shows, in the snapshot's shape, read from the DOM only. */
function readPage(chat) {
  const box = chat.elements.messages;
  const rows = [];
  for (const node of box.children) {
    if (node.classList.contains("msg-notice")) {
      rows.push({ kind: "notice", text: node.textContent, transient: node.classList.contains("transient") });
    } else if (node.classList.contains("msg-user")) {
      rows.push({ kind: "user", text: text(node.querySelector(".msg-body")), attachments: [...node.querySelectorAll(".attachment-chip")].map(text) });
    } else if (node.classList.contains("msg-error")) {
      rows.push({ kind: "error", label: text(node.querySelector(".msg-meta")), text: text(node.querySelector(".msg-body:not(.transcript)")), parts: readParts(node.querySelector(".msg-activity")) });
    } else if (node.classList.contains("msg-assistant")) {
      const meta = node.querySelector(".msg-agent");
      const body = node.querySelector(".msg-body");
      rows.push({
        kind: "assistant",
        label: meta.textContent,
        showLabel: !meta.hidden,
        brand: [...node.querySelector(".working-glyph").classList].find((name) => name.startsWith("brand-")).slice(6),
        working: body.classList.contains("typing")
          ? { text: text(node.querySelector(".working-text")), queued: node.querySelector(".working").classList.contains("queued") }
          : null,
        parts: readParts(body),
      });
    }
  }
  const pendingNode = box.querySelector(".pending-send");
  return { rows, pending: pendingNode ? text(pendingNode.querySelector(".working-text")) : null };
}

function readParts(body) {
  if (!body) return [];
  return [...body.children].map((part) => {
    if (part.classList.contains("part-text")) return { type: "text", html: part.innerHTML };
    if (part.classList.contains("part-thinking")) {
      return { type: "thinking", label: text(part.querySelector(".thinking-label")), text: text(part.querySelector(".part-pre")), open: part.open };
    }
    const status = ["running", "ok", "error", "unknown"].find((name) => part.classList.contains(name));
    const approval = part.querySelector(".tool-approval");
    const outRow = [...part.querySelectorAll(".io-row")].find((row) => text(row.querySelector(".io-label")) === "OUT");
    return {
      type: "tool",
      name: text(part.querySelector(".tool-name")),
      summary: text(part.querySelector(".tool-summary")),
      status,
      statusLabel: part.querySelector(".part-dot").getAttribute("aria-label"),
      compact: part.tagName === "DETAILS",
      approval: approval ? approval.textContent : null,
      injection: text(part.querySelector(".tool-injection")),
      fix: Boolean(part.querySelector(".tool-fix")),
      diffLines: part.querySelectorAll(".diff-line").length,
      outputShown: !outRow.hidden,
    };
  });
}

/** The same reading, taken from a snapshot instead of the page. */
function expectedPage(snapshot, { hasFix }) {
  const parts = (list) => list.map((part) => {
    if (part.type === "text") return { type: "text", html: renderMarkdown(part.text) };
    if (part.type === "thinking") return { type: "thinking", label: part.label, text: part.text, open: part.open };
    return {
      type: "tool",
      name: part.name,
      summary: part.summary,
      status: part.status,
      statusLabel: part.statusLabel,
      compact: part.compact,
      approval: part.approval ? part.approval.label : null,
      injection: part.injection ? part.injection.label : null,
      fix: hasFix && part.fixable,
      diffLines: part.diff ? Math.min(part.diff.length, 6) : 0,
      outputShown: part.outputShown,
    };
  });
  return {
    rows: snapshot.rows.map((row) => {
      if (row.kind === "notice") return { kind: "notice", text: row.text, transient: row.transient };
      if (row.kind === "user") return { kind: "user", text: row.text, attachments: row.attachments };
      if (row.kind === "error") return { kind: "error", label: row.label, text: row.text, parts: parts(row.parts) };
      return { kind: "assistant", label: row.label, showLabel: row.showLabel, brand: row.brand, working: row.working, parts: parts(row.parts) };
    }),
    pending: snapshot.pending ? snapshot.pending.working.text : null,
  };
}

for (const name of caseFiles()) {
  test(`the page draws the snapshots of ${name}`, () => {
    const testCase = readCase(name);
    const { chat, clock } = mount({ agents: caseAgents(testCase), projectRoot: testCase.projectRoot || "", onToolFix: async () => {} });
    replay(testCase, {
      session: chat.session,
      onSnapshot: (snapshotName, at) => {
        clock.now = at;
        chat.render();
        const stored = testCase.snapshots[snapshotName];
        assert.deepEqual(readPage(chat), expectedPage(stored, { hasFix: true }), `${name} › ${snapshotName}`);
        const dialog = chat.elements.dialog;
        const isOpen = dialog.open || dialog.hasAttribute("open");
        assert.equal(isOpen, stored.approvals.length > 0, `${name} › ${snapshotName}: dialog`);
        if (stored.approvals.length) assert.equal(text(chat.elements.title), stored.approvals[0].title);
      },
    });
  });
}

const AGENTS = [{ id: "claude", name: "Claude Code", models: [{ id: "claude-opus-4-7", name: "Opus 4.7" }] }];

test("Send shows the message at once, then the host's start replaces it", async () => {
  const sent = [];
  const { chat } = mount({ agents: AGENTS, onSend: async (message) => { sent.push(message); } });
  chat.composer.setText("Hello there");
  await chat.composer.send();
  chat.render();
  assert.equal(sent.length, 1);
  assert.equal(sent[0].text, "Hello there");
  assert.equal(sent[0].command, "claude");
  assert.ok(chat.elements.messages.querySelector(".pending-send"));
  chat.feed("start", { user_message: { id: "u1", content: "Hello there" }, agent: "claude", model: "claude-opus-4-7" });
  chat.render();
  assert.equal(chat.elements.messages.querySelector(".pending-send"), null);
  assert.equal(chat.elements.messages.querySelector(".msg-user .msg-body").textContent, "Hello there");
});

test("A failed send gives the words back and drops the pending row", async () => {
  const { chat } = mount({ agents: AGENTS, onSend: async () => { throw new Error("offline"); } });
  chat.composer.setText("Will fail");
  await chat.composer.send();
  chat.render();
  assert.equal(chat.composer.getText(), "Will fail");
  assert.equal(chat.elements.messages.querySelector(".pending-send"), null);
});

test("Stop shows while a turn runs and calls the host", () => {
  let stopped = 0;
  const { chat } = mount({ agents: AGENTS, onStop: () => { stopped += 1; } });
  assert.equal(chat.composer.elements.stop.hidden, true);
  chat.feed("start", { user_message: { id: "u1", content: "Go" }, agent: "claude", model: "claude-opus-4-7" });
  chat.render();
  assert.equal(chat.composer.elements.stop.hidden, false);
  chat.composer.elements.stop.click();
  assert.equal(stopped, 1);
  chat.feed("done", { returncode: 0 });
  chat.render();
  assert.equal(chat.composer.elements.stop.hidden, true);
});

test("Allow answers the host and closes the dialog; Later puts it behind the banner", async () => {
  const answers = [];
  const { chat } = mount({ agents: AGENTS, onApproval: async (answer) => { answers.push(answer); } });
  chat.feed("start", { user_message: { id: "u1", content: "Go" }, agent: "claude", model: "claude-opus-4-7" });
  chat.feed("tool_call", { call_id: "t1", name: "Bash", arguments: { command: "make" } });
  chat.feed("approval_request", { approval_id: "a1", call_id: "t1", name: "Bash", arguments: { command: "make" } });
  chat.render();
  const { dialog, banner, later, allow } = chat.elements;
  assert.ok(dialog.open || dialog.hasAttribute("open"));
  later.click();
  assert.equal(banner.hidden, false);
  assert.ok(!(dialog.open || dialog.hasAttribute("open")));
  chat.elements.messages.querySelector(".tool-approval.waiting").click();
  assert.ok(dialog.open || dialog.hasAttribute("open"));
  chat.elements.message.value = "go ahead";
  allow.click();
  await new Promise((resolve) => setTimeout(resolve, 0));
  chat.render();
  assert.deepEqual(answers, [{ approvalId: "a1", allow: true, message: "go ahead" }]);
  assert.ok(!(dialog.open || dialog.hasAttribute("open")));
});

test("A question is answered with the picked options and the own words", async () => {
  const answers = [];
  const { chat } = mount({ agents: AGENTS, onApproval: async (answer) => { answers.push(answer); } });
  const args = { questions: [
    { question: "Which database?", multiSelect: false, options: [{ label: "SQLite" }, { label: "Postgres" }] },
    { question: "Extras?", multiSelect: true, options: [{ label: "Auth" }, { label: "Billing" }] },
  ] };
  chat.feed("start", { user_message: { id: "u1", content: "Go" }, agent: "claude" });
  chat.feed("approval_request", { approval_id: "q", call_id: "c", name: "AskUserQuestion", arguments: args });
  chat.render();
  const { detail, send } = chat.elements;
  assert.equal(send.disabled, true);
  const blocks = detail.querySelectorAll(".question-block");
  blocks[0].querySelectorAll(".question-option")[1].click();
  blocks[1].querySelectorAll(".question-option")[0].click();
  blocks[1].querySelector(".question-other").click();
  const own = blocks[1].querySelector(".question-own");
  own.value = "Search";
  own.dispatchEvent(new own.ownerDocument.defaultView.Event("input"));
  assert.equal(send.disabled, false);
  send.click();
  await new Promise((resolve) => setTimeout(resolve, 0));
  assert.deepEqual(answers, [{ approvalId: "q", allow: true, answers: { "Which database?": "Postgres", "Extras?": ["Auth", "Search"] } }]);
});

test("SSE frames are read from a response body", async () => {
  const { chat } = mount({ agents: AGENTS });
  const body = [
    'event: start\ndata: {"user_message": {"id": "u1", "content": "Hi"}, "agent": "claude", "model": "claude-opus-4-7"}\n\n',
    'event: text\ndata: {"v": 1, "text": "Hel',
    'lo"}\n\nevent: done\ndata: {"v": 1, "returncode": 0}\n\n',
  ];
  const stream = new ReadableStream({ start(controller) { for (const chunk of body) controller.enqueue(new TextEncoder().encode(chunk)); controller.close(); } });
  await chat.readSse({ body: stream });
  const snapshot = chat.session.snapshot(1);
  assert.equal(snapshot.busy, false);
  assert.deepEqual(snapshot.rows.map((row) => row.kind), ["user", "assistant"]);
  assert.equal(snapshot.rows[1].parts[0].text, "Hello");
});

test("Palettes switch on the root from the spec tokens", () => {
  const { chat } = mount();
  assert.equal(chat.setPalette("dusk"), "dusk");
  assert.equal(chat.elements.root.style.getPropertyValue("--bg"), "#1c1614");
  assert.equal(chat.setPalette("nope"), "teal");
  assert.equal(chat.elements.root.style.getPropertyValue("--bg"), "#13242d");
});

test("Agent text is escaped: no script reaches the page", () => {
  const { chat } = mount({ agents: AGENTS });
  chat.feed("text", { text: "<img src=x onerror=alert(1)> [x](javascript:alert(1))" });
  chat.render();
  const part = chat.elements.messages.querySelector(".part-text");
  assert.equal(part.querySelector("img"), null);
  assert.equal(part.querySelector("a"), null);
});
