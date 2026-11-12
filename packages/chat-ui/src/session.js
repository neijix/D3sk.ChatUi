// The chat's state, folded from turn frames. What D3SKHAND's hub stream.js does to the page, done to
// plain data instead, so the web view and D3sk.ChatUi.Avalonia draw the same thing: D3sk.ChatUi's
// ChatSession is this file in C#, and both replay spec/conformance to the same snapshots.
//
// Time is a number of seconds passed with each call (`at`): the working line and "Thought for 3s"
// depend on it, and a fixture replays a turn on its own clock.
import spec from "./generated/spec.js";
import {
  agentBrand, fill, permissionLabel, shouldShowAgentLabel, thinkingLabel, workingActivity, workingPhrase,
} from "./labels.js";
import { hideReplyTitle } from "./reply-title.js";
import {
  diffLines, isPermissionRefusal, toolInput, toolKind, toolLabel, toolSummary,
} from "./tools.js";
import { eventToFrame } from "./events.js";

const L = spec.labels;
const QUIET_SECONDS = L.working.quietSeconds;

const isHttp = (url) => /^https?:\/\//i.test(String(url || ""));
const str = (value) => (value === undefined || value === null ? "" : String(value));
const defaultClock = () => Date.now() / 1000;

/** A message's attachment names: from saved attachments ({ name }) or plain names. */
function attachmentNames(list) {
  return (Array.isArray(list) ? list : [])
    .map((item) => (typeof item === "string" ? item : str(item?.name)))
    .filter(Boolean);
}

/**
 * Creates a session. `agents`: [{ id, name, models: [{ id, name }] }] — names for the reply labels
 * and the approval titles. `projectRoot`: tool paths under it are shown relative to it.
 */
