// Small wording rules of the transcript, from D3SKHAND's hub labels.js. Every word comes from the
// shared spec (spec/chat-ui.json); D3sk.ChatUi's ChatLabels follows the same rules, and both pass
// D3SKHAND's labels corpus (tests/fixtures/ui).
import spec from "./generated/spec.js";

const L = spec.labels;
const W = L.working;

/** Fills `{name}` holes of a spec sentence. */
export function fill(template, values = {}) {
  return String(template).replace(/\{(\w+)\}/g, (match, key) => (key in values ? String(values[key]) : match));
}

/** JavaScript's Math.round: halves go up. The C# side uses Math.Floor(x + 0.5) for the same result. */
const roundHalfUp = (value) => Math.floor(value + 0.5);

export const QUEUED_LINE = L.lines.queued;
export const SENDING_LINE = L.lines.sending;
export const TERMINAL_WORKING_LINE = L.lines.terminalWorking;

/** "1.2k" from 1234, "999" from 999 — the token count wording of the box's context meter. */
export function formatTokenCount(tokens) {
  return tokens >= 1000 ? `${(tokens / 1000).toFixed(1)}k` : String(tokens);
}

function formatThinkingSeconds(seconds) {
  const whole = Math.max(1, roundHalfUp(seconds));
  if (whole < 60) return `${whole}s`;
  const minutes = Math.floor(whole / 60);
  const rest = whole % 60;
  return rest ? `${minutes}m ${rest}s` : `${minutes}m`;
}

/** "Thinking… · 10.2k tokens" while written, "Thought for 12s" once it ended, "Thought" when untimed. */
export function thinkingLabel({ isStreaming = false, characters = 0, seconds = null } = {}) {
  if (isStreaming) {
    const tokens = roundHalfUp(characters / 4);
    return tokens ? fill(L.thinking.streamingTokens, { tokens: formatTokenCount(tokens) }) : L.thinking.streaming;
  }
  return typeof seconds === "number" && seconds > 0
    ? fill(L.thinking.thoughtFor, { duration: formatThinkingSeconds(seconds) })
    : L.thinking.thought;
}

/** A reply names its subscription · model only when that differs from the reply before it. */
export function shouldShowAgentLabel(previousLabel, label) {
  return Boolean(label) && label !== previousLabel;
}

/** What the agent is doing, from the tool it just called (names as Claude Code spells them). */
export function workingActivity(toolName) {
  const name = String(toolName || "").toLowerCase();
  if (/^(bash|shell|run_|execute)/.test(name) || name === "killshell" || name === "bashoutput") return "shell";
  if (/^(read|grep|glob|list|ls|view|webfetch|websearch|toolsearch|codebase_search)/.test(name)) return "searching";
  if (/^(edit|multiedit|search_replace|str_replace|replace|notebookedit)/.test(name)) return "editing";
  if (/^(write|create)/.test(name)) return "writing";
  if (/^(task|agent|skill)/.test(name)) return "assembling";
  return "handling";
}

/** The line under a reply being written. Deterministic: same inputs, same words. */
export function workingPhrase({ activity = "starting", elapsedSeconds = 0 } = {}) {
  const pool = W.phrases[activity] || W.phrases.starting;
  const step = Math.max(0, Math.floor(elapsedSeconds / W.stepSeconds));
  const every = elapsedSeconds >= W.veryLongSeconds ? 2 : elapsedSeconds >= W.longSeconds ? 3 : 0;
  if (every && step % every === every - 1) {
    const longFrom = Math.ceil(W.longSeconds / W.stepSeconds);
    const veryLongFrom = Math.ceil(W.veryLongSeconds / W.stepSeconds);
    const turn = step < veryLongFrom
      ? Math.floor((step - longFrom) / 3)
      : Math.ceil((veryLongFrom - longFrom) / 3) + Math.floor((step - veryLongFrom) / 2);
    const isWired = activity === "shell" || activity === "searching";
    const last = W.longHaul.length - 1;
    const haul = isWired ? [W.longHaul[last], ...W.longHaul.slice(0, last)] : W.longHaul;
    return haul[turn % haul.length];
  }
  return pool[step % pool.length];
}

/** One plain word per chat, always the same for that chat. */
export function workingWord(seed) {
  let hash = 0;
  for (const char of String(seed ?? "")) hash = (Math.imul(hash, 31) + char.codePointAt(0)) >>> 0;
  return W.words[hash % W.words.length];
}

/** "00:52" today, "Mon 00:52" on another day, this computer's clock. */
export function resumeTimeText(resumeAt, now = Date.now() / 1000) {
  const at = new Date(resumeAt * 1000);
  const today = new Date(now * 1000);
  const clock = `${String(at.getHours()).padStart(2, "0")}:${String(at.getMinutes()).padStart(2, "0")}`;
  return at.toDateString() === today.toDateString() ? clock : `${L.status.weekdays[at.getDay()]} ${clock}`;
}

/** The chat list's pill: { kind, text } or null. */
export function chatStatusLabel(conversation, now = Date.now() / 1000) {
  if (!conversation) return null;
  if (conversation.queued) return { kind: "queued", text: L.status.queued };
  if (conversation.running || conversation.terminal_working) return { kind: "running", text: L.status.running };
  if (conversation.resume_at) {
    const when = conversation.resume_at > now ? resumeTimeText(conversation.resume_at, now) : L.status.soon;
    return { kind: "resuming", text: fill(L.status.resumes, { when }) };
  }
  if (conversation.has_error) return { kind: "error", text: L.status.error };
  if (conversation.unread) return { kind: "done", text: L.status.done };
  return null;
}

/** Whose mark spins under a reply: the model says more than the CLI. */
export function agentBrand(agent, model) {
  const name = String(model || "").toLowerCase();
  if (/claude|opus|sonnet|haiku|fable|mythos/.test(name)) return "claude";
  if (/gemini|gemma/.test(name)) return "gemini";
  if (/gpt|chatgpt|openai|codex|(^|\/)o[134](-|$)/.test(name)) return "openai";
  if (/grok|xai/.test(name)) return "grok";
  const cli = String(agent || "").toLowerCase();
  if (cli === "claude") return "claude";
  if (cli === "gemini" || cli === "agy") return "gemini";
  if (cli === "grok") return "grok";
  if (cli === "copilot") return "copilot";
  if (cli === "agent" || cli === "cursor" || cli === "cursor-agent") return "cursor";
  if (cli === "codex") return "openai";
  if (cli === "mlx") return "mlx";
  if (cli === "colibri") return "colibri";
  return "d3skhand";
}

/** "Accept edits" from "acceptEdits"; an unknown mode is shown as is. */
export function permissionLabel(mode) {
  const found = L.permissions.modes.find(([value]) => value === mode);
  return found ? found[1] : String(mode ?? "");
}

/**
 * What goes back to the agent for an AskUserQuestion, keyed by question text. `picked` holds the
 * option labels (a list for a multi-select), `own` what was typed under "Other…".
 */
export function questionAnswers(questions, picked = {}, own = {}) {
  const answers = {};
  for (const question of questions || []) {
    const key = question?.question;
    if (typeof key !== "string" || !key) continue;
    const typed = own[key]?.isActive ? String(own[key].text || "").trim() : "";
    if (question.multiSelect === true) {
      const labels = Array.isArray(picked[key]) ? [...picked[key]] : [];
      if (typed) labels.push(typed);
      if (labels.length) answers[key] = labels;
    } else if (typed) {
      answers[key] = typed;
    } else if (typeof picked[key] === "string" && picked[key]) {
      answers[key] = picked[key];
    }
  }
  return answers;
}
