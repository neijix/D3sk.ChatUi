using System.Text.Json.Nodes;
using D3sk.ChatUi.Tools;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>The one-line summary beside a tool part's name: <see cref="ToolText.Summarize"/>, kept here for 0.1 callers.</summary>
public static class ToolSummary
{
    /// <summary>The longest summary, in UTF-16 units, ellipsis included.</summary>
    public const int MaxLength = 140;

    /// <summary>Summarizes a tool call of <paramref name="kind"/> (edit, write, read, bash, ...) with its arguments.</summary>
    public static string Summarize(string? kind, JsonNode? args, string? projectRoot = null) =>
        ToolText.Summarize(kind, args, projectRoot);
}
