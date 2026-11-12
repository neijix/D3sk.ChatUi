using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using D3sk.ChatUi.Session;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Tools;

/// <summary>One line of an edit's or a write's diff: <paramref name="Kind"/> is add, del or gap.</summary>
public sealed record DiffLine(string Kind, string Text);

/// <summary>
/// How a tool call reads in the transcript (hub transcript.js; @d3sk/chat-ui's tools.js): its label,
/// kind, one-line summary, IN text or diff, and whether its output is a refusal.
/// </summary>
public static partial class ToolText
{
    private static readonly JsonObject Tools = ChatSpec.Labels["tools"]!.AsObject();
    private static readonly string[] PathKeys = [.. ChatSpec.Strings(Tools["pathKeys"])];
    private static readonly string[] PatternKeys = [.. ChatSpec.Strings(Tools["patternKeys"])];
    private static readonly string[] SummaryKeys = [.. ChatSpec.Strings(Tools["summaryKeys"])];

    /// <summary>The longest summary, in UTF-16 units, ellipsis included.</summary>
    public static int MaxLength { get; } = Tools["summaryMax"]!.GetValue<int>();

    /// <summary>"Bash" from "run_terminal_command"; an unknown name is kept, a missing one is "tool".</summary>
    public static string Label(string? name)
    {
        if (!string.IsNullOrEmpty(name) && Tools["labels"]![name] is JsonValue label)
        {
            return label.GetValue<string>();
        }
        return string.IsNullOrEmpty(name) ? Tools["fallbackName"]!.GetValue<string>() : name;
    }

    /// <summary>shell, read, edit, write or other, from a tool label.</summary>
    public static string Kind(string label) =>
        Tools["kinds"]![label] is JsonValue kind ? kind.GetValue<string>() : "other";

    /// <summary>The summary beside a tool's name: a path, a pattern, a command's first line.</summary>
    public static string Summarize(string? kind, JsonNode? args, string? projectRoot = null)
    {
        var summary = "";
        if (args is JsonValue value && value.TryGetValue<string>(out var text))
        {
            summary = text;
        }
        else if (args is JsonObject obj)
        {
            summary = FromObject(kind, obj, projectRoot);
        }
        var newline = summary.IndexOf('\n');
        if (newline >= 0)
        {
            summary = summary[..newline];
        }
        return summary.Length > MaxLength ? summary[..(MaxLength - 1)] + "…" : summary;
    }

    /// <summary>The IN row: a command as typed, else the arguments as JSON.stringify(args, null, 2).</summary>
    public static string Input(JsonNode? args)
    {
        if (args is null)
        {
            return "";
        }
        if (args is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }
        if (args is JsonObject obj)
        {
            var command = Truthy(obj["command"]) ? obj["command"] : obj["CommandLine"];
            if (command is JsonValue c && c.TryGetValue<string>(out var line))
            {
                return line;
            }
        }
        return JsJson.Stringify(args, 2);
    }

    /// <summary>An edit's or a write's lines, every one (a view clips them); null when there is nothing to show.</summary>
    public static IReadOnlyList<DiffLine>? Diff(string? kind, JsonNode? args)
    {
        if (kind is not ("edit" or "write") || args is not JsonObject obj)
        {
            return null;
        }
        var lines = new List<DiffLine>();
        void Add(string lineKind, string sign, string text)
        {
            if (text.Length == 0)
            {
                return;
            }
            var body = text.EndsWith('\n') ? text[..^1] : text;
            foreach (var line in body.Split('\n'))
            {
                lines.Add(new DiffLine(lineKind, sign + " " + line));
            }
        }
        if (kind == "write")
        {
            Add("add", "+", FirstString(obj, ["content", "contents", "file_text", "text"]));
        }
        else
        {
            var edits = obj["edits"] is JsonArray array ? array.ToList() : [obj];
            for (var i = 0; i < edits.Count; i++)
            {
                if (edits[i] is not JsonObject edit)
                {
                    continue;
                }
                if (i > 0)
                {
                    lines.Add(new DiffLine("gap", "⋯"));
                }
                Add("del", "-", FirstString(edit, ["old_string", "old_str", "oldText", "oldString"]));
                Add("add", "+", FirstString(edit, ["new_string", "new_str", "newText", "newString"]));
            }
        }
        return lines.Count > 0 ? lines : null;
    }

    /// <summary>The first lines of a long text, as a closed tool box shows them.</summary>
    public static string ClipPreview(string? text)
    {
        var full = text ?? "";
        var lines = (int)ChatSpec.Limit("clipLines");
        var chars = (int)ChatSpec.Limit("clipChars");
        var preview = string.Join('\n', full.Split('\n').Take(lines));
        return preview.Length > chars ? preview[..chars] : preview;
    }

    /// <summary>The CLI refused the call itself (not an OS "permission denied", not a file quoting one).</summary>
    public static bool IsPermissionRefusal(string? output)
    {
        var text = (output ?? "").Trim();
        return text.Length > 0 && text.Length <= 2000 && Refusal().IsMatch(text);
    }

    private static bool Truthy(JsonNode? node) =>
        node is not null && !(node is JsonValue v && ((v.TryGetValue<string>(out var s) && s.Length == 0) || (v.TryGetValue<bool>(out var b) && !b)));

    private static string FromObject(string? kind, JsonObject args, string? projectRoot)
    {
        var path = ProjectRelative(FirstString(args, PathKeys), projectRoot);
        var summary = "";
        if (kind is "edit" or "write")
        {
            summary = path;
        }
        else if (kind == "read")
        {
            var pattern = FirstString(args, PatternKeys);
            summary = pattern.Length > 0 && path.Length > 0 ? $"{pattern} · {path}" : pattern.Length > 0 ? pattern : path;
            if (IntegerOf(args["offset"]) is { } offset && IntegerOf(args["limit"]) is { } limit)
            {
                summary += $" · lines {offset}–{offset + limit - 1}";
            }
        }
        if (summary.Length > 0)
        {
            return summary;
        }
        var keyed = FirstString(args, SummaryKeys);
        return keyed.Length > 0 ? keyed : args.Select(p => StringOf(p.Value)).FirstOrDefault(s => s is not null) ?? "";
    }

    private static long? IntegerOf(JsonNode? node)
    {
        if (node is not JsonValue v)
        {
            return null;
        }
        if (v.TryGetValue<long>(out var whole))
        {
            return whole;
        }
        return v.TryGetValue<double>(out var d) && d == Math.Floor(d) && !double.IsInfinity(d) ? (long)d : null;
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string FirstString(JsonObject args, string[] keys)
    {
        foreach (var key in keys)
        {
            if (StringOf(args[key]) is { Length: > 0 } s)
            {
                return s;
            }
        }
        return "";
    }

    private static string ProjectRelative(string path, string? root) =>
        !string.IsNullOrEmpty(root) && path.StartsWith(root + "/", StringComparison.Ordinal) ? path[(root.Length + 1)..] : path;

    [GeneratedRegex(@"^(?:Tool `[^`]+` was not executed: )?(?:Denied by permission policy\b|User cancelled the execution for tool\b|The user rejected permission to use this specific tool call\.?|This command requires approval\b|A variable in this command requires approval\b|The following part requires approval\b|Auto mode blocked\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Refusal();
}
