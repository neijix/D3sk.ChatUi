using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Labels;

/// <summary>The fields of a chat that its left-pane status pill reads.</summary>
public sealed record ChatStatusInput(
    bool Queued = false,
    bool Running = false,
    bool TerminalWorking = false,
    double? ResumeAt = null,
    bool HasError = false,
    bool Unread = false);

/// <summary>A status pill: its kind (queued, running, resuming, error, done) and its text.</summary>
public sealed record StatusPill(string Kind, string Text);

/// <summary>
/// Small wording rules of the transcript, equal to the web UI's labels.js (and @d3sk/chat-ui's).
/// Every word comes from the shared spec (spec/chat-ui.json); the working phrases are the owner's
/// Québécois list (2026-09-19).
/// </summary>
public static partial class ChatLabels
{
    private static readonly JsonObject Working = ChatSpec.Labels["working"]!.AsObject();

    /// <summary>Activity name to the two wordings its line alternates between (spec <c>labels.working.phrases</c>).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> WorkingPhrases { get; } =
        Working["phrases"]!.AsObject().ToDictionary(p => p.Key, p => ChatSpec.Strings(p.Value));

    /// <summary>Phrases for very long requests; the last one leads when the agent is wired.</summary>
    public static IReadOnlyList<string> WorkingLongHaul { get; } = ChatSpec.Strings(Working["longHaul"]);

    /// <summary>The plain words, for places too small for a phrase (the agent map).</summary>
    public static IReadOnlyList<string> WorkingWords { get; } = ChatSpec.Strings(Working["words"]);

    /// <summary>The line of a queued message.</summary>
    public static string QueuedLine { get; } = ChatSpec.Label("lines", "queued");

    /// <summary>The line between Send and the host's start.</summary>
    public static string SendingLine { get; } = ChatSpec.Label("lines", "sending");

    /// <summary>The line while the chat's terminal is at a reply.</summary>
    public static string TerminalWorkingLine { get; } = ChatSpec.Label("lines", "terminalWorking");

    /// <summary>Seconds of quiet after text before the working line goes.</summary>
    public static double QuietSeconds { get; } = Working["quietSeconds"]!.GetValue<double>();

    /// <summary>The permission modes, value then label, in menu order.</summary>
    public static IReadOnlyList<(string Value, string Label)> PermissionModes { get; } =
        ChatSpec.Labels["permissions"]!["modes"]!.AsArray()
            .Select(m => (m![0]!.GetValue<string>(), m[1]!.GetValue<string>())).ToArray();

    private static readonly int StepSeconds = Working["stepSeconds"]!.GetValue<int>();
    private static readonly int LongSeconds = Working["longSeconds"]!.GetValue<int>();
    private static readonly int VeryLongSeconds = Working["veryLongSeconds"]!.GetValue<int>();
    private static readonly IReadOnlyList<string> WeekdayNames = ChatSpec.Strings(ChatSpec.Labels["status"]!["weekdays"]);

    /// <summary>
    /// The left-pane pill: null when there is nothing to show. <paramref name="zone"/> is the clock
    /// the "Resumes" time is written in (default: this computer's).
    /// </summary>
    public static StatusPill? ChatStatusLabel(ChatStatusInput? chat, double nowSeconds, TimeZoneInfo? zone = null)
    {
        if (chat is null)
        {
            return null;
        }
        if (chat.Queued)
        {
            return new StatusPill("queued", ChatSpec.Label("status", "queued"));
        }
        if (chat.Running || chat.TerminalWorking)
        {
            return new StatusPill("running", ChatSpec.Label("status", "running"));
        }
        if (chat.ResumeAt is { } resumeAt && resumeAt != 0 && !double.IsNaN(resumeAt))
        {
            var when = resumeAt > nowSeconds ? ResumeTimeText(resumeAt, nowSeconds, zone ?? TimeZoneInfo.Local) : ChatSpec.Label("status", "soon");
            return new StatusPill("resuming", ChatSpec.Fill(ChatSpec.Label("status", "resumes"), ("when", when)));
        }
        if (chat.HasError)
        {
            return new StatusPill("error", ChatSpec.Label("status", "error"));
        }
        return chat.Unread ? new StatusPill("done", ChatSpec.Label("status", "done")) : null;
    }

