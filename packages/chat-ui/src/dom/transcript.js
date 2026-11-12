// Draws a session's view into a `.messages` box: the hub's transcript.js markup (message
// articles, the rail of text / thinking / tool parts, badges, the working line), built from the
// view model instead of from frames. Rows and parts are keyed: a streaming reply redraws only the
// part that changed, and what the reader opened (a tool box, a thinking block) stays open.
import spec from "../generated/spec.js";
import { renderMarkdown } from "../markdown.js";
import { clipPreview } from "../tools.js";
import { workingMark } from "./marks.js";

const L = spec.labels;

function el(doc, tag, className = "", text) {
  const node = doc.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = text;
  return node;
}

function sign(value) {
  return JSON.stringify(value);
}

/**
 * Creates the transcript renderer. `hooks`: { onReviewApproval(), onToolFix(callId) | null,
 * onLink(href, event) | null }.
 */
export function createTranscript(box, hooks = {}) {
  const doc = box.ownerDocument;
  const drawn = new Map();      // row key → { node, sig, parts: [{ node, sig }] }
  const expanded = new Set();   // "rowKey:partIndex" of tool boxes the reader opened
  const toggled = new Map();    // "rowKey:partIndex" → open, for thinking blocks / compact tools the reader toggled
  let empty = null;
  let pendingNode = null;
  let pendingSig = "";

  function paintClipped(pre, text, isOpen) {
    const full = String(text ?? "");
    const shown = isOpen ? full : clipPreview(full);
    pre.textContent = shown;
    pre.classList.toggle("overflowing", !isOpen && shown !== full);
  }

  function diffNode(lines, isOpen) {
    const pre = el(doc, "pre", "diff clip");
    const limit = isOpen ? Infinity : spec.limits.clipLines;
    lines.slice(0, limit).forEach((line) => pre.append(el(doc, "span", `diff-line diff-${line.kind}`, line.text)));
    pre.classList.toggle("overflowing", lines.length > limit);
    return pre;
  }

  function ioRow(label, content) {
    const row = el(doc, "div", "io-row");
    row.append(el(doc, "span", "io-label", label), content);
    return row;
  }

  function badge(part, head) {
    const approval = part.approval;
    if (approval) {
      if (approval.state === "blocked" && approval.detailsUrl) {
        const link = el(doc, "a", `tool-approval ${approval.state} security-link`, approval.label);
        link.href = approval.detailsUrl;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.title = L.approval.blockedTitle;
        link.addEventListener("click", (event) => event.stopPropagation());
        head.append(link);
      } else if (approval.state === "waiting") {
        const button = el(doc, "button", `tool-approval ${approval.state}`, approval.label);
        button.type = "button";
        button.title = L.approval.waitingTitle;
        button.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();
          hooks.onReviewApproval?.();
        });
        head.append(button);
      } else {
        head.append(el(doc, "span", `tool-approval ${approval.state}`, approval.label));
      }
    }
    if (part.injection) {
      const node = part.injection.detailsUrl ? el(doc, "a", "tool-injection security-link", part.injection.label) : el(doc, "span", "tool-injection", part.injection.label);
      if (part.injection.detailsUrl) {
        node.href = part.injection.detailsUrl;
        node.target = "_blank";
        node.rel = "noopener noreferrer";
        node.addEventListener("click", (event) => event.stopPropagation());
      }
      node.title = part.injection.title;
      head.append(node);
    }
    if (part.fixable && hooks.onToolFix) {
      const button = el(doc, "button", "tool-fix", L.tools.fix);
      button.type = "button";
      button.title = L.tools.fixTitle;
      button.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        button.disabled = true;
        try {
          await hooks.onToolFix(part.callId);
        } finally {
          button.disabled = false;
        }
      });
      head.append(button);
    }
  }

  function toolNode(part, id) {
    const isCompact = part.compact;
    const root = el(doc, isCompact ? "details" : "div", `part part-tool tool-${part.kind} ${part.status}`);
    if (isCompact) {
      root.open = toggled.get(id) === true;
      root.addEventListener("toggle", () => toggled.set(id, root.open));
    }
    const head = el(doc, isCompact ? "summary" : "div", "tool-head");
    const dot = el(doc, "span", "part-dot");
    dot.title = part.statusLabel;
    dot.setAttribute("aria-label", part.statusLabel);
    head.append(dot, el(doc, "span", "tool-name", part.name), el(doc, "span", "tool-summary", part.summary));
    badge(part, head);
    const isOpen = expanded.has(id);
    const box = el(doc, "div", isOpen ? "tool-box expanded" : "tool-box");
    if (part.diff) box.append(diffNode(part.diff, isOpen));
    else {
      const input = el(doc, "pre", "clip");
      paintClipped(input, part.input, isOpen);
      box.append(ioRow(L.tools.io.in, input));
    }
    const output = el(doc, "pre", "clip");
    paintClipped(output, part.output, isOpen);
    const outputRow = ioRow(L.tools.io.out, output);
    outputRow.hidden = !part.outputShown;
    box.append(outputRow);
    box.addEventListener("click", () => {
      if (doc.defaultView?.getSelection?.()?.toString()) return; // selecting text is not a toggle
      if (expanded.has(id)) expanded.delete(id);
      else expanded.add(id);
      const replacement = toolNode(part, id);
      root.replaceWith(replacement);
      const entry = findPart(root);
      if (entry) entry.node = replacement;
    });
    root.append(head, box);
    return root;
  }

  function findPart(node) {
    for (const row of drawn.values()) {
      for (const part of row.parts) if (part.node === node) return part;
    }
    return null;
  }

  function thinkingNode(part, id) {
    const details = el(doc, "details", "part part-thinking");
    details.open = toggled.has(id) ? toggled.get(id) : part.open;
    const summary = el(doc, "summary");
    summary.append(el(doc, "span", "part-dot"), el(doc, "span", "thinking-label", part.label));
    details.append(summary, el(doc, "div", "part-pre", part.text));
    details.addEventListener("toggle", () => {
      if (details.open !== part.open) toggled.set(id, details.open);
      else toggled.delete(id);
    });
    return details;
  }

  function textNode(part) {
    const node = el(doc, "div", "part part-text");
    // Agent text is untrusted: it only becomes markup through the escaping markdown renderer.
    node.innerHTML = renderMarkdown(part.text);
    if (hooks.onLink) {
      for (const link of node.querySelectorAll("a[href]")) {
        link.addEventListener("click", (event) => hooks.onLink(link.getAttribute("href"), event));
      }
    }
    return node;
  }

  function partNode(part, id) {
    if (part.type === "tool") return toolNode(part, id);
    if (part.type === "thinking") return thinkingNode(part, id);
    return textNode(part);
  }

  function drawParts(body, entry, parts, rowKey) {
    parts.forEach((part, index) => {
      const sig = sign(part);
      const old = entry.parts[index];
      if (old && old.sig === sig) return;
      const node = partNode(part, `${rowKey}:${index}`);
      if (old) old.node.replaceWith(node);
      else body.append(node);
      entry.parts[index] = { node, sig };
    });
    while (entry.parts.length > parts.length) entry.parts.pop().node.remove();
  }

  function workingNode(working, brand) {
    const node = el(doc, "div", working?.queued ? "working queued" : "working");
    node.setAttribute("aria-hidden", "true");
    node.append(workingMark(doc, brand), el(doc, "span", "working-text", working?.text || ""));
    return node;
  }

  function newRow(row) {
    const article = el(doc, "article", `msg msg-${row.kind}`);
    if (row.kind === "notice") {
      const notice = el(doc, "div", row.transient ? "msg-notice transient" : "msg-notice", row.text);
      return { node: notice, parts: [] };
    }
    if (row.kind === "user") {
      article.append(el(doc, "div", "msg-body", row.text));
      if (row.attachments.length) {
        const list = el(doc, "div", "attachment-list");
        for (const name of row.attachments) list.append(el(doc, "span", "attachment-chip", name));
        article.append(list);
      }
      if (!row.text && row.attachments.length) article.querySelector(".msg-body").hidden = true;
      return { node: article, parts: [] };
    }
    if (row.kind === "error") {
      article.append(el(doc, "div", "msg-meta", row.label));
      const activity = el(doc, "div", "msg-body transcript msg-activity");
      activity.hidden = !row.parts.length;
      article.append(activity, el(doc, "div", "msg-body", row.text));
      return { node: article, parts: [], body: activity };
    }
    const meta = el(doc, "div", "msg-meta msg-agent", row.label);
    const body = el(doc, "div", "msg-body transcript");
    const working = workingNode(row.working, row.brand);
    article.append(meta, body, working);
    return { node: article, parts: [], body, meta, working, brand: row.brand };
  }

  function updateRow(entry, row) {
    if (row.kind === "assistant") {
      entry.meta.textContent = row.label;
      entry.meta.hidden = !row.showLabel;
      drawParts(entry.body, entry, row.parts, row.key);
      entry.body.classList.toggle("typing", Boolean(row.working));
      if (entry.brand !== row.brand) {
        const mark = workingMark(doc, row.brand);
        entry.working.querySelector(".working-glyph").replaceWith(mark);
        entry.brand = row.brand;
      }
      entry.working.classList.toggle("queued", Boolean(row.working?.queued));
      const text = entry.working.querySelector(".working-text");
      const wording = row.working?.text || "";
      if (text.textContent !== wording) text.textContent = wording;
    } else if (row.kind === "error") {
      entry.body.hidden = !row.parts.length;
      drawParts(entry.body, entry, row.parts, row.key);
    }
  }

  function drawPending(pending) {
    const sig = sign(pending);
    if (sig === pendingSig) return;
    pendingSig = sig;
    pendingNode?.remove();
    pendingNode = null;
    if (!pending) return;
    pendingNode = el(doc, "div", "pending-send");
    const article = el(doc, "article", "msg msg-user");
    article.append(el(doc, "div", "msg-body", [pending.text, ...pending.attachments].filter(Boolean).join("\n")));
    pendingNode.append(article, workingNode(pending.working, pending.brand));
  }

  /** Draws `view` (session.view()). */
  function render(view) {
    const keys = new Set(view.rows.map((row) => row.key));
    for (const [key, entry] of drawn) {
      if (!keys.has(key)) {
        entry.node.remove();
        drawn.delete(key);
      }
    }
    let previous = null;
    for (const row of view.rows) {
      let entry = drawn.get(row.key);
      if (!entry) {
        entry = newRow(row);
        drawn.set(row.key, entry);
      }
      const sig = `${row.rev}|${row.showLabel}|${row.working ? row.working.text : ""}|${row.label || ""}`;
      if (entry.sig !== sig) {
        updateRow(entry, row);
        entry.sig = sig;
      }
      const wanted = previous ? previous.nextSibling : box.firstChild;
      if (entry.node !== wanted) box.insertBefore(entry.node, wanted);
      previous = entry.node;
    }
    drawPending(view.pending);
    if (pendingNode && box.lastChild !== pendingNode) box.append(pendingNode);
    const isEmpty = !view.rows.length && !view.pending;
    if (isEmpty && !empty) {
      empty = el(doc, "div", "empty", L.transcript.empty);
      box.append(empty);
    } else if (!isEmpty && empty) {
      empty.remove();
      empty = null;
    }
  }

  return { render };
}
