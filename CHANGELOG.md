# Changelog

All notable changes to `D3sk.ChatUi`, `D3sk.ChatUi.Avalonia` and `@d3sk/chat-ui`. Versions follow
[Semantic Versioning](https://semver.org).

## [Unreleased]

## [0.2.0] - 2026-11-12

D3SKHAND's chat in two identical front-ends, Avalonia and the web, under one spec.

### Shared (`spec/`)

- `spec/chat-ui.json`: design tokens (six palettes, action and brand colours, type, radii, spacing)
  and every label, read by both front-ends. Badges, tool summaries and dialog labels are at least 13 px in both.
- `spec/conformance/`: 22 replayed turns (D3sk.AgentCli's ten captured streams plus approvals,
  questions, injection guard, live input, queue, notices, resume, sending) with the snapshots both sessions
  must reach, and both views must draw.
- D3SKHAND's labels and markdown corpora moved to `spec/corpus/`; the web package passes them too.

### D3sk.ChatUi

- `ChatSession`: turn frames or D3sk.AgentCli events folded into rows, parts, badges, the working
  line and the pending approvals. It also handles resume, sending and dismissing. `ChatViewState.ToSnapshot()` is the
  conformance JSON.
- `IChatHost` and `ChatController`: Send (with "Sending…" and refusal), Stop, approvals, Fix.
- `ChatSpec`, `ToolText`, `ReplyTitle`, `AgentCliEvents`, `JsJson` (JavaScript's JSON and `toFixed`);
  `ChatLabels` reads its words from the spec and gains thinking, brand, permission and answer rules.

### D3sk.ChatUi.Avalonia

- `ChatView`: transcript, working line, approval panel (Allow / Deny / Later, AskUserQuestion with
  Other…), composer with Stop and the model and permission pickers; the host API of `mountChat`.
- `ChatRowPresenter` draws a session row. `TranscriptItem.Row` and `TranscriptItem.Pending` carry one
  through the virtualized `TranscriptView`. Palettes, sizes and action colours come from the spec.

### @d3sk/chat-ui (new, npm)

- `mountChat`: D3SKHAND's hub chat (transcript, stream, approvals) and AgentCliBridge's `/acb` chat
  box (vendored unchanged), with `feed`, `feedEvent`, `readSse`, `resume`, `setAgents`, `setPalette`.
- `createSession`, labels, tools, markdown, `eventToFrame`, the spec, all usable without a page.

## [0.1.0] - 2026-11-04

First release, ported from D3SKHAND's web chat.

### D3sk.ChatUi (no Avalonia types)

- Markdown: a whitelist model and an HTML writer that escapes everything else; D3SKHAND's
  `markdown.js` corpus passes case for case.
- Labels: status pills and the working phrases, equal to D3SKHAND's labels corpus.
- Composer state: attachment limits (10 files, 10 MB each, 25 MB in all) with an inline error,
  busy state, a draft per thread, the Architect prefix never twice.

### D3sk.ChatUi.Avalonia

- Markdown view: selectable text, code blocks and tables; a streaming append rebuilds only its
  last block; links follow the model's whitelist and are raised to the host, never opened.
- Virtualized transcript: only the visible rows plus a fixed buffer are realized, the scroll
  stays anchored while a reply streams, jump to bottom, tool summaries equal to D3SKHAND's corpus.
- Composer box, quick settings (model, permission and effort pickers emitting typed changes) and
  the slash menu.
- Theme: the palettes, text at least 13 px, contrast at least 4.5:1 tested for every palette, a
  visible focus.
