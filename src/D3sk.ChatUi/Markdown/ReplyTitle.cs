using System.Text.RegularExpressions;

namespace D3sk.ChatUi.Markdown;

/// <summary>
/// Agents end a reply with a hidden <c>&lt;!-- d3skhand-title: … --&gt;</c> line; the transcript hides it,
/// whole or still arriving (hub reply-title.js, @d3sk/chat-ui's reply-title.js).
/// </summary>
public static partial class ReplyTitle
{
    private static readonly string[] TitleStarts = ["<!-- d3skhand-title:", "<!-- agentdesk-title:"];

    /// <summary>The text without its title line; while streaming, a title still arriving is cut too.</summary>
    public static string Hide(string text, bool isStreaming = false)
    {
        var shown = TitleLine().Replace(text, "");
        if (isStreaming)
        {
            var start = shown.LastIndexOf('<');
            var tail = start < 0 ? "" : shown[start..];
            if (tail.Length > 0 && !tail.Contains('\n') && IsTitleStart(tail))
            {
                shown = shown[..start];
            }
        }
        return shown == text ? text : TrimEndJs(shown);
    }

    private static bool IsTitleStart(string tail)
    {
        var normalized = CommentOpen().Replace(tail, "<!-- ", 1).ToLowerInvariant();
        return TitleStarts.Any(start => start.StartsWith(normalized, StringComparison.Ordinal) || normalized.StartsWith(start, StringComparison.Ordinal));
    }

    // JavaScript's trimEnd: white space and line terminators.
    private static string TrimEndJs(string text) => text.TrimEnd();

    [GeneratedRegex(@"[ \t]*<!--[ \t]*(?:d3skhand|agentdesk)-title[ \t]*:[^\n]*?-->[ \t]*(?:\r?\n)?", RegexOptions.IgnoreCase)]
    private static partial Regex TitleLine();

    [GeneratedRegex(@"^<!--[ \t]*")]
    private static partial Regex CommentOpen();
}
