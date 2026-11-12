using System.Text.Json.Nodes;
using D3sk.ChatUi.Tools;

namespace D3sk.ChatUi.Session;

/// <summary>The line under a reply being written: its words, and whether the turn is still queued.</summary>
public sealed record ChatWorking(string Text, bool Queued);

/// <summary>One part of a reply, in arrival order: text, thinking or a tool call.</summary>
public abstract record ChatPartView;

/// <summary>Markdown text (the hidden title line already taken out).</summary>
public sealed record ChatTextPart(string Text) : ChatPartView;

/// <summary>A thinking block: <paramref name="Label"/> is "Thinking… · 1.2k tokens" or "Thought for 3s".</summary>
public sealed record ChatThinkingPart(string Text, string Label, bool Open) : ChatPartView;

/// <summary>An approval badge on a tool: waiting, allowed, denied or blocked.</summary>
public sealed record ChatBadge(string State, string Label, string? DetailsUrl);

/// <summary>The injection guard's badge on a tool whose result looked like a prompt injection.</summary>
public sealed record ChatInjection(string Label, string Title, string? DetailsUrl);

/// <summary>A tool call: its line (name, summary, status, badges) and its box (IN or diff, OUT).</summary>
public sealed record ChatToolPart(
    string CallId,
    string Name,
    string Kind,
    bool Compact,
    string Summary,
    string Status,
    string StatusLabel,
    string? Input,
    IReadOnlyList<DiffLine>? Diff,
    string Output,
    bool OutputShown,
    ChatBadge? Approval,
    ChatInjection? Injection,
    bool Fixable) : ChatPartView;

/// <summary>One row of the transcript. <see cref="Key"/> stays the same for the row's life; <see cref="Rev"/> grows when it changes.</summary>
public abstract record ChatRowView(long Key, long Rev);

/// <summary>The person's message.</summary>
public sealed record ChatUserRow(long Key, long Rev, JsonNode? Id, string Text, IReadOnlyList<string> Attachments) : ChatRowView(Key, Rev);

/// <summary>A reply: label shown only when it differs from the reply before; working is null when the line is down.</summary>
public sealed record ChatAssistantRow(
    long Key,
    long Rev,
    JsonNode? Id,
    string Label,
    bool ShowLabel,
    string Brand,
    bool Live,
    ChatWorking? Working,
    IReadOnlyList<ChatPartView> Parts) : ChatRowView(Key, Rev);

/// <summary>A status line; a transient one goes once the reply runs again.</summary>
public sealed record ChatNoticeRow(long Key, long Rev, string Text, bool Transient) : ChatRowView(Key, Rev);

/// <summary>An error, with the activity of the turn that failed.</summary>
public sealed record ChatErrorRow(long Key, long Rev, string Label, string Text, IReadOnlyList<ChatPartView> Parts) : ChatRowView(Key, Rev);

/// <summary>The message just sent, shown with "Sending…" until the host's start frame.</summary>
public sealed record ChatPending(string Text, IReadOnlyList<string> Attachments, string Brand, ChatWorking Working);

/// <summary>An option of an AskUserQuestion question.</summary>
public sealed record ChatQuestionOption(string Label, string Description);

/// <summary>One AskUserQuestion question.</summary>
public sealed record ChatQuestion(string Question, string Header, bool MultiSelect, IReadOnlyList<ChatQuestionOption> Options);

/// <summary>A pending approval as the dialog shows it.</summary>
public sealed record ChatApprovalView(
    string ApprovalId,
    string CallId,
    bool IsQuestion,
    string Title,
    string Summary,
    string Tool,
    string Kind,
    string? Input,
    IReadOnlyList<DiffLine>? Diff,
    IReadOnlyList<ChatQuestion> Questions);

