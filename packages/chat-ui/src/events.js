// D3sk.AgentCli's events, as its stream fixtures write them ({ "event": "TextDelta", "text": … }),
// to the chat's wire frames ([name, data]): the same mapping as D3sk.AgentCli's Wire.ToWire, with
// no truncation (the host already bounded what it sends). A C# host calls Wire.ToWire itself and
// feeds D3sk.ChatUi the frame; D3sk.ChatUi's AgentCliEvents does what this file does.

const str = (value) => (value === undefined || value === null ? "" : String(value));

/** [name, data] for one AgentCli event, or null for an event the chat does not show. */
export function eventToFrame(event) {
  if (!event || typeof event !== "object") return null;
  const v = 1;
  switch (event.event) {
    case "TextDelta": return ["text", { v, text: str(event.text) }];
    case "ThinkingDelta": return ["thinking", { v, text: str(event.text) }];
    case "ToolCall": return ["tool_call", { v, call_id: str(event.call_id), name: str(event.name), arguments: event.arguments ?? null }];
    case "ToolResult": return ["tool_result", { v, call_id: str(event.call_id), output: str(event.output), is_error: Boolean(event.is_error) }];
    case "ApprovalRequest": return ["approval_request", {
      v, approval_id: str(event.request_id), call_id: str(event.call_id), name: str(event.name),
      arguments: event.arguments ?? null, description: str(event.description),
    }];
    case "InjectionDetected": return ["injection_detected", {
      v, call_id: str(event.call_id), tool_name: str(event.tool_name), rules: Array.isArray(event.rules) ? event.rules.map(str) : [],
      excerpt: str(event.excerpt), details_url: str(event.details_url),
    }];
    case "ActionBlocked": return ["action_blocked", {
      v, call_id: str(event.call_id), name: str(event.name), reason: str(event.reason), details_url: str(event.details_url),
    }];
    case "InputAccepted": return ["input_accepted", { v, text: str(event.text) }];
    case "ModeChanged": return ["permission_mode", event.error ? { v, mode: str(event.mode), error: str(event.error) } : { v, mode: str(event.mode) }];
    case "Notice": return ["notice", event.kind ? { v, message: str(event.text), notice_kind: str(event.kind) } : { v, message: str(event.text) }];
    case "SessionUpdate": return ["session", { v, session_id: str(event.session_id) }];
    case "StreamError": return ["error", { v, message: str(event.message), code: str(event.code || "cli_agent_failed") }];
    case "Completed": return ["done", { v, returncode: Number(event.returncode) || 0 }];
    default: return null;
  }
}
