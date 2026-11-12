# @d3sk/chat-ui

D3SKHAND's chat for any web page: the transcript (markdown, thinking, tool calls with IN/OUT and
diffs, badges), the working line, the approval dialog with AskUserQuestion, and AgentCliBridge's chat box.
You feed it D3sk.AgentCli turn events. No build step and no framework: ES modules plus one stylesheet.

The same chat exists for Avalonia as the NuGet package `D3sk.ChatUi.Avalonia`. Both follow one spec
(tokens and labels) and pass the same conformance fixtures. See the
[repository](https://github.com/neijix/D3sk.ChatUi).

```bash
npm install @d3sk/chat-ui
```

```js
import { mountChat } from "@d3sk/chat-ui";
import "@d3sk/chat-ui/chat.css"; // or <link rel="stylesheet" href=".../chat.css">

const chat = mountChat(document.querySelector("#chat"), {
  agents: [{ id: "claude", name: "Claude Code", models: [{ id: "claude-opus-4-7", name: "Opus 4.7" }] }],
  palette: "teal",                                   // teal, slate, midnight, ocean, forest, dusk
  projectRoot: "/work/app",                          // tool paths under it are shown relative
  onSend: async (message) => { /* { text, files, command, model, permission, effort, … }; reject to give the words back */ },
  onStop: () => { /* stop the running turn */ },
  onApproval: async ({ approvalId, allow, message, answers }) => { /* reject to keep it pending */ },
  onToolFix: async (callId) => { /* optional: shows Fix on failed tools */ },
  onLink: (href, event) => { /* optional: links in the agent's text */ },
  onError: (message) => { /* the box's toasts */ },
  composer: { /* more AgentUI.mountComposer options: permission, effort, slashCommands, … */ },
});
```

## Feeding a turn

| Call | Takes |
|---|---|
| `chat.feed(name, data)` | A wire frame: `start`, `text` (or `delta`), `thinking`, `tool_call`, `tool_result`, `approval_request`, `approval_resolved`, `injection_detected`, `action_blocked`, `input_accepted`, `permission_mode`, `notice`, `session`, `live_input`, `dequeued`, `error`, `done`. |
| `chat.feedEvent(event)` | A D3sk.AgentCli event in its JSON form: `{ event: "TextDelta", text }`, `{ event: "ToolCall", … }`, … |
| `await chat.readSse(response)` | A `fetch` response whose body is Server-Sent Events (`event:` / `data:`). |
| `chat.resume(saved)` | A saved chat: `{ messages: [{ id, role, content, parts, agent, model, attachments }], approvals, running, agent, model, permissionMode, sessionId }`. |
| `chat.setAgents(list)`, `chat.setPalette(id)` | Change the agents or the colours. |
| `chat.destroy()` | Unmount. |

`start { user_message, agent, model, queued }` opens a reply. A host that never sends it still gets
one on the first text or tool. `done` ends the turn.

## Without the DOM

`createSession()` is the chat's state with no page: `feed`, `feedEvent`, `resume`, `view(at)` and
`snapshot(at)`. The labels and tool-summary helpers, `renderMarkdown` (escapes everything outside its
whitelist), `eventToFrame` and the spec (`spec`, or `@d3sk/chat-ui/spec/chat-ui.json`) are exported too.

## Licence

MIT. `vendor/acb` is AgentCliBridge's chat box (MIT), copied unchanged.
