using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using D3sk.ChatUi.Labels;
using D3sk.ChatUi.Markdown;
using D3sk.ChatUi.Spec;
using D3sk.ChatUi.Tools;

namespace D3sk.ChatUi.Session;

/// <summary>A model an agent offers: its id and the name shown.</summary>
public sealed record ChatModel(string Id, string Name);

/// <summary>An agent the host offers: <paramref name="Id"/> is the CLI command (claude, grok…).</summary>
public sealed record ChatAgent(string Id, string Name, IReadOnlyList<ChatModel> Models)
{
    /// <summary>The agents in the host-API JSON form: <c>[{ "id", "name", "models": [{ "id", "name" }] }]</c>.</summary>
    public static IReadOnlyList<ChatAgent> FromJson(JsonArray? agents) =>
        (agents ?? []).OfType<JsonObject>().Select(a => new ChatAgent(
            Js.Str(a["id"]),
            Js.Str(a["name"]),
            (a["models"] as JsonArray ?? []).OfType<JsonObject>().Select(m => new ChatModel(Js.Str(m["id"]), Js.Str(m["name"]))).ToArray())).ToArray();
}

/// <summary>
/// The chat's state, folded from turn frames: D3SKHAND's hub stream.js done to plain data. The web
/// package's session.js is this class in JavaScript; both replay spec/conformance to the same
/// snapshots. Time is passed with each call (<c>at</c>, seconds): the working line and "Thought for
/// 3s" depend on it. Not thread-safe: call it from one thread (the UI's).
/// </summary>
public sealed partial class ChatSession
{
    private static readonly JsonObject L = ChatSpec.Labels;

    private sealed class Part
    {
        public string Type = "text";
        public string Text = "";
        public double StartedAt;
        public double? Seconds;
        public bool Closed;
        public bool Open;
        public string CallId = "";
        public string Name = "";
        public string Kind = "other";
        public JsonNode? Args;
        public string Status = "running";
        public string? Output;
        public bool IsError;
        public (string State, string? DetailsUrl)? Approval;
        public (IReadOnlyList<string> Rules, string Excerpt, string? DetailsUrl)? Injection;
    }

    private sealed class Row
    {
        public long Key;
        public long Rev;
        public string Kind = "";
        public JsonNode? Id;
        public string Text = "";
        public List<string> Attachments = [];
        public bool Transient;
        public string Agent = "";
        public string Model = "";
        public bool Live;
        public bool Typing;
        public List<Part> Parts = [];
        public Dictionary<string, Part> Tools = [];
        public Part? Current;
    }

    private readonly Func<double> _clock;
    private IReadOnlyList<ChatAgent> _agents;
    private List<Row> _rows = [];
    private List<JsonObject> _approvals = [];
    private Dictionary<string, string> _approvalCalls = [];
    private long _nextKey = 1;
    private string _agent = "";
    private string _model = "";
    private Row? _live;
    private double? _liveSince;
    private bool _usedTools;
    private string _activity = "starting";
    private double? _quietAt;
    private (string Text, List<string> Attachments, string Agent, string Model)? _pending;

    /// <summary>Creates a session. <paramref name="clock"/> gives "now" in seconds when a call passes no time.</summary>
    public ChatSession(IReadOnlyList<ChatAgent>? agents = null, string? projectRoot = null, Func<double>? clock = null)
    {
        _agents = agents ?? [];
        ProjectRoot = projectRoot ?? "";
        _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
    }

    /// <summary>Raised after every change the view should draw.</summary>
    public event EventHandler? Changed;

    /// <summary>Tool paths under it are shown relative to it.</summary>
    public string ProjectRoot { get; set; }

    /// <summary>True while a turn runs (start to done).</summary>
    public bool Busy { get; private set; }

    /// <summary>True while the turn waits for another chat of the project.</summary>
    public bool Queued { get; private set; }

    /// <summary>True when the running CLI takes messages sent while it works.</summary>
    public bool LiveInput { get; private set; }

