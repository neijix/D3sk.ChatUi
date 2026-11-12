// @d3sk/chat-ui: D3SKHAND's chat for any web page. mountChat is the component; the rest is the
// plain logic it draws from, the same rules D3sk.ChatUi (C#) follows.
export { mountChat, composerAgents } from "./chat.js";
export { createSession } from "./session.js";
export { eventToFrame } from "./events.js";
export { parseSse, readSse } from "./sse.js";
export { applyTheme, themeProperties, brandColor, PALETTES } from "./theme.js";
export { renderMarkdown, escapeHtml } from "./markdown.js";
export { hideReplyTitle } from "./reply-title.js";
export {
  agentBrand, chatStatusLabel, formatTokenCount, permissionLabel, questionAnswers, resumeTimeText,
  shouldShowAgentLabel, thinkingLabel, workingActivity, workingPhrase, workingWord,
  QUEUED_LINE, SENDING_LINE, TERMINAL_WORKING_LINE,
} from "./labels.js";
export { toolLabel, toolKind, toolSummary, toolInput, diffLines, isPermissionRefusal } from "./tools.js";
export { default as spec } from "./generated/spec.js";
