// From D3SKHAND hub/static/reply-title.js: agents end a reply with a hidden
// `<!-- d3skhand-title: … -->` line; the transcript hides it, whole or still arriving.
const TITLE_LINE = /[ \t]*<!--[ \t]*(?:d3skhand|agentdesk)-title[ \t]*:[^\n]*?-->[ \t]*(?:\r?\n)?/gi;
const TITLE_STARTS = ["<!-- d3skhand-title:", "<!-- agentdesk-title:"];

function isTitleStart(tail) {
  const normalized = tail.replace(/^<!--[ \t]*/, "<!-- ").toLowerCase();
  return TITLE_STARTS.some((start) => start.startsWith(normalized) || normalized.startsWith(start));
}

export function hideReplyTitle(text, isStreaming = false) {
  let shown = text.replace(TITLE_LINE, "");
  if (isStreaming) {
    const start = shown.lastIndexOf("<");
    const tail = start < 0 ? "" : shown.slice(start);
    if (tail && !tail.includes("\n") && isTitleStart(tail)) shown = shown.slice(0, start);
  }
  return shown === text ? text : shown.trimEnd();
}