    /// <summary>The permission mode the CLI really runs in, once it said.</summary>
    public string? PermissionMode { get; private set; }

    /// <summary>The CLI's own session id, for resuming.</summary>
    public string? SessionId { get; private set; }

    /// <summary>The agent of the running (or last) turn.</summary>
    public string Agent => _agent;

    /// <summary>The model of the running (or last) turn.</summary>
    public string Model => _model;

    /// <summary>"Now" by the session's clock.</summary>
    public double Now => _clock();

    /// <summary>The agents whose names the labels use.</summary>
    public void SetAgents(IReadOnlyList<ChatAgent> agents)
    {
        _agents = agents;
        OnChanged();
    }

    /// <summary>Folds one wire frame (text, tool_call, done… and the host's start). False when the name is unknown.</summary>
    public bool Feed(string name, JsonObject? data, double? at = null)
    {
        var now = at ?? _clock();
        Advance(now);
        var known = Apply(name, data ?? [], now);
        if (known)
        {
            OnChanged();
        }
        return known;
    }

    /// <summary>Folds one D3sk.AgentCli event in its JSON form. False when the chat does not show it.</summary>
    public bool FeedEvent(JsonObject? agentCliEvent, double? at = null) =>
        AgentCliEvents.ToFrame(agentCliEvent) is { } frame && Feed(frame.Name, frame.Data, at);

