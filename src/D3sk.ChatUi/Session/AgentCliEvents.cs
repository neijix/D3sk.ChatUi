using System.Text.Json.Nodes;

namespace D3sk.ChatUi.Session;

/// <summary>A chat wire frame: its name (text, tool_call, done…) and its data.</summary>
public sealed record ChatFrame(string Name, JsonObject Data);

/// <summary>
/// D3sk.AgentCli's events in their JSON form (<c>{"event": "TextDelta", "text": …}</c>, as its stream
/// fixtures write them) to wire frames: the mapping of D3sk.AgentCli's <c>Wire.ToWire</c>, without
/// truncation. A host holding typed events calls <c>Wire.ToWire</c> itself and feeds the frame.
/// </summary>
public static class AgentCliEvents
{
    /// <summary>The frame for one event, or null for an event the chat does not show.</summary>
    public static ChatFrame? ToFrame(JsonObject? e)
    {
        if (e is null)
        {
            return null;
        }
        string S(string key) => Js.Str(e[key]);
        JsonNode? Clone(string key) => e[key]?.DeepClone();
        var data = new JsonObject { ["v"] = 1 };
        switch (Js.Str(e["event"]))
        {
            case "TextDelta":
                data["text"] = S("text");
                return new ChatFrame("text", data);
            case "ThinkingDelta":
                data["text"] = S("text");
                return new ChatFrame("thinking", data);
            case "ToolCall":
                data["call_id"] = S("call_id");
                data["name"] = S("name");
                data["arguments"] = Clone("arguments");
                return new ChatFrame("tool_call", data);
            case "ToolResult":
                data["call_id"] = S("call_id");
                data["output"] = S("output");
                data["is_error"] = Js.Truthy(e["is_error"]);
                return new ChatFrame("tool_result", data);
            case "ApprovalRequest":
                data["approval_id"] = S("request_id");
                data["call_id"] = S("call_id");
                data["name"] = S("name");
                data["arguments"] = Clone("arguments");
                data["description"] = S("description");
                return new ChatFrame("approval_request", data);
            case "InjectionDetected":
                data["call_id"] = S("call_id");
                data["tool_name"] = S("tool_name");
                data["rules"] = e["rules"] is JsonArray rules ? new JsonArray(rules.Select(r => (JsonNode?)JsonValue.Create(Js.Str(r))).ToArray()) : new JsonArray();
                data["excerpt"] = S("excerpt");
                data["details_url"] = S("details_url");
                return new ChatFrame("injection_detected", data);
            case "ActionBlocked":
                data["call_id"] = S("call_id");
                data["name"] = S("name");
                data["reason"] = S("reason");
                data["details_url"] = S("details_url");
                return new ChatFrame("action_blocked", data);
            case "InputAccepted":
                data["text"] = S("text");
                return new ChatFrame("input_accepted", data);
            case "ModeChanged":
                data["mode"] = S("mode");
                if (Js.Truthy(e["error"]))
                {
                    data["error"] = S("error");
                }
                return new ChatFrame("permission_mode", data);
            case "Notice":
                data["message"] = S("text");
                if (Js.Truthy(e["kind"]))
                {
                    data["notice_kind"] = S("kind");
                }
                return new ChatFrame("notice", data);
            case "SessionUpdate":
                data["session_id"] = S("session_id");
                return new ChatFrame("session", data);
            case "StreamError":
                data["message"] = S("message");
                data["code"] = Js.Truthy(e["code"]) ? S("code") : "cli_agent_failed";
                return new ChatFrame("error", data);
            case "Completed":
                data["returncode"] = e["returncode"] is JsonValue code && code.TryGetValue<int>(out var rc) ? rc : 0;
                return new ChatFrame("done", data);
            default:
                return null;
        }
    }
}

/// <summary>The few JavaScript coercions the session's rules are written with.</summary>
internal static class Js
{
    /// <summary>String(value), "" for null.</summary>
    public static string Str(JsonNode? node) => node switch
    {
        null => "",
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v when v.TryGetValue<double>(out var d) => JsJson.Number(d),
        JsonArray a => string.Join(",", a.Select(Str)),
        JsonObject => "[object Object]",
        _ => node.ToJsonString(),
    };

    /// <summary>Boolean(value).</summary>
    public static bool Truthy(JsonNode? node) => node switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0 && !double.IsNaN(d),
        _ => true,
    };

    /// <summary>value ?? fallback: null or missing gives the fallback.</summary>
    public static string StrOr(JsonNode? node, string fallback) => node is null ? fallback : Str(node);
}