export function createSession({ agents = [], projectRoot = "", clock = defaultClock } = {}) {
  let nextKey = 1;
  const s = {
    agents: Array.isArray(agents) ? agents : [],
    projectRoot: str(projectRoot),
    rows: [],
    approvals: [],
    approvalCalls: new Map(),
    busy: false,
    queued: false,
    liveInput: false,
    permissionMode: null,
    sessionId: null,
    pending: null,
    meta: { agent: "", model: "" },
    live: null,          // the assistant row being written
    liveSince: null,
    usedTools: false,
    activity: "starting",
    quietAt: null,       // when the working line goes quiet after text, or null
    listeners: new Set(),
  };

  const changed = () => { for (const listener of s.listeners) listener(); };
  const touch = (row) => { if (row) row.rev += 1; };

  const agentOf = (id) => s.agents.find((agent) => agent && agent.id === id);
  const agentName = (id) => str(agentOf(id)?.name) || str(id);
  const modelName = (id, model) => {
    const found = (agentOf(id)?.models || []).find((item) => item && item.id === model);
    return str(found?.name) || str(model);
  };
  const replyLabel = (row) => [agentName(row.agent), modelName(row.agent, row.model)].filter(Boolean).join(" · ");
  const isKnown = (id) => id !== undefined && id !== null && id !== "" && s.rows.some((row) => row.id === id);

  // ── rows ──

  function pushRow(row) {
    const full = { key: nextKey++, rev: 0, ...row };
    s.rows.push(full);
    return full;
  }

  function userRow(message) {
    return pushRow({ kind: "user", id: message?.id ?? null, text: str(message?.content ?? message?.text), attachments: attachmentNames(message?.attachments) });
  }

  function assistantRow({ id = null, agent = "", model = "", live = false }) {
    return pushRow({ kind: "assistant", id, agent: str(agent), model: str(model), live, typing: live, parts: [], tools: new Map(), current: null });
  }

  function sweepTransient() {
    const before = s.rows.length;
    s.rows = s.rows.filter((row) => !(row.kind === "notice" && row.transient));
    return before !== s.rows.length;
  }

  // ── parts of a reply ──

  function closeThinking(row, at) {
    const part = row?.current;
    if (!part || part.type !== "thinking" || part.closed) return;
    part.closed = true;
    part.open = false;
    part.seconds = Math.max(1, at - part.startedAt);
    touch(row);
  }

  function appendText(row, type, text, at, { seconds = null, closed = false } = {}) {
    if (!row.current || row.current.type !== type) {
      closeThinking(row, at);
      const part = type === "thinking"
        ? { type, text: "", startedAt: at, seconds, closed, open: !closed }
        : { type: "text", text: "" };
      row.parts.push(part);
      row.current = part;
    }
    row.current.text += str(text);
    touch(row);
  }

  function addTool(row, call, at) {
    closeThinking(row, at);
    row.current = null;
    const name = toolLabel(call?.name);
    const part = {
      type: "tool", callId: str(call?.call_id), name, kind: toolKind(name), args: call?.arguments ?? null,
      status: "running", output: null, isError: false, approval: null, injection: null,
    };
    row.parts.push(part);
    row.tools.set(call?.call_id || `anonymous-${row.tools.size}`, part);
    touch(row);
    return part;
  }

  function markApproval(row, part, decision, detailsUrl = "") {
    if (!part) return;
    part.approval = { state: str(decision), detailsUrl: isHttp(detailsUrl) ? str(detailsUrl) : null };
    touch(row);
  }

  function markInjection(row, part, injection) {
    if (!part) return;
    part.injection = {
      rules: Array.isArray(injection?.rules) ? injection.rules.map(str) : [],
      excerpt: str(injection?.excerpt),
      detailsUrl: isHttp(injection?.details_url) ? str(injection.details_url) : null,
    };
    touch(row);
  }

  function finishTool(row, result, at) {
    const part = row.tools.get(result?.call_id) || addTool(row, { call_id: result?.call_id, name: "tool" }, at);
    part.output = str(result?.output) || L.tools.output.empty;
    part.isError = Boolean(result?.is_error);
    part.status = part.isError ? "error" : "ok";
    const isWaiting = part.approval?.state === "waiting";
    if (result?.approval && (!part.approval || isWaiting)) markApproval(row, part, result.approval, result.approval_details_url);
    else if (!part.approval && isPermissionRefusal(result?.output)) markApproval(row, part, "denied");
    touch(row);
    return part;
  }

  function settleTools(row) {
    for (const part of row.parts) {
      if (part.type === "tool" && part.status === "running") {
        part.status = "unknown";
        part.output = L.tools.output.noResult;
      }
    }
  }

  function renderSavedParts(row, parts, at) {
    for (const part of parts) {
      if (!part || typeof part !== "object") continue;
      if (part.type === "tool") {
        const tool = addTool(row, part, at);
        if (part.approval) markApproval(row, tool, part.approval, part.approval_details_url);
        if (part.injection) markInjection(row, tool, part.injection);
        if (part.output !== null && part.output !== undefined) finishTool(row, part, at);
      } else {
        appendText(row, part.type === "thinking" ? "thinking" : "text", part.text, at, { seconds: typeof part.seconds === "number" ? part.seconds : null, closed: true });
      }
    }
    row.current = null;
    settleTools(row);
  }

  // ── the live reply ──

  function advance(at) {
    if (s.quietAt !== null && at >= s.quietAt) {
      s.quietAt = null;
      if (s.live) {
        s.live.typing = false;
        touch(s.live);
      }
    }
  }

  function startReply(at) {
    if (s.liveSince === null) s.liveSince = at;
    s.live = assistantRow({ agent: s.meta.agent, model: s.meta.model, live: true });
    return s.live;
  }

  function ensureLive(at) {
    if (s.live) return s.live;
    s.busy = true;
    return startReply(at);
  }

  function closeReply(row, at) {
    if (!row) return;
    closeThinking(row, at);
    row.current = null;
    row.typing = false;
    row.live = false;
    touch(row);
  }

  function setActivity(activity) {
    s.activity = activity;
    if (s.live && !s.live.typing) {
      s.live.typing = true;
      touch(s.live);
    }
  }

  function finishTurn(at) {
    const row = s.live;
    if (row) {
      closeReply(row, at);
      settleTools(row);
      if (!row.parts.length) s.rows = s.rows.filter((other) => other !== row);
    }
    s.live = null;
    s.busy = false;
    s.queued = false;
    s.liveInput = false;
    s.quietAt = null;
    s.approvals = [];
    sweepTransient();
  }

  function toolByCall(callId) {
    return s.live?.tools.get(callId) || null;
  }

  function apply(name, data, at) {
    const d = data && typeof data === "object" ? data : {};
    switch (name) {
      case "start": {
        s.pending = null;
        if (d.user_message && !isKnown(d.user_message.id)) userRow(d.user_message);
        if (s.live) closeReply(s.live, at);
        s.meta = { agent: str(d.agent ?? s.meta.agent), model: str(d.model ?? s.meta.model) };
        s.busy = true;
        s.queued = Boolean(d.queued);
        s.liveInput = false;
        s.liveSince = null;
        s.usedTools = false;
        s.activity = "starting";
        s.quietAt = null;
        startReply(at);
        break;
      }
      case "dequeued":
        s.queued = false;
        s.liveSince = at;
        break;
      case "live_input":
        s.liveInput = true;
        break;
      case "permission_mode":
        s.permissionMode = str(d.mode);
        if (d.error) pushRow({ kind: "notice", text: fill(L.notices.modeStill, { mode: permissionLabel(d.mode), error: str(d.error) }), transient: false });
        else pushRow({ kind: "notice", text: fill(L.notices.modeNow, { mode: permissionLabel(d.mode) }), transient: true });
        break;
      case "approval_request": {
        const id = str(d.approval_id);
        s.approvalCalls.set(id, str(d.call_id));
        if (!s.approvals.some((approval) => approval.approval_id === id)) s.approvals.push({ ...d, approval_id: id });
        markApproval(s.live, toolByCall(d.call_id), "waiting");
        break;
      }
      case "approval_resolved": {
        const id = str(d.approval_id);
        s.approvals = s.approvals.filter((approval) => approval.approval_id !== id);
        markApproval(s.live, toolByCall(s.approvalCalls.get(id)), d.allow ? "allowed" : "denied");
        break;
      }
      case "injection_detected":
        markInjection(s.live, toolByCall(d.call_id), d);
        break;
      case "action_blocked":
        markApproval(s.live, toolByCall(d.call_id), "blocked", d.details_url);
        break;
      case "input_accepted": {
        const row = s.live;
        closeReply(row, at);
        s.quietAt = null;
        if (row && d.reply && isKnown(d.reply.id) && row.id !== d.reply.id) s.rows = s.rows.filter((other) => other !== row);
        else if (row && d.reply && d.reply.id !== undefined) row.id = d.reply.id;
        if (d.user_message) {
          if (!isKnown(d.user_message.id)) userRow(d.user_message);
        } else if (d.text) {
          userRow({ content: d.text });
        }
        s.busy = true;
        startReply(at);
        break;
      }
      case "text":
      case "delta": {
        const row = ensureLive(at);
        sweepTransient();
        appendText(row, "text", d.text, at);
        setActivity(s.usedTools ? "wrapping" : "writing");
        s.quietAt = at + QUIET_SECONDS;
        break;
      }
      case "thinking": {
        const row = ensureLive(at);
        sweepTransient();
        s.quietAt = null;
        appendText(row, "thinking", d.text, at);
        setActivity("thinking");
        break;
      }
      case "tool_call": {
        const row = ensureLive(at);
        sweepTransient();
        s.quietAt = null;
        addTool(row, d, at);
        s.usedTools = true;
        setActivity(workingActivity(toolLabel(d.name)));
        break;
      }
      case "tool_result": {
        const row = ensureLive(at);
        finishTool(row, d, at);
        if (d.is_error) setActivity("recovering");
        break;
      }
      case "session":
        if (d.session_id) s.sessionId = str(d.session_id);
        break;
      case "notice":
        pushRow({ kind: "notice", text: str(d.message), transient: Boolean(d.transient) });
        break;
      case "error":
        pushRow({ kind: "error", agent: s.meta.agent, text: str(d.message), parts: [] });
        break;
      case "done":
        finishTurn(at);
        break;
      default:
        return false;
    }
    return true;
  }

  // ── what a view draws ──

  function partView(part, row) {
    if (part.type === "text") return { type: "text", text: hideReplyTitle(part.text, row.live) };
    if (part.type === "thinking") {
      const label = part.closed ? thinkingLabel({ seconds: part.seconds }) : thinkingLabel({ isStreaming: true, characters: part.text.length });
      return { type: "thinking", text: part.text, label, open: part.open };
    }
    const diff = diffLines(part.kind, part.args);
    const output = part.output === null ? L.tools.output.running : part.output;
    const approval = part.approval
      ? { state: part.approval.state, label: L.approval.badges[part.approval.state] || part.approval.state, detailsUrl: part.approval.detailsUrl }
      : null;
    const injection = part.injection
      ? {
        label: L.approval.injection,
        title: fill(L.approval.injectionTitle, { rules: part.injection.rules.join(", ") })
          + (part.injection.excerpt ? `\n\n“${part.injection.excerpt}”` : ""),
        detailsUrl: part.injection.detailsUrl,
      }
      : null;
    const refused = approval && (approval.state === "denied" || approval.state === "blocked");
    return {
      type: "tool",
      callId: part.callId,
      name: part.name,
      kind: part.kind,
      compact: part.kind === "read",
      summary: toolSummary(part.kind, part.args, s.projectRoot),
      status: part.status,
      statusLabel: L.tools.status[part.status],
      input: diff ? null : toolInput(part.args),
      diff,
      output,
      outputShown: !diff || part.isError,
      approval,
      injection,
      fixable: part.isError && Boolean(part.callId) && !refused,
    };
  }

  function workingView(row, at) {
    if (!row.live || !row.typing || row !== s.live) return null;
    if (s.queued) return { text: L.lines.queued, queued: true };
    const since = s.liveSince ?? at;
    return { text: workingPhrase({ activity: s.activity, elapsedSeconds: Math.max(0, at - since) }), queued: false };
  }

  function approvalView(approval) {
    const args = approval.arguments;
    const agent = agentName(s.meta.agent) || L.approval.agentFallback;
    const isQuestion = approval.name === "AskUserQuestion" && args && Array.isArray(args.questions);
    if (isQuestion) {
      return {
        approvalId: approval.approval_id,
        callId: str(approval.call_id),
        isQuestion: true,
        title: fill(L.approval.questionTitle, { agent }),
        summary: str(approval.description) || L.approval.questionSummary,
        tool: str(approval.name),
        kind: "other",
        input: null,
        diff: null,
        questions: args.questions.map((question) => ({
          question: str(question?.question),
          header: str(question?.header),
          multiSelect: question?.multiSelect === true,
          options: (Array.isArray(question?.options) ? question.options : []).map((option) => ({
            label: str(option?.label), description: str(option?.description),
          })),
        })),
      };
    }
    const name = toolLabel(approval.name);
    const kind = toolKind(name);
    const diff = diffLines(kind, args);
    const summary = toolSummary(kind, args, s.projectRoot);
    return {
      approvalId: approval.approval_id,
      callId: str(approval.call_id),
      isQuestion: false,
      title: fill(L.approval.title, { agent, tool: name }),
      summary: [str(approval.description), summary].filter((text, index, all) => text && all.indexOf(text) === index).join(" · "),
      tool: name,
      kind,
      input: diff ? null : toolInput(args),
      diff,
      questions: [],
    };
  }

  function view(at = clock()) {
    advance(at);
    let previousLabel = null;
    const rows = s.rows.map((row) => {
      if (row.kind === "user") return { key: row.key, rev: row.rev, kind: "user", id: row.id, text: row.text, attachments: [...row.attachments] };
      if (row.kind === "notice") return { key: row.key, rev: row.rev, kind: "notice", text: row.text, transient: row.transient };
      if (row.kind === "error") {
        return {
          key: row.key, rev: row.rev, kind: "error",
          label: row.agent ? fill(L.errors.labelWithAgent, { agent: agentName(row.agent) }) : L.errors.label,
          text: row.text,
          parts: row.parts.map((part) => partView(part, row)),
        };
      }
      const label = replyLabel(row);
      const showLabel = shouldShowAgentLabel(previousLabel, label);
      previousLabel = label;
      return {
        key: row.key, rev: row.rev, kind: "assistant", id: row.id, label, showLabel, brand: agentBrand(row.agent, row.model),
        live: row.live, working: workingView(row, at), parts: row.parts.map((part) => partView(part, row)),
      };
    });
    return {
      busy: s.busy,
      queued: s.queued,
      liveInput: s.liveInput,
      permissionMode: s.permissionMode,
      sessionId: s.sessionId,
      pending: s.pending
        ? { text: s.pending.text, attachments: [...s.pending.attachments], brand: agentBrand(s.pending.agent, s.pending.model), working: { text: L.lines.sending, queued: false } }
        : null,
      rows,
      approvals: s.approvals.map(approvalView),
    };
  }

  const session = {
    /** The agents whose names the labels use: [{ id, name, models: [{ id, name }] }]. */
    setAgents(list) {
      s.agents = Array.isArray(list) ? list : [];
      changed();
    },
    /** Folds one wire frame (`text`, `tool_call`, `done`, … and the host's `start`). False when ignored. */
    feed(name, data = {}, at = clock()) {
      advance(at);
      const known = apply(String(name), data, at);
      if (known) changed();
      return known;
    },
    /** Folds one D3sk.AgentCli event ({ event: "TextDelta", text }). False when the chat does not show it. */
    feedEvent(event, at = clock()) {
      const frame = eventToFrame(event);
      return frame ? session.feed(frame[0], frame[1], at) : false;
    },
    /**
     * Loads a saved chat: messages [{ id, role: user | assistant | error, content, parts, agent, model,
     * attachments }], the approvals still pending, and whether a turn is running (its frames follow).
     */
    resume({ messages = [], approvals = [], running = false, queued = false, agent = "", model = "", permissionMode = null, sessionId = null } = {}, at = clock()) {
      s.rows = [];
      s.live = null;
      s.pending = null;
      s.approvalCalls = new Map();
      s.quietAt = null;
      s.liveSince = null;
      s.usedTools = false;
      s.activity = "starting";
      for (const message of Array.isArray(messages) ? messages : []) {
        if (!message || typeof message !== "object") continue;
        const parts = Array.isArray(message.parts) ? message.parts : [];
        if (message.role === "user") userRow(message);
        else if (message.role === "error") {
          const row = pushRow({ kind: "error", agent: str(message.agent), text: str(message.content), parts: [], tools: new Map(), current: null });
          renderSavedParts(row, parts.filter((part) => part && part.type !== "text"), at);
        } else if (message.role === "assistant") {
          const row = assistantRow({ id: message.id ?? null, agent: message.agent, model: message.model });
          renderSavedParts(row, parts.length ? parts : [{ type: "text", text: str(message.content) }].filter((part) => part.text), at);
        }
      }
      s.approvals = (Array.isArray(approvals) ? approvals : []).map((approval) => ({ ...approval, approval_id: str(approval.approval_id) }));
      for (const approval of s.approvals) s.approvalCalls.set(approval.approval_id, str(approval.call_id));
      s.busy = Boolean(running);
      s.queued = Boolean(running && queued);
      s.liveInput = false;
      s.meta = { agent: str(agent), model: str(model) };
      s.permissionMode = permissionMode === null || permissionMode === undefined ? null : str(permissionMode);
      s.sessionId = sessionId ? str(sessionId) : null;
      changed();
    },
    /** The message just sent, drawn at once with "Sending…" until the host's `start` frame. */
    markSending({ text = "", attachments = [], agent = "", model = "" } = {}) {
      s.pending = { text: str(text), attachments: attachmentNames(attachments), agent: str(agent), model: str(model) };
      changed();
    },
    /** The send failed: the pending message goes (the box gets the words back). */
    cancelSending() {
      if (!s.pending) return;
      s.pending = null;
      changed();
    },
    /** The approval was answered here: it leaves the queue until the turn says what happened. */
    dismissApproval(approvalId) {
      const before = s.approvals.length;
      s.approvals = s.approvals.filter((approval) => approval.approval_id !== String(approvalId));
      if (before !== s.approvals.length) changed();
    },
    /** The current approval, the agent of the running turn, the busy flag: what a host's controls need. */
    get busy() { return s.busy; },
    get agent() { return s.meta.agent; },
    get model() { return s.meta.model; },
    /** Everything a view draws at `at`, rows keyed for incremental drawing. */
    view,
    /** The conformance snapshot: the view without draw keys. Both front-ends must give the same one. */
    snapshot(at = clock()) {
      const shown = view(at);
      return { ...shown, rows: shown.rows.map(({ key, rev, ...row }) => row) };
    },
    /** Calls `listener` after every change; returns the unsubscribe. */
    subscribe(listener) {
      s.listeners.add(listener);
      return () => s.listeners.delete(listener);
    },
  };
  return session;
}