    /// <summary>
    /// Loads a saved chat: <c>{ messages: [{ id, role, content, parts, agent, model, attachments }], approvals,
    /// running, queued, agent, model, permissionMode, sessionId }</c>; a running turn's frames follow.
    /// </summary>
    public void Resume(JsonObject? state, double? at = null)
    {
        var now = at ?? _clock();
        state ??= [];
        _rows = [];
        _live = null;
        _pending = null;
        _approvalCalls = [];
        _quietAt = null;
        _liveSince = null;
        _usedTools = false;
        _activity = "starting";
        foreach (var message in (state["messages"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var parts = (message["parts"] as JsonArray ?? []).ToList();
            var role = Js.Str(message["role"]);
            if (role == "user")
            {
                UserRow(message);
            }
            else if (role == "error")
            {
                var row = PushRow(new Row { Kind = "error", Agent = Js.Str(message["agent"]), Text = Js.Str(message["content"]) });
                RenderSavedParts(row, parts.Where(p => p is JsonObject o && Js.Str(o["type"]) != "text").ToList(), now);
            }
            else if (role == "assistant")
            {
                var row = AssistantRow(message["id"]?.DeepClone(), Js.Str(message["agent"]), Js.Str(message["model"]), live: false);
                var content = Js.Str(message["content"]);
                List<JsonNode?> saved = parts.Count > 0 ? parts : content.Length > 0 ? [new JsonObject { ["type"] = "text", ["text"] = content }] : [];
                RenderSavedParts(row, saved, now);
            }
        }
        _approvals = (state["approvals"] as JsonArray ?? []).OfType<JsonObject>().Select(a =>
        {
            var copy = a.DeepClone().AsObject();
            copy["approval_id"] = Js.Str(a["approval_id"]);
            return copy;
        }).ToList();
        foreach (var approval in _approvals)
        {
            _approvalCalls[Js.Str(approval["approval_id"])] = Js.Str(approval["call_id"]);
        }
        Busy = Js.Truthy(state["running"]);
        Queued = Busy && Js.Truthy(state["queued"]);
        LiveInput = false;
        _agent = Js.Str(state["agent"]);
        _model = Js.Str(state["model"]);
        PermissionMode = state["permissionMode"] is null ? null : Js.Str(state["permissionMode"]);
        SessionId = Js.Truthy(state["sessionId"]) ? Js.Str(state["sessionId"]) : null;
        OnChanged();
    }

    /// <summary>The message just sent, drawn at once with "Sending…" until the host's start frame.</summary>
    public void MarkSending(string text, IEnumerable<string>? attachments = null, string? agent = null, string? model = null)
    {
        _pending = (text, (attachments ?? []).Where(a => a.Length > 0).ToList(), agent ?? "", model ?? "");
        OnChanged();
    }

    /// <summary>The send failed: the pending message goes.</summary>
    public void CancelSending()
    {
        if (_pending is null)
        {
            return;
        }
        _pending = null;
        OnChanged();
    }

    /// <summary>The approval was answered here: it leaves the queue until the turn says what happened.</summary>
    public void DismissApproval(string approvalId)
    {
        var before = _approvals.Count;
        _approvals = _approvals.Where(a => Js.Str(a["approval_id"]) != approvalId).ToList();
        if (before != _approvals.Count)
        {
            OnChanged();
        }
    }

    /// <summary>The conformance snapshot at <paramref name="at"/>.</summary>
    public JsonObject Snapshot(double? at = null) => View(at).ToSnapshot();

    /// <summary>Everything a view draws at <paramref name="at"/>, rows keyed for incremental drawing.</summary>
    public ChatViewState View(double? at = null)
    {
        var now = at ?? _clock();
        Advance(now);
        string? previousLabel = null;
        var rows = new List<ChatRowView>();
        foreach (var row in _rows)
        {
            switch (row.Kind)
            {
                case "user":
                    rows.Add(new ChatUserRow(row.Key, row.Rev, row.Id?.DeepClone(), row.Text, [.. row.Attachments]));
                    break;
                case "notice":
                    rows.Add(new ChatNoticeRow(row.Key, row.Rev, row.Text, row.Transient));
                    break;
                case "error":
                    rows.Add(new ChatErrorRow(
                        row.Key, row.Rev,
                        row.Agent.Length > 0 ? ChatSpec.Fill(ChatSpec.Label("errors", "labelWithAgent"), ("agent", AgentName(row.Agent))) : ChatSpec.Label("errors", "label"),
                        row.Text,
                        row.Parts.Select(p => PartView(p, row)).ToArray()));
                    break;
                default:
                    var label = string.Join(" · ", new[] { AgentName(row.Agent), ModelName(row.Agent, row.Model) }.Where(s => s.Length > 0));
                    var show = ChatLabels.ShouldShowAgentLabel(previousLabel, label);
                    previousLabel = label;
                    rows.Add(new ChatAssistantRow(
                        row.Key, row.Rev, row.Id?.DeepClone(), label, show, ChatLabels.AgentBrand(row.Agent, row.Model), row.Live,
                        WorkingView(row, now), row.Parts.Select(p => PartView(p, row)).ToArray()));
                    break;
            }
        }
        var pending = _pending is { } p
            ? new ChatPending(p.Text, [.. p.Attachments], ChatLabels.AgentBrand(p.Agent, p.Model), new ChatWorking(ChatLabels.SendingLine, false))
            : null;
        return new ChatViewState(Busy, Queued, LiveInput, PermissionMode, SessionId, pending, rows, _approvals.Select(ApprovalView).ToArray());
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private ChatAgent? AgentOf(string id) => _agents.FirstOrDefault(a => a.Id == id);

    private string AgentName(string id) => AgentOf(id) is { Name.Length: > 0 } agent ? agent.Name : id;

    private string ModelName(string id, string model) =>
        AgentOf(id)?.Models.FirstOrDefault(m => m.Id == model) is { Name.Length: > 0 } found ? found.Name : model;

    private bool IsKnown(JsonNode? id) =>
        id is not null && !(id is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0)
        && _rows.Any(r => JsonNode.DeepEquals(r.Id, id));

    private static void Touch(Row? row)
    {
        if (row is not null)
        {
            row.Rev++;
        }
    }

    private Row PushRow(Row row)
    {
        row.Key = _nextKey++;
        _rows.Add(row);
        return row;
    }

    private Row UserRow(JsonObject message) => PushRow(new Row
    {
        Kind = "user",
        Id = message["id"]?.DeepClone(),
        Text = Js.Str(message["content"] ?? message["text"]),
        Attachments = AttachmentNames(message["attachments"]),
    });

    private static List<string> AttachmentNames(JsonNode? list) =>
        (list as JsonArray ?? []).Select(item => item is JsonObject o ? Js.Str(o["name"]) : Js.Str(item)).Where(s => s.Length > 0).ToList();

    private Row AssistantRow(JsonNode? id, string agent, string model, bool live) =>
        PushRow(new Row { Kind = "assistant", Id = id, Agent = agent, Model = model, Live = live, Typing = live });

    private bool SweepTransient()
    {
        var before = _rows.Count;
        _rows = _rows.Where(r => !(r.Kind == "notice" && r.Transient)).ToList();
        return before != _rows.Count;
    }

    private static void CloseThinking(Row? row, double at)
    {
        var part = row?.Current;
        if (part is null || part.Type != "thinking" || part.Closed)
        {
            return;
        }
        part.Closed = true;
        part.Open = false;
        part.Seconds = Math.Max(1, at - part.StartedAt);
        Touch(row);
    }

    private static void AppendText(Row row, string type, string text, double at, double? seconds = null, bool closed = false)
    {
        if (row.Current is null || row.Current.Type != type)
        {
            CloseThinking(row, at);
            var part = type == "thinking"
                ? new Part { Type = "thinking", StartedAt = at, Seconds = seconds, Closed = closed, Open = !closed }
                : new Part { Type = "text" };
            row.Parts.Add(part);
            row.Current = part;
        }
        row.Current.Text += text;
        Touch(row);
    }

    private static Part AddTool(Row row, JsonObject call, double at)
    {
        CloseThinking(row, at);
        row.Current = null;
        var name = ToolText.Label(Js.Str(call["name"]));
        var part = new Part
        {
            Type = "tool",
            CallId = Js.Str(call["call_id"]),
            Name = name,
            Kind = ToolText.Kind(name),
            Args = call["arguments"]?.DeepClone(),
        };
        row.Parts.Add(part);
        var key = Js.Truthy(call["call_id"]) ? Js.Str(call["call_id"]) : $"anonymous-{row.Tools.Count}";
        row.Tools[key] = part;
        Touch(row);
        return part;
    }

    private static void MarkApproval(Row? row, Part? part, string decision, JsonNode? detailsUrl = null)
    {
        if (part is null)
        {
            return;
        }
        var url = Js.Str(detailsUrl);
        part.Approval = (decision, HttpUrl().IsMatch(url) ? url : null);
        Touch(row);
    }

    private static void MarkInjection(Row? row, Part? part, JsonObject injection)
    {
        if (part is null)
        {
            return;
        }
        var url = Js.Str(injection["details_url"]);
        part.Injection = (
            (injection["rules"] as JsonArray ?? []).Select(Js.Str).ToArray(),
            Js.Str(injection["excerpt"]),
            HttpUrl().IsMatch(url) ? url : null);
        Touch(row);
    }

    private static Part FinishTool(Row row, JsonObject result, double at)
    {
        var callId = Js.Str(result["call_id"]);
        var part = result["call_id"] is not null && row.Tools.TryGetValue(callId, out var known)
            ? known
            : AddTool(row, new JsonObject { ["call_id"] = result["call_id"]?.DeepClone(), ["name"] = "tool" }, at);
        var output = Js.Str(result["output"]);
        part.Output = output.Length > 0 ? output : ChatSpec.Label("tools", "output", "empty");
        part.IsError = Js.Truthy(result["is_error"]);
        part.Status = part.IsError ? "error" : "ok";
        var isWaiting = part.Approval?.State == "waiting";
        if (Js.Truthy(result["approval"]) && (part.Approval is null || isWaiting))
        {
            MarkApproval(row, part, Js.Str(result["approval"]), result["approval_details_url"]);
        }
        else if (part.Approval is null && ToolText.IsPermissionRefusal(output))
        {
            MarkApproval(row, part, "denied");
        }
        Touch(row);
        return part;
    }

    private static void SettleTools(Row row)
    {
        foreach (var part in row.Parts)
        {
            if (part.Type == "tool" && part.Status == "running")
            {
                part.Status = "unknown";
                part.Output = ChatSpec.Label("tools", "output", "noResult");
            }
        }
    }

    private static void RenderSavedParts(Row row, List<JsonNode?> parts, double at)
    {
        foreach (var node in parts)
        {
            if (node is not JsonObject part)
            {
                continue;
            }
            if (Js.Str(part["type"]) == "tool")
            {
                var tool = AddTool(row, part, at);
                if (Js.Truthy(part["approval"]))
                {
                    MarkApproval(row, tool, Js.Str(part["approval"]), part["approval_details_url"]);
                }
                if (Js.Truthy(part["injection"]) && part["injection"] is JsonObject injection)
                {
                    MarkInjection(row, tool, injection);
                }
                if (part["output"] is not null)
                {
                    FinishTool(row, part, at);
                }
            }
            else
            {
                double? seconds = part["seconds"] is JsonValue s && s.TryGetValue<double>(out var d) ? d : null;
                AppendText(row, Js.Str(part["type"]) == "thinking" ? "thinking" : "text", Js.Str(part["text"]), at, seconds, closed: true);
            }
        }
        row.Current = null;
        SettleTools(row);
    }

    private void Advance(double at)
    {
        if (_quietAt is { } quiet && at >= quiet)
        {
            _quietAt = null;
            if (_live is not null)
            {
                _live.Typing = false;
                Touch(_live);
            }
        }
    }

    private Row StartReply(double at)
    {
        _liveSince ??= at;
        _live = AssistantRow(null, _agent, _model, live: true);
        return _live;
    }

    private Row EnsureLive(double at)
    {
        if (_live is not null)
        {
            return _live;
        }
        Busy = true;
        return StartReply(at);
    }

    private static void CloseReply(Row? row, double at)
    {
        if (row is null)
        {
            return;
        }
        CloseThinking(row, at);
        row.Current = null;
        row.Typing = false;
        row.Live = false;
        Touch(row);
    }

    private void SetActivity(string activity)
    {
        _activity = activity;
        if (_live is { Typing: false })
        {
            _live.Typing = true;
            Touch(_live);
        }
    }

    private void FinishTurn(double at)
    {
        var row = _live;
        if (row is not null)
        {
            CloseReply(row, at);
            SettleTools(row);
            if (row.Parts.Count == 0)
            {
                _rows.Remove(row);
            }
        }
        _live = null;
        Busy = false;
        Queued = false;
        LiveInput = false;
        _quietAt = null;
        _approvals = [];
        SweepTransient();
    }

    private Part? ToolByCall(string? callId) =>
        callId is not null && _live is not null && _live.Tools.TryGetValue(callId, out var part) ? part : null;

    private bool Apply(string name, JsonObject d, double at)
    {
        switch (name)
        {
            case "start":
            {
                _pending = null;
                if (d["user_message"] is JsonObject user && !IsKnown(user["id"]))
                {
                    UserRow(user);
                }
                CloseReply(_live, at);
                _agent = Js.StrOr(d["agent"], _agent);
                _model = Js.StrOr(d["model"], _model);
                Busy = true;
                Queued = Js.Truthy(d["queued"]);
                LiveInput = false;
                _liveSince = null;
                _usedTools = false;
                _activity = "starting";
                _quietAt = null;
                StartReply(at);
                break;
            }
            case "dequeued":
                Queued = false;
                _liveSince = at;
                break;
            case "live_input":
                LiveInput = true;
                break;
            case "permission_mode":
                PermissionMode = Js.Str(d["mode"]);
                var mode = ChatLabels.PermissionLabel(Js.Str(d["mode"]));
                if (Js.Truthy(d["error"]))
                {
                    PushRow(new Row { Kind = "notice", Text = ChatSpec.Fill(ChatSpec.Label("notices", "modeStill"), ("mode", mode), ("error", Js.Str(d["error"]))) });
                }
                else
                {
                    PushRow(new Row { Kind = "notice", Text = ChatSpec.Fill(ChatSpec.Label("notices", "modeNow"), ("mode", mode)), Transient = true });
                }
                break;
            case "approval_request":
            {
                var id = Js.Str(d["approval_id"]);
                _approvalCalls[id] = Js.Str(d["call_id"]);
                if (!_approvals.Any(a => Js.Str(a["approval_id"]) == id))
                {
                    var copy = d.DeepClone().AsObject();
                    copy["approval_id"] = id;
                    _approvals.Add(copy);
                }
                MarkApproval(_live, ToolByCall(d["call_id"] is null ? null : Js.Str(d["call_id"])), "waiting");
                break;
            }
            case "approval_resolved":
            {
                var id = Js.Str(d["approval_id"]);
                _approvals = _approvals.Where(a => Js.Str(a["approval_id"]) != id).ToList();
                MarkApproval(_live, ToolByCall(_approvalCalls.GetValueOrDefault(id)), Js.Truthy(d["allow"]) ? "allowed" : "denied");
                break;
            }
            case "injection_detected":
                MarkInjection(_live, ToolByCall(d["call_id"] is null ? null : Js.Str(d["call_id"])), d);
                break;
            case "action_blocked":
                MarkApproval(_live, ToolByCall(d["call_id"] is null ? null : Js.Str(d["call_id"])), "blocked", d["details_url"]);
                break;
            case "input_accepted":
            {
                var row = _live;
                CloseReply(row, at);
                _quietAt = null;
                if (row is not null && d["reply"] is JsonObject reply)
                {
                    if (IsKnown(reply["id"]) && !JsonNode.DeepEquals(row.Id, reply["id"]))
                    {
                        _rows.Remove(row);
                    }
                    else if (reply["id"] is not null)
                    {
                        row.Id = reply["id"]!.DeepClone();
                    }
                }
                if (d["user_message"] is JsonObject user)
                {
                    if (!IsKnown(user["id"]))
                    {
                        UserRow(user);
                    }
                }
                else if (Js.Truthy(d["text"]))
                {
                    UserRow(new JsonObject { ["content"] = Js.Str(d["text"]) });
                }
                Busy = true;
                StartReply(at);
                break;
            }
            case "text" or "delta":
            {
                var row = EnsureLive(at);
                SweepTransient();
                AppendText(row, "text", Js.Str(d["text"]), at);
                SetActivity(_usedTools ? "wrapping" : "writing");
                _quietAt = at + ChatLabels.QuietSeconds;
                break;
            }
            case "thinking":
            {
                var row = EnsureLive(at);
                SweepTransient();
                _quietAt = null;
                AppendText(row, "thinking", Js.Str(d["text"]), at);
                SetActivity("thinking");
                break;
            }
            case "tool_call":
            {
                var row = EnsureLive(at);
                SweepTransient();
                _quietAt = null;
                AddTool(row, d, at);
                _usedTools = true;
                SetActivity(ChatLabels.WorkingActivity(ToolText.Label(Js.Str(d["name"]))));
                break;
            }
            case "tool_result":
            {
                var row = EnsureLive(at);
                FinishTool(row, d, at);
                if (Js.Truthy(d["is_error"]))
                {
                    SetActivity("recovering");
                }
                break;
            }
            case "session":
                if (Js.Truthy(d["session_id"]))
                {
                    SessionId = Js.Str(d["session_id"]);
                }
                break;
            case "notice":
                PushRow(new Row { Kind = "notice", Text = Js.Str(d["message"]), Transient = Js.Truthy(d["transient"]) });
                break;
            case "error":
                PushRow(new Row { Kind = "error", Agent = _agent, Text = Js.Str(d["message"]) });
                break;
            case "done":
                FinishTurn(at);
                break;
            default:
                return false;
        }
        return true;
    }

    private ChatPartView PartView(Part part, Row row)
    {
        if (part.Type == "text")
        {
            return new ChatTextPart(ReplyTitle.Hide(part.Text, row.Live));
        }
        if (part.Type == "thinking")
        {
            var label = part.Closed ? ChatLabels.ThinkingLabel(false, seconds: part.Seconds) : ChatLabels.ThinkingLabel(true, part.Text.Length);
            return new ChatThinkingPart(part.Text, label, part.Open);
        }
        var diff = ToolText.Diff(part.Kind, part.Args);
        var output = part.Output ?? ChatSpec.Label("tools", "output", "running");
        ChatBadge? approval = part.Approval is { } a
            ? new ChatBadge(a.State, L["approval"]!["badges"]![a.State] is JsonValue badge ? badge.GetValue<string>() : a.State, a.DetailsUrl)
            : null;
        ChatInjection? injection = part.Injection is { } i
            ? new ChatInjection(
                ChatSpec.Label("approval", "injection"),
                ChatSpec.Fill(ChatSpec.Label("approval", "injectionTitle"), ("rules", string.Join(", ", i.Rules)))
                    + (i.Excerpt.Length > 0 ? $"\n\n“{i.Excerpt}”" : ""),
                i.DetailsUrl)
            : null;
        var refused = approval is { State: "denied" or "blocked" };
        return new ChatToolPart(
            part.CallId,
            part.Name,
            part.Kind,
            part.Kind == "read",
            ToolText.Summarize(part.Kind, part.Args, ProjectRoot),
            part.Status,
            ChatSpec.Label("tools", "status", part.Status),
            diff is null ? ToolText.Input(part.Args) : null,
            diff,
            output,
            diff is null || part.IsError,
            approval,
            injection,
            part.IsError && part.CallId.Length > 0 && !refused);
    }

    private ChatWorking? WorkingView(Row row, double at)
    {
        if (!row.Live || !row.Typing || row != _live)
        {
            return null;
        }
        if (Queued)
        {
            return new ChatWorking(ChatLabels.QueuedLine, true);
        }
        var since = _liveSince ?? at;
        return new ChatWorking(ChatLabels.WorkingPhrase(_activity, Math.Max(0, at - since)), false);
    }

    private ChatApprovalView ApprovalView(JsonObject approval)
    {
        var args = approval["arguments"];
        var agentName = AgentName(_agent);
        var agent = agentName.Length > 0 ? agentName : ChatSpec.Label("approval", "agentFallback");
        var toolName = Js.Str(approval["name"]);
        if (toolName == "AskUserQuestion" && args is JsonObject a && a["questions"] is JsonArray questions)
        {
            var description = Js.Str(approval["description"]);
            return new ChatApprovalView(
                Js.Str(approval["approval_id"]),
                Js.Str(approval["call_id"]),
                true,
                ChatSpec.Fill(ChatSpec.Label("approval", "questionTitle"), ("agent", agent)),
                description.Length > 0 ? description : ChatSpec.Label("approval", "questionSummary"),
                toolName,
                "other",
                null,
                null,
                questions.Select(q => new ChatQuestion(
                    Js.Str(q?["question"]),
                    Js.Str(q?["header"]),
                    q?["multiSelect"] is JsonValue m && m.TryGetValue<bool>(out var multi) && multi,
                    (q?["options"] as JsonArray ?? []).Select(o => new ChatQuestionOption(Js.Str(o?["label"]), Js.Str(o?["description"]))).ToArray())).ToArray());
        }
        var name = ToolText.Label(toolName);
        var kind = ToolText.Kind(name);
        var diff = ToolText.Diff(kind, args);
        var summary = ToolText.Summarize(kind, args, ProjectRoot);
        var parts = new[] { Js.Str(approval["description"]), summary }.Where(s => s.Length > 0).Distinct();
        return new ChatApprovalView(
            Js.Str(approval["approval_id"]),
            Js.Str(approval["call_id"]),
            false,
            ChatSpec.Fill(ChatSpec.Label("approval", "title"), ("agent", agent), ("tool", name)),
            string.Join(" · ", parts),
            name,
            kind,
            diff is null ? ToolText.Input(args) : null,
            diff,
            []);
    }

    [GeneratedRegex("^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrl();
}