    /// <summary>"00:52" today, "Mon 00:52" on another day, 24-hour.</summary>
    public static string ResumeTimeText(double resumeAt, double nowSeconds, TimeZoneInfo zone)
    {
        var at = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(resumeAt * 1000)), zone);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(nowSeconds * 1000)), zone);
        var clock = string.Create(CultureInfo.InvariantCulture, $"{at.Hour:00}:{at.Minute:00}");
        return at.Date == today.Date ? clock : $"{WeekdayNames[(int)at.DayOfWeek]} {clock}";
    }

    /// <summary>What the agent is doing, from the tool it just called (names as Claude Code spells them).</summary>
    public static string WorkingActivity(string? toolName)
    {
        var name = (toolName ?? "").ToLowerInvariant();
        if (StartsWithAny(name, "bash", "shell", "run_", "execute") || name is "killshell" or "bashoutput")
        {
            return "shell";
        }
        if (StartsWithAny(name, "read", "grep", "glob", "list", "ls", "view", "webfetch", "websearch", "toolsearch", "codebase_search"))
        {
            return "searching";
        }
        if (StartsWithAny(name, "edit", "multiedit", "search_replace", "str_replace", "replace", "notebookedit"))
        {
            return "editing";
        }
        if (StartsWithAny(name, "write", "create"))
        {
            return "writing";
        }
        return StartsWithAny(name, "task", "agent", "skill") ? "assembling" : "handling";
    }

    /// <summary>The line under a reply being written. Deterministic: same inputs, same words.</summary>
    public static string WorkingPhrase(string? activity = "starting", double elapsedSeconds = 0)
    {
        var pool = WorkingPhrases[activity is not null && WorkingPhrases.ContainsKey(activity) ? activity : "starting"];
        var step = (long)Math.Max(0, Math.Floor(elapsedSeconds / StepSeconds));
        var every = elapsedSeconds >= VeryLongSeconds ? 2 : elapsedSeconds >= LongSeconds ? 3 : 0;
        if (every != 0 && step % every == every - 1)
        {
            var longFrom = (LongSeconds + StepSeconds - 1) / StepSeconds;
            var veryLongFrom = (VeryLongSeconds + StepSeconds - 1) / StepSeconds;
            var turn = step < veryLongFrom
                ? (step - longFrom) / 3
                : ((veryLongFrom - longFrom + 2) / 3) + ((step - veryLongFrom) / 2);
            var wired = activity is "shell" or "searching";
            var last = WorkingLongHaul.Count - 1;
            var index = (int)(turn % WorkingLongHaul.Count);
            // Wired: the last phrase first, then the others in order.
            return WorkingLongHaul[wired ? (index == 0 ? last : index - 1) : index];
        }
        return pool[(int)(step % pool.Count)];
    }

    /// <summary>One plain word per chat, always the same for that chat (the agent map uses it).</summary>
    public static string WorkingWord(string? seed)
    {
        uint hash = 0;
        var text = seed ?? "";
        for (var i = 0; i < text.Length; i++)
        {
            int codePoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            hash = unchecked((hash * 31) + (uint)codePoint);
        }
        return WorkingWords[(int)(hash % (uint)WorkingWords.Count)];
    }

    /// <summary>"1.2k" from 1234, "999" from 999: the token count wording of the context meter.</summary>
    public static string FormatTokenCount(double tokens) =>
        tokens >= 1000
            ? Session.JsJson.ToFixed(tokens / 1000, 1) + "k"
            : tokens.ToString(CultureInfo.InvariantCulture);

    /// <summary>JavaScript's Math.round: halves go up.</summary>
    public static double RoundHalfUp(double value) => Math.Floor(value + 0.5);

    /// <summary>"Thinking… · 10.2k tokens" while written, "Thought for 12s" once it ended, "Thought" when untimed.</summary>
    public static string ThinkingLabel(bool isStreaming, int characters = 0, double? seconds = null)
    {
        if (isStreaming)
        {
            var tokens = RoundHalfUp(characters / 4.0);
            return tokens != 0
                ? ChatSpec.Fill(ChatSpec.Label("thinking", "streamingTokens"), ("tokens", FormatTokenCount(tokens)))
                : ChatSpec.Label("thinking", "streaming");
        }
        return seconds is > 0
            ? ChatSpec.Fill(ChatSpec.Label("thinking", "thoughtFor"), ("duration", FormatThinkingSeconds(seconds.Value)))
            : ChatSpec.Label("thinking", "thought");
    }

    /// <summary>A reply names its subscription · model only when that differs from the reply before it.</summary>
    public static bool ShouldShowAgentLabel(string? previousLabel, string? label) =>
        !string.IsNullOrEmpty(label) && label != previousLabel;

    /// <summary>Whose mark spins under a reply: the model says more than the CLI.</summary>
    public static string AgentBrand(string? agent, string? model)
    {
        var name = (model ?? "").ToLowerInvariant();
        if (ClaudeModel().IsMatch(name))
        {
            return "claude";
        }
        if (GeminiModel().IsMatch(name))
        {
            return "gemini";
        }
        if (OpenAiModel().IsMatch(name))
        {
            return "openai";
        }
        if (GrokModel().IsMatch(name))
        {
            return "grok";
        }
        return (agent ?? "").ToLowerInvariant() switch
        {
            "claude" => "claude",
            "gemini" or "agy" => "gemini",
            "grok" => "grok",
            "copilot" => "copilot",
            "agent" or "cursor" or "cursor-agent" => "cursor",
            "codex" => "openai",
            "mlx" => "mlx",
            "colibri" => "colibri",
            _ => "d3skhand",
        };
    }

    /// <summary>"Accept edits" from "acceptEdits"; an unknown mode is shown as is.</summary>
    public static string PermissionLabel(string? mode)
    {
        foreach (var (value, label) in PermissionModes)
        {
            if (value == mode)
            {
                return label;
            }
        }
        return mode ?? "";
    }

    /// <summary>
    /// What goes back to the agent for an AskUserQuestion, keyed by question text: the picked
    /// label (a list for a multi-select), with the words typed under "Other…" (null when inactive).
    /// </summary>
    public static JsonObject QuestionAnswers(
        IEnumerable<(string Question, bool MultiSelect)> questions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> picked,
        IReadOnlyDictionary<string, string?> own)
    {
        var answers = new JsonObject();
        foreach (var (question, multiSelect) in questions)
        {
            if (string.IsNullOrEmpty(question))
            {
                continue;
            }
            var typed = own.TryGetValue(question, out var words) ? (words ?? "").Trim() : "";
            var labels = picked.TryGetValue(question, out var list) ? list.ToList() : [];
            if (multiSelect)
            {
                if (typed.Length > 0)
                {
                    labels.Add(typed);
                }
                if (labels.Count > 0)
                {
                    answers[question] = new JsonArray(labels.Select(l => (JsonNode?)JsonValue.Create(l)).ToArray());
                }
            }
            else if (typed.Length > 0)
            {
                answers[question] = typed;
            }
            else if (labels.Count > 0 && labels[^1].Length > 0)
            {
                answers[question] = labels[^1];
            }
        }
        return answers;
    }

    private static string FormatThinkingSeconds(double seconds)
    {
        var whole = (long)Math.Max(1, RoundHalfUp(seconds));
        if (whole < 60)
        {
            return whole.ToString(CultureInfo.InvariantCulture) + "s";
        }
        var minutes = whole / 60;
        var rest = whole % 60;
        return rest != 0
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes}m {rest}s")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m");
    }

    private static bool StartsWithAny(string name, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    [GeneratedRegex("claude|opus|sonnet|haiku|fable|mythos")]
    private static partial Regex ClaudeModel();

    [GeneratedRegex("gemini|gemma")]
    private static partial Regex GeminiModel();

    [GeneratedRegex("gpt|chatgpt|openai|codex|(^|/)o[134](-|$)")]
    private static partial Regex OpenAiModel();

    [GeneratedRegex("grok|xai")]
    private static partial Regex GrokModel();
}