/// <summary>Everything a view draws at one moment. <see cref="ToSnapshot"/> is the conformance JSON both front-ends must equal.</summary>
public sealed record ChatViewState(
    bool Busy,
    bool Queued,
    bool LiveInput,
    string? PermissionMode,
    string? SessionId,
    ChatPending? Pending,
    IReadOnlyList<ChatRowView> Rows,
    IReadOnlyList<ChatApprovalView> Approvals)
{
    /// <summary>The snapshot, with the same keys and values as @d3sk/chat-ui's <c>session.snapshot()</c>.</summary>
    public JsonObject ToSnapshot() => new()
    {
        ["busy"] = Busy,
        ["queued"] = Queued,
        ["liveInput"] = LiveInput,
        ["permissionMode"] = PermissionMode,
        ["sessionId"] = SessionId,
        ["pending"] = Pending is null ? null : new JsonObject
        {
            ["text"] = Pending.Text,
            ["attachments"] = Strings(Pending.Attachments),
            ["brand"] = Pending.Brand,
            ["working"] = Working(Pending.Working),
        },
        ["rows"] = new JsonArray(Rows.Select(Row).ToArray()),
        ["approvals"] = new JsonArray(Approvals.Select(Approval).ToArray()),
    };

    private static JsonArray Strings(IEnumerable<string> list) => new(list.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());

    private static JsonObject? Working(ChatWorking? working) =>
        working is null ? null : new JsonObject { ["text"] = working.Text, ["queued"] = working.Queued };

    private static JsonArray? Diff(IReadOnlyList<DiffLine>? diff) =>
        diff is null ? null : new JsonArray(diff.Select(d => (JsonNode?)new JsonObject { ["kind"] = d.Kind, ["text"] = d.Text }).ToArray());

    private static JsonArray Parts(IEnumerable<ChatPartView> parts) => new(parts.Select(Part).ToArray());

    private static JsonNode? Row(ChatRowView row) => row switch
    {
        ChatUserRow u => new JsonObject { ["kind"] = "user", ["id"] = u.Id?.DeepClone(), ["text"] = u.Text, ["attachments"] = Strings(u.Attachments) },
        ChatNoticeRow n => new JsonObject { ["kind"] = "notice", ["text"] = n.Text, ["transient"] = n.Transient },
        ChatErrorRow e => new JsonObject { ["kind"] = "error", ["label"] = e.Label, ["text"] = e.Text, ["parts"] = Parts(e.Parts) },
        ChatAssistantRow a => new JsonObject
        {
            ["kind"] = "assistant",
            ["id"] = a.Id?.DeepClone(),
            ["label"] = a.Label,
            ["showLabel"] = a.ShowLabel,
            ["brand"] = a.Brand,
            ["live"] = a.Live,
            ["working"] = Working(a.Working),
            ["parts"] = Parts(a.Parts),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };

    /// <summary>One part as it appears in the snapshot (a view uses it as the part's signature).</summary>
    public static JsonNode? Part(ChatPartView part) => part switch
    {
        ChatTextPart t => new JsonObject { ["type"] = "text", ["text"] = t.Text },
        ChatThinkingPart t => new JsonObject { ["type"] = "thinking", ["text"] = t.Text, ["label"] = t.Label, ["open"] = t.Open },
        ChatToolPart t => new JsonObject
        {
            ["type"] = "tool",
            ["callId"] = t.CallId,
            ["name"] = t.Name,
            ["kind"] = t.Kind,
            ["compact"] = t.Compact,
            ["summary"] = t.Summary,
            ["status"] = t.Status,
            ["statusLabel"] = t.StatusLabel,
            ["input"] = t.Input,
            ["diff"] = Diff(t.Diff),
            ["output"] = t.Output,
            ["outputShown"] = t.OutputShown,
            ["approval"] = t.Approval is null ? null : new JsonObject { ["state"] = t.Approval.State, ["label"] = t.Approval.Label, ["detailsUrl"] = t.Approval.DetailsUrl },
            ["injection"] = t.Injection is null ? null : new JsonObject { ["label"] = t.Injection.Label, ["title"] = t.Injection.Title, ["detailsUrl"] = t.Injection.DetailsUrl },
            ["fixable"] = t.Fixable,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(part)),
    };

    private static JsonNode? Approval(ChatApprovalView a) => new JsonObject
    {
        ["approvalId"] = a.ApprovalId,
        ["callId"] = a.CallId,
        ["isQuestion"] = a.IsQuestion,
        ["title"] = a.Title,
        ["summary"] = a.Summary,
        ["tool"] = a.Tool,
        ["kind"] = a.Kind,
        ["input"] = a.Input,
        ["diff"] = Diff(a.Diff),
        ["questions"] = new JsonArray(a.Questions.Select(q => (JsonNode?)new JsonObject
        {
            ["question"] = q.Question,
            ["header"] = q.Header,
            ["multiSelect"] = q.MultiSelect,
            ["options"] = new JsonArray(q.Options.Select(o => (JsonNode?)new JsonObject { ["label"] = o.Label, ["description"] = o.Description }).ToArray()),
        }).ToArray()),
    };
}
