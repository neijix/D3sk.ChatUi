// The approval dialog (hub approvals.js): Allow / Deny with an optional message, or the
// AskUserQuestion options with "Other…". Later (or Esc) puts it off behind a banner.
import spec from "../generated/spec.js";
import { questionAnswers } from "../labels.js";

const A = spec.labels.approval;

function el(doc, tag, className = "", text) {
  const node = doc.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = text;
  return node;
}

function button(doc, className, text) {
  const node = el(doc, "button", className, text);
  node.type = "button";
  return node;
}

/**
 * Creates the dialog in `root` and its banner in `bannerHost`. `answer({ approvalId, allow,
 * message, answers })` returns a promise; on success the approval leaves the queue.
 */
export function createApprovals(root, bannerHost, { answer, onError = () => {} }) {
  const doc = root.ownerDocument;
  const dialog = el(doc, "dialog", "approval-dialog");
  const head = el(doc, "div", "approval-head");
  const title = el(doc, "h3", "approval-title", A.fallbackTitle);
  const count = el(doc, "span", "approval-count muted");
  head.append(title, count);
  const summary = el(doc, "p", "approval-summary");
  const detail = el(doc, "div", "msg-body approval-detail");
  const message = el(doc, "textarea", "approval-message");
  message.rows = 2;
  message.maxLength = 2000;
  message.placeholder = A.messagePlaceholder;
  const later = button(doc, "btn-ghost approval-later", A.later);
  const skip = button(doc, "btn-ghost approval-skip", A.skip);
  const deny = button(doc, "btn-danger approval-deny", A.deny);
  const allow = button(doc, "btn-primary approval-allow", A.allow);
  const send = button(doc, "btn-primary approval-send", A.send);
  const actions = el(doc, "div", "approval-actions");
  actions.append(later, skip, el(doc, "span", "spacer"), deny, allow, send);
  dialog.append(head, summary, detail, message, actions);
  root.append(dialog);
  const banner = button(doc, "approval-banner", A.banner);
  banner.hidden = true;
  bannerHost.append(banner);

  let approvals = [];
  let shownId = null;
  let putOff = null;
  let picks = { answers: {}, own: {}, questions: [] };
  let isBusy = false;

  const current = () => approvals[0] || null;
  const isOpen = () => dialog.open || dialog.hasAttribute("open");
  const open = () => {
    if (isOpen()) return;
    if (typeof dialog.showModal === "function") {
      try {
        dialog.showModal();
        return;
      } catch { /* not in a document yet: fall back to the attribute */ }
    }
    dialog.setAttribute("open", "");
  };
  const close = () => {
    if (!isOpen()) return;
    if (typeof dialog.close === "function") dialog.close();
    dialog.removeAttribute("open");
  };

  function ioRow(label, content) {
    const row = el(doc, "div", "io-row");
    row.append(el(doc, "span", "io-label", label), content);
    return row;
  }

  function updateSend() {
    send.disabled = isBusy || Object.keys(questionAnswers(picks.questions, picks.answers, picks.own)).length === 0;
  }

  function questionBlock(question) {
    const block = el(doc, "div", "question-block");
    const qhead = el(doc, "div", "question-head");
    if (question.header) qhead.append(el(doc, "span", "question-tag", question.header));
    qhead.append(el(doc, "span", "question-text", question.question));
    block.append(qhead);
    const options = el(doc, "div", "question-options");
    const field = el(doc, "textarea", "question-own");
    const other = button(doc, "question-option question-other");
    for (const option of question.options) {
      const pick = button(doc, "question-option");
      pick.append(el(doc, "span", "question-option-label", option.label));
      if (option.description) pick.append(el(doc, "span", "question-option-desc", option.description));
      pick.addEventListener("click", () => {
        const key = question.question;
        if (question.multiSelect) {
          const list = Array.isArray(picks.answers[key]) ? picks.answers[key] : [];
          const at = list.indexOf(option.label);
          if (at >= 0) list.splice(at, 1);
          else list.push(option.label);
          if (list.length) picks.answers[key] = list;
          else delete picks.answers[key];
          pick.classList.toggle("selected", list.includes(option.label));
        } else {
          picks.answers[key] = option.label;
          for (const sibling of options.children) sibling.classList.toggle("selected", sibling === pick);
          if (picks.own[key]?.isActive) {
            picks.own[key].isActive = false;
            other.setAttribute("aria-expanded", "false");
            field.hidden = true;
          }
        }
        updateSend();
      });
      options.append(pick);
    }
    other.setAttribute("aria-expanded", "false");
    other.append(el(doc, "span", "question-option-label", A.other), el(doc, "span", "question-option-desc", A.otherDescription));
    field.rows = 2;
    field.maxLength = A.ownAnswerLength;
    field.placeholder = A.ownPlaceholder;
    field.setAttribute("aria-label", question.question);
    field.hidden = true;
    other.addEventListener("click", () => {
      const key = question.question;
      const isActive = !picks.own[key]?.isActive;
      picks.own[key] = { isActive, text: field.value };
      other.classList.toggle("selected", isActive);
      other.setAttribute("aria-expanded", String(isActive));
      field.hidden = !isActive;
      if (isActive && !question.multiSelect) {
        delete picks.answers[key];
        for (const sibling of options.children) sibling.classList.toggle("selected", sibling === other);
      }
      if (isActive) field.focus?.();
      updateSend();
    });
    field.addEventListener("input", () => {
      picks.own[question.question] = { isActive: true, text: field.value };
      updateSend();
    });
    field.addEventListener("keydown", (event) => {
      if (event.key === "Enter" && (event.ctrlKey || event.metaKey) && !send.disabled) {
        event.preventDefault();
        send.click();
      }
    });
    options.append(other);
    block.append(options, field);
    if (question.multiSelect) block.append(el(doc, "div", "question-hint", A.multiHint));
    return block;
  }

  function fill(approval) {
    shownId = approval.approvalId;
    dialog.classList.toggle("questions", approval.isQuestion);
    title.textContent = approval.title;
    dialog.setAttribute("aria-label", approval.title);
    summary.textContent = approval.summary;
    message.value = "";
    picks = { answers: {}, own: {}, questions: approval.questions };
    if (approval.isQuestion) {
      detail.replaceChildren(...approval.questions.map(questionBlock));
      updateSend();
      return;
    }
    const box = el(doc, "div", "tool-box expanded");
    if (approval.diff) {
      const pre = el(doc, "pre", "diff");
      for (const line of approval.diff) pre.append(el(doc, "span", `diff-line diff-${line.kind}`, line.text));
      box.append(pre);
    } else {
      box.append(ioRow(spec.labels.tools.io.in, el(doc, "pre", "", approval.input)));
    }
    detail.replaceChildren(box);
  }

  function render(list) {
    approvals = list;
    const approval = current();
    if (!approval) {
      banner.hidden = true;
      shownId = null;
      close();
      return;
    }
    const isPutOff = putOff === approval.approvalId;
    banner.hidden = !isPutOff;
    if (isPutOff) {
      close();
      return;
    }
    if (shownId !== approval.approvalId) fill(approval);
    count.textContent = approvals.length > 1 ? A.count.replace("{index}", "1").replace("{count}", String(approvals.length)) : "";
    open();
  }

  async function respond(payload) {
    const approval = current();
    if (!approval || isBusy) return;
    isBusy = true;
    for (const node of [allow, deny, send, skip]) node.disabled = true;
    try {
      await answer({ approvalId: approval.approvalId, ...payload });
    } catch (error) {
      onError(error);
    } finally {
      isBusy = false;
      for (const node of [allow, deny, skip]) node.disabled = false;
      updateSend();
    }
  }

  allow.addEventListener("click", () => respond({ allow: true, message: message.value.trim() }));
  deny.addEventListener("click", () => respond({ allow: false, message: message.value.trim() }));
  send.addEventListener("click", () => respond({ allow: true, answers: questionAnswers(picks.questions, picks.answers, picks.own) }));
  skip.addEventListener("click", () => respond({ allow: true, answers: {} }));
  later.addEventListener("click", () => {
    putOff = current()?.approvalId || null;
    render(approvals);
  });
  dialog.addEventListener("cancel", (event) => {
    event.preventDefault();
    later.click();
  });
  banner.addEventListener("click", () => {
    putOff = null;
    render(approvals);
  });

  return {
    render,
    /** Opens the dialog again after Later (the waiting badge on a tool calls it). */
    review() {
      putOff = null;
      render(approvals);
    },
    elements: { dialog, banner, title, summary, detail, message, allow, deny, later, send, skip },
  };
}
