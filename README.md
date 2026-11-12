# D3sk.ChatUi

D3SKHAND's chat, identical in two front-ends, for any app that runs AI coding agents:

| Package | Where | What |
|---|---|---|
| `D3sk.ChatUi` | NuGet | The rules, with no UI types: the session that folds turn events into what the chat shows, labels, tool summaries, markdown to escaped HTML, the composer's state. |
| `D3sk.ChatUi.Avalonia` | NuGet | `ChatView`: virtualized transcript, working line, approval panel, composer with quick settings. |
| `@d3sk/chat-ui` | npm | `mountChat`: the same chat for a web page, taken from D3SKHAND's hub chat (transcript, stream, approvals) with AgentCliBridge's `/acb` chat box. |

D3SKHAND's web chat is the reference. Both front-ends follow it, and they are kept identical by
three shared things under [`spec/`](spec):

- [`spec/chat-ui.json`](spec/chat-ui.json): the design tokens (six palettes, action and brand colours,
  type sizes, radii, spacing) and every label (working phrases, badges, dialog texts, notices).
  D3sk.ChatUi embeds it and the web package imports it. Change a colour or a sentence there, never in a
  front-end.
- [`spec/conformance/`](spec/conformance): turn event streams. They include D3sk.AgentCli's own captures
  (`agentcli/`, frozen with a manifest) and hand-written turns: approvals, AskUserQuestion, injection guard,
  live input, queue, notices, resume, sending. Each case stores the snapshot the chat must reach at
  given moments. The C# `ChatSession` and the JS `createSession` must produce those snapshots exactly.
  Then each view is checked against them: `ChatView` (headless Avalonia) and `mountChat` (happy-dom) are
  read back by the web chat's class names, row by row.
- [`spec/corpus/`](spec/corpus): D3SKHAND's own corpora (labels, tool summaries, markdown), which both pass.

## Host API

The host gives the agents, feeds the turn's events, answers Send / Stop / approvals, and can resume a saved chat.
Events are D3sk.AgentCli's wire frames (`text`, `thinking`, `tool_call`, `tool_result`,
`approval_request`, `injection_detected`, `action_blocked`, `input_accepted`, `permission_mode`,
`notice`, `session`, `error`, `done`), plus the host's own frames:

- `start {user_message, agent, model, queued}`
- `dequeued`
- `live_input`
- `approval_resolved {approval_id, allow}`

A host that holds D3sk.AgentCli events feeds them as they are (`FeedEvent` / `feedEvent`).
D3SKHAND's hub SSE stream can be fed whole (`readSse`).

### C# (Avalonia)

```csharp
using D3sk.ChatUi.Avalonia.Chat;
using D3sk.ChatUi.Session;

sealed class MyHost : IChatHost
{
    public Task SendAsync(ChatSendRequest r, CancellationToken ct) => agents.SendAsync(r.Agent, r.Model, r.Text, ct);
    public Task StopAsync(CancellationToken ct) => agents.StopAsync(ct);
    public Task AnswerApprovalAsync(ChatApprovalAnswer a, CancellationToken ct) => agents.ApproveAsync(a.ApprovalId, a.Allow, a.Message, a.Answers, ct);
}

var chat = new ChatView(new MyHost(), [new ChatAgent("claude", "Claude Code", [new ChatModel("claude-opus-4-7", "Opus 4.7")])],
    projectRoot: "/work/app", palette: "teal");
window.Content = chat;

// From D3sk.AgentCli: var frame = Wire.ToWire(streamEvent); chat.Feed(frame.Name, frame.Data);
chat.Feed("start", new JsonObject { ["user_message"] = new JsonObject { ["id"] = "u1", ["content"] = "Fix the build" }, ["agent"] = "claude" });
chat.FeedEvent(JsonNode.Parse("""{"event":"TextDelta","text":"Looking…"}""")!.AsObject());
chat.Resume(savedChat);   // { messages, approvals, running, agent, model, permissionMode, sessionId }
```

`ChatSession` and `ChatController` (in `D3sk.ChatUi`) carry the same API without any UI, for a host
with its own view.

### Web

```js
import { mountChat } from "@d3sk/chat-ui";
import "@d3sk/chat-ui/chat.css";

const chat = mountChat(document.querySelector("#chat"), {
  agents: [{ id: "claude", name: "Claude Code", models: [{ id: "claude-opus-4-7", name: "Opus 4.7" }] }],
  projectRoot: "/work/app",
  palette: "teal",
  onSend: (message) => api.send(message),          // { text, files, command, model, permission, effort, … }
  onStop: () => api.stop(),
  onApproval: (answer) => api.approve(answer),     // { approvalId, allow, message?, answers? }
  onToolFix: (callId) => api.fix(callId),          // optional: shows Fix on failed tools
});
chat.feed("text", { text: "Hello" });
chat.feedEvent({ event: "ToolCall", call_id: "t1", name: "Bash", arguments: { command: "ls" } });
await chat.readSse(await fetch(`/api/conversations/${id}/stream`));
chat.resume(savedChat);
```

See [`packages/chat-ui/README.md`](packages/chat-ui/README.md).

### D3SKHAND

D3SKHAND moves its hub chat to `@d3sk/chat-ui` like this:

1. `mountChat` replaces `stream.js consume()`, the transcript and approval parts of
   `transcript.js` / `approvals.js`, and the `AgentUI.mountComposer` call in `composer.js`. The hub's
   SSE frames already have the right names (`delta` is read as `text`).
2. Hub-only parts stay in the hub, outside the package: follow-ups, sign-in links, terminal links,
   limit resume and the project pickers. They go into `chat.composer.elements.slot`. They stay in the hub because they call the hub's own APIs.
3. `onSend` is `sendMessage`, `onStop` is `stopReply`, `onApproval` posts to
   `/api/conversations/{id}/approvals/{approval_id}`, and `onToolFix` posts to `/tool-fix`.
   `resume()` takes `GET /api/conversations/{id}` as it is: `{ messages, approvals }` plus `running`.

## Build and test

Needs the .NET 10 SDK (`global.json`) and Node 20 or later.

```bash
dotnet test D3sk.ChatUi.slnx -c Release
cd packages/chat-ui && npm ci && npm test
```

After a deliberate rule change, update the snapshots from the web session with
`npm run conformance:write`. Read the diff, then make the C# side pass the same files.

## CI and release

`.github/workflows/ci.yml` runs both gates on the self-hosted Linux runner (`[self-hosted, kyber]`;
set the repository variable `CI_RUNS_ON` to a JSON label list to override). Tag `vX.Y.Z` and
`publish.yml` tests and publishes with the tag's version:

- the two NuGet packages go to GitHub Packages, and to nuget.org when the `NUGET_API_KEY` secret is set;
- `@d3sk/chat-ui` goes to npm when the `NPM_TOKEN` secret is set. The `d3sk` npm org must exist.

## Licence

MIT, see `LICENSE`. `packages/chat-ui/vendor/acb` is AgentCliBridge's chat box (MIT, same owner),
copied unchanged with a manifest.
