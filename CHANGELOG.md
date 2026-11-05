# Changelog

All notable changes to `D3sk.ChatUi` and `D3sk.ChatUi.Avalonia`. Versions follow
[Semantic Versioning](https://semver.org).

## [Unreleased]

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
