// mountChat: D3SKHAND's chat as a component any page can host. The page gives the agents, feeds
// the turn's frames (or D3sk.AgentCli events, or an SSE response), and answers Send, Stop and the
// approvals; the chat draws the transcript, the working line, the approval dialog and
// AgentCliBridge's chat box. D3sk.ChatUi.Avalonia's ChatView has the same host API.
import spec from "./generated/spec.js";
import { createSession } from "./session.js";
import { applyTheme } from "./theme.js";
import { readSse } from "./sse.js";
import { createTranscript } from "./dom/transcript.js";
import { createApprovals } from "./dom/approvals.js";
import { AgentUI } from "./dom/acb.js";

const BOTTOM = spec.limits.bottomThreshold;

/**
 * The box's agents (AgentCliBridge's catalog shape) from the host's: { id, name, models: [{ id,
 * name }] } plus any catalog field (flavor, prices, new_models…), which passes through.
 */
export function composerAgents(agents) {
  return (agents || []).map((agent) => ({
    ...agent,
    command: agent.command || agent.id,
    flavor: agent.flavor || agent.id,
    name: agent.name || agent.id,
    models: (agent.models || []).map((model) => (typeof model === "string" ? model : model.id)),
  }));
}

/** The session's agents ({ id, name, models: [{ id, name }] }) from the host's. */
function sessionAgents(agents) {
  return (agents || []).map((agent) => ({
    id: agent.id || agent.command,
    name: agent.name || agent.id || agent.command,
    models: (agent.models || []).map((model) => (typeof model === "string" ? { id: model, name: model } : model)),
  }));
}

/**
 * Mounts the chat in `container` (its content is replaced).
 *
 * options:
 *  - agents: [{ id, name, models: [{ id, name }] }]
 *  - projectRoot: tool paths under it are shown relative to it
 *  - palette: teal | slate | midnight | ocean | forest | dusk
 *  - onSend(message): message = { text, files, command, model, permission, effort, … } from the
 *    box; return a promise, reject to give the words back. While a turn runs it is live input.
 *  - onStop(): Stop was pressed.
 *  - onApproval({ approvalId, allow, message?, answers? }): return a promise; on success the
 *    approval leaves the queue (the turn's approval_resolved frame then marks the tool).
 *  - onToolFix(callId): shows Fix on failed tools when given.
 *  - onLink(href, event): a link in the agent's text was clicked (they open in a new tab otherwise).
 *  - onError(message): the box's toasts.
 *  - composer: more AgentUI.mountComposer options (permission, effort, slashCommands, …).
 */
export function mountChat(container, options = {}) {
  const doc = container.ownerDocument;
  const win = doc.defaultView;
  const clock = options.clock || (() => Date.now() / 1000);
  let agents = options.agents || [];

  const root = doc.createElement("div");
  root.className = "d3sk-chat";
  applyTheme(root, options.palette);
  const messages = doc.createElement("section");
  messages.className = "messages";
  messages.setAttribute("aria-live", "polite");
  messages.setAttribute("aria-label", spec.labels.transcript.ariaLabel);
  const jump = doc.createElement("button");
  jump.type = "button";
  jump.className = "jump-bottom";
  jump.textContent = spec.labels.transcript.jumpToBottom;
  jump.hidden = true;
  const composerRoot = doc.createElement("div");
  composerRoot.className = "composer-root";
  root.append(messages, jump, composerRoot);
  container.replaceChildren(root);

  const session = createSession({ agents: sessionAgents(agents), projectRoot: options.projectRoot || "", clock });
  let approvals = null;
  const transcript = createTranscript(messages, {
    onReviewApproval: () => approvals?.review(),
    onToolFix: options.onToolFix || null,
    onLink: options.onLink || null,
  });

  const composer = AgentUI.mountComposer(composerRoot, {
    agents: composerAgents(agents),
    toast: (message) => options.onError?.(message),
    ...options.composer,
    onSend: async (message) => {
      const isLive = session.busy;
      if (!isLive) {
        session.markSending({ text: message.text, attachments: message.files || [], agent: message.command, model: message.model });
      }
      try {
        await options.onSend?.(message);
      } catch (error) {
        if (!isLive) session.cancelSending();
        throw error;
      }
    },
    onStop: () => options.onStop?.(),
  });

  approvals = createApprovals(root, composer.elements.slot, {
    answer: async ({ approvalId, ...answer }) => {
      await options.onApproval?.({ approvalId, ...answer });
      session.dismissApproval(approvalId);
    },
    onError: (error) => options.onError?.(error?.message || String(error)),
  });

  let stuck = true;
  let frame = 0;
  let wasBusy = null;
  const nearBottom = () => messages.scrollHeight - messages.scrollTop - messages.clientHeight < BOTTOM;
  messages.addEventListener("scroll", () => {
    stuck = nearBottom();
    jump.hidden = stuck;
  });
  jump.addEventListener("click", () => {
    stuck = true;
    messages.scrollTop = messages.scrollHeight;
    jump.hidden = true;
  });

  function render() {
    if (frame) {
      (win?.cancelAnimationFrame || clearTimeout)(frame);
      frame = 0;
    }
    const view = session.view(clock());
    transcript.render(view);
    approvals.render(view.approvals);
    if (wasBusy !== view.busy) {
      composer.update({ isStreaming: view.busy });
      wasBusy = view.busy;
    }
    if (stuck) messages.scrollTop = messages.scrollHeight;
    return view;
  }

  const schedule = () => {
    if (frame) return;
    frame = (win?.requestAnimationFrame || ((callback) => setTimeout(callback, 16)))(() => {
      frame = 0;
      render();
    });
  };
  const unsubscribe = session.subscribe(schedule);
  // The working line changes wording every few seconds while nothing arrives.
  const ticker = setInterval(() => { if (session.busy) schedule(); }, 1000);
  render();

  return {
    session,
    composer,
    elements: { root, messages, composerRoot, jump, ...approvals.elements },
    /** Folds one wire frame: text, thinking, tool_call, tool_result, approval_request, done, start… */
    feed(name, data) {
      return session.feed(name, data, clock());
    },
    /** Folds one D3sk.AgentCli event ({ event: "TextDelta", text }). */
    feedEvent(event) {
      return session.feedEvent(event, clock());
    },
    /** Feeds every frame of an SSE response (the hub's /stream, AgentCli's Wire.SseFrame). */
    readSse(response) {
      return readSse(response, (name, data) => session.feed(name, data, clock()));
    },
    /** Loads a saved chat: { messages, approvals, running, agent, model, permissionMode, sessionId }. */
    resume(state) {
      session.resume(state, clock());
      stuck = true;
    },
    /** Replaces the agents (the box's picker and the reply labels). */
    setAgents(list) {
      agents = list || [];
      session.setAgents(sessionAgents(agents));
      composer.update({ agents: composerAgents(agents) });
    },
    /** Switches the palette; returns the id applied. */
    setPalette(id) {
      return applyTheme(root, id);
    },
    /** Draws now instead of on the next frame (tests, or before measuring). */
    render,
    destroy() {
      unsubscribe();
      clearInterval(ticker);
      if (frame) (win?.cancelAnimationFrame || clearTimeout)(frame);
      composer.destroy();
      container.replaceChildren();
    },
  };
}
