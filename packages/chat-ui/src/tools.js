// How a tool call reads in the transcript, from D3SKHAND's hub transcript.js: its label, kind,
// one-line summary, IN text or diff, and whether its output is a refusal. D3sk.ChatUi's ToolView
// follows the same rules (shared corpus tests/fixtures/ui/tool-summary.json).
import spec from "./generated/spec.js";

const T = spec.labels.tools;

/** "Bash" from "run_terminal_command"; an unknown name is kept, a missing one is "tool". */
export function toolLabel(name) {
  return T.labels[name] || name || T.fallbackName;
}

/** shell, read, edit, write or other, from a tool label. */
export function toolKind(label) {
  return T.kinds[label] || "other";
}

function firstString(args, keys) {
  const key = keys.find((candidate) => typeof args[candidate] === "string" && args[candidate]);
  return key ? args[key] : "";
}

function projectRelative(path, root) {
  return root && path.startsWith(`${root}/`) ? path.slice(root.length + 1) : path;
}

/** The summary beside a tool's name: a path, a pattern, a command's first line; at most 140 characters. */
export function toolSummary(kind, args, projectRoot = "") {
  let summary = "";
  if (typeof args === "string") summary = args;
  else if (args && typeof args === "object" && !Array.isArray(args)) {
    const path = projectRelative(firstString(args, T.pathKeys), projectRoot);
    if (kind === "edit" || kind === "write") summary = path;
    else if (kind === "read") {
      const pattern = firstString(args, T.patternKeys);
      summary = pattern && path ? `${pattern} · ${path}` : pattern || path;
      if (Number.isInteger(args.offset) && Number.isInteger(args.limit)) {
        summary += ` · lines ${args.offset}–${args.offset + args.limit - 1}`;
      }
    }
    summary = summary || firstString(args, T.summaryKeys) || Object.values(args).find((value) => typeof value === "string") || "";
  }
  summary = summary.split("\n")[0];
  return summary.length > T.summaryMax ? `${summary.slice(0, T.summaryMax - 1)}…` : summary;
}

/** The IN row: a command as typed, else the arguments as indented JSON. */
export function toolInput(args) {
  if (args === null || args === undefined) return "";
  if (typeof args === "string") return args;
  const command = args.command || args.CommandLine;
  if (typeof command === "string") return command;
  return stableJson(args);
}

/** JSON.stringify(value, null, 2), the layout both front-ends print arguments in. */
export function stableJson(value) {
  return JSON.stringify(value, null, 2);
}

/**
 * An edit's or a write's lines: [{ kind: "add" | "del" | "gap", text }], every line (a view clips
 * them to the preview). Null when the call is not an edit or write, or has nothing to show.
 */
export function diffLines(kind, args) {
  if ((kind !== "edit" && kind !== "write") || !args || typeof args !== "object" || Array.isArray(args)) return null;
  const lines = [];
  const add = (lineKind, sign, text) => {
    if (!text) return;
    for (const line of String(text).replace(/\n$/, "").split("\n")) lines.push({ kind: lineKind, text: `${sign} ${line}` });
  };
  if (kind === "write") {
    add("add", "+", firstString(args, ["content", "contents", "file_text", "text"]));
  } else {
    const edits = Array.isArray(args.edits) ? args.edits : [args];
    edits.forEach((edit, index) => {
      if (!edit || typeof edit !== "object") return;
      if (index) lines.push({ kind: "gap", text: "⋯" });
      add("del", "-", firstString(edit, ["old_string", "old_str", "oldText", "oldString"]));
      add("add", "+", firstString(edit, ["new_string", "new_str", "newText", "newString"]));
    });
  }
  return lines.length ? lines : null;
}

/** The first lines of a long text, as a closed tool box shows them. */
export function clipPreview(text) {
  const full = String(text ?? "");
  let preview = full.split("\n").slice(0, spec.limits.clipLines).join("\n");
  if (preview.length > spec.limits.clipChars) preview = preview.slice(0, spec.limits.clipChars);
  return preview;
}

const REFUSAL = /^(?:Tool `[^`]+` was not executed: )?(?:Denied by permission policy\b|User cancelled the execution for tool\b|The user rejected permission to use this specific tool call\.?|This command requires approval\b|A variable in this command requires approval\b|The following part requires approval\b|Auto mode blocked\b)/i;

/** The CLI refused the call itself (not an OS "permission denied", not a file quoting one). */
export function isPermissionRefusal(output) {
  const text = String(output || "").trim();
  if (!text || text.length > 2000) return false;
  return REFUSAL.test(text);
}
