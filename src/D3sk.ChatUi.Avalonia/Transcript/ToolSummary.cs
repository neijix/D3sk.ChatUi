using System.Text.Json.Nodes;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>The one-line summary beside a tool part's name; the same rules as the web UI's toolSummary (row 6's corpus).</summary>
public static class ToolSummary
{
    /// <summary>The longest summary, in UTF-16 units, ellipsis included.</summary>
    public const int MaxLength = 140;

    private static readonly string[] PathKeys =
        ["file_path", "filePath", "target_file", "AbsolutePath", "notebook_path", "path", "target_directory"];

    private static readonly string[] PatternKeys = ["pattern", "Pattern", "query", "url"];

    private static readonly string[] SummaryKeys =
        ["description", "command", "CommandLine", "pattern", "Pattern", "query", "url", .. PathKeys];

    /// <summary>Summarizes a tool call of <paramref name="kind"/> (edit, write, read, bash, ...) with its arguments.</summary>
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
            summary = ReadSummary(args, path);
        }
        if (summary.Length > 0)
        {
            return summary;
        }
        var keyed = FirstString(args, SummaryKeys);
        return keyed.Length > 0 ? keyed : args.Select(p => StringOf(p.Value)).FirstOrDefault(s => s is not null) ?? "";
    }

    private static string ReadSummary(JsonObject args, string path)
    {
        var pattern = FirstString(args, PatternKeys);
        var summary = pattern.Length > 0 && path.Length > 0 ? $"{pattern} · {path}" : pattern.Length > 0 ? pattern : path;
        if (IntegerOf(args["offset"]) is { } offset && IntegerOf(args["limit"]) is { } limit)
        {
            summary += $" · lines {offset}–{offset + limit - 1}";
        }
        return summary;
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
}
