using System.Text;
using System.Text.RegularExpressions;

namespace D3sk.ChatUi.Markdown;

/// <summary>
/// Minimal, safe Markdown to model, a port of <c>markdown.js</c>. Agent output is untrusted, so
/// only a small whitelist of constructs becomes nodes; everything else is text.
/// </summary>
public static partial class MarkdownParser
{
    // Private-use marks standing in for already-parsed spans while the emphasis passes run.
    private const char LinkMark = '';
    private const char StrongOpen = '';
    private const char StrongClose = '';
    private const char EmOpen = '';
    private const char EmClose = '';

    [GeneratedRegex(@"^\s*```\s*([A-Za-z0-9_+#.\-]*)\s*$")]
    private static partial Regex FenceOpen();

    [GeneratedRegex(@"^\s*```\s*$")]
    private static partial Regex FenceClose();

    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")]
    private static partial Regex TableDivider();

    [GeneratedRegex(@"^\s*(?:-{3,}|\*{3,}|_{3,})\s*$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\s*>\s?(.*)$")]
    private static partial Regex Quote();

    [GeneratedRegex(@"^\s*>")]
    private static partial Regex QuoteStart();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^\s*[-*]\s+(.*)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^\s*[0-9]+[.)]\s+(.*)$")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"(`[^`\n]+`)")]
    private static partial Regex InlineCode();

    // The url may hold one level of balanced parentheses (e.g. Wikipedia links).
    [GeneratedRegex(@"\[([^\]\n]+)\]\(((?:[^()\s]|\([^()\s]*\))+)\)")]
    private static partial Regex LinkSyntax();

    [GeneratedRegex(@"\*\*([^*\n]+)\*\*")]
    private static partial Regex StrongSyntax();

    [GeneratedRegex(@"(^|[^*])\*([^*\s][^*\n]*)\*(?!\*)")]
    private static partial Regex EmSyntax();

    /// <summary>Parses <paramref name="source"/>; null is empty. An unclosed fence runs to the end,
    /// the normal state while a reply is streaming.</summary>
    public static MarkdownDocument Parse(string? source)
    {
        var lines = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return new BlockReader(lines).Read();
    }

    // Walks the lines once; each block kind has its own method, and a method returns true when it
    // took the current line (and any it consumed after it).
    private sealed class BlockReader(string[] lines)
    {
        private readonly List<MarkdownBlock> _blocks = [];
        private List<string> _paragraph = [];
        private List<string>? _items;
        private bool _ordered;
        private int _i;

        public MarkdownDocument Read()
        {
            for (_i = 0; _i < lines.Length; _i++)
            {
                var line = lines[_i];
                if (!(TryFence(line) || TryTable(line) || TryRule(line) || TryQuote(line)
                    || TryHeading(line) || TryListItem(line) || TryBlank(line)))
                {
                    FlushList();
                    _paragraph.Add(line);
                }
            }
            FlushParagraph();
            FlushList();
            return new MarkdownDocument(_blocks);
        }

        private void FlushParagraph()
        {
            if (_paragraph.Count > 0)
            {
                _blocks.Add(new Paragraph(JoinLines(_paragraph)));
                _paragraph = [];
            }
        }

        private void FlushList()
        {
            if (_items is not null)
            {
                _blocks.Add(new ListBlock(_ordered, _items.Select(ParseInline).ToList()));
                _items = null;
            }
        }

        private void FlushAll()
        {
            FlushParagraph();
            FlushList();
        }

        private bool TryFence(string line)
        {
            var fence = FenceOpen().Match(line);
            if (!fence.Success)
            {
                return false;
            }
            FlushAll();
            var code = new List<string>();
            _i++;
            while (_i < lines.Length && !FenceClose().IsMatch(lines[_i]))
            {
                code.Add(lines[_i]);
                _i++;
            }
            _blocks.Add(new CodeBlock(fence.Groups[1].Value, string.Join('\n', code)));
            return true;
        }

        // A table needs a header row followed by a |---| divider; every cell is inline text.
        private bool TryTable(string line)
        {
            if (!(line.Contains('|') && _i + 1 < lines.Length && lines[_i + 1].Contains('|') && TableDivider().IsMatch(lines[_i + 1])))
            {
                return false;
            }
            FlushAll();
            var head = TableCells(line);
            var rows = new List<IReadOnlyList<IReadOnlyList<Inline>>>();
            _i += 2;
            while (_i < lines.Length && lines[_i].Contains('|') && lines[_i].Trim().Length > 0)
            {
                var row = TableCells(lines[_i]);
                rows.Add(head.Select((_, k) => ParseInline(k < row.Count ? row[k] : "")).ToList());
                _i++;
            }
            _i--;
            _blocks.Add(new Table(head.Select(ParseInline).ToList(), rows));
            return true;
        }

        private bool TryRule(string line)
        {
            if (!Rule().IsMatch(line))
            {
                return false;
            }
            FlushAll();
            _blocks.Add(new ThematicBreak());
            return true;
        }

        private bool TryQuote(string line)
        {
            var quote = Quote().Match(line);
            if (!quote.Success)
            {
                return false;
            }
            FlushAll();
            var quoted = new List<string> { quote.Groups[1].Value };
            while (_i + 1 < lines.Length && QuoteStart().IsMatch(lines[_i + 1]))
            {
                _i++;
                quoted.Add(Quote().Match(lines[_i]).Groups[1].Value);
            }
            _blocks.Add(new BlockQuote(JoinLines(quoted)));
            return true;
        }

        private bool TryHeading(string line)
        {
            var heading = HeadingLine().Match(line);
            if (!heading.Success)
            {
                return false;
            }
            FlushAll();
            _blocks.Add(new Heading(Math.Min(heading.Groups[1].Length, 3), ParseInline(heading.Groups[2].Value)));
            return true;
        }

        private bool TryListItem(string line)
        {
            var bullet = Bullet().Match(line);
            var numbered = bullet.Success ? Match.Empty : Numbered().Match(line);
            if (!bullet.Success && !numbered.Success)
            {
                return false;
            }
            FlushParagraph();
            var isOrdered = !bullet.Success;
            if (_items is not null && _ordered != isOrdered)
            {
                FlushList();
            }
            if (_items is null)
            {
                _items = [];
                _ordered = isOrdered;
            }
            _items.Add((bullet.Success ? bullet : numbered).Groups[1].Value);
            return true;
        }

        private bool TryBlank(string line)
        {
            if (line.Trim().Length > 0)
            {
                return false;
            }
            FlushAll();
            return true;
        }
    }

    private static List<Inline> JoinLines(List<string> lines)
    {
        var result = new List<Inline>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                result.Add(new LineBreak());
            }
            result.AddRange(ParseInline(lines[i]));
        }
        return result;
    }

    private static List<string> TableCells(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|'))
        {
            t = t[1..];
        }
        if (t.EndsWith('|'))
        {
            t = t[..^1];
        }
        return t.Split('|').Select(c => c.Trim()).ToList();
    }

    /// <summary>Parses one line of inline text.</summary>
    internal static List<Inline> ParseInline(string text)
    {
        var result = new List<Inline>();
        // Inline code is split out first so its content never gets bold, italic or link formatting.
        var parts = InlineCode().Split(text);
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index % 2 == 1)
            {
                result.Add(new CodeSpan(part[1..^1]));
            }
            else if (part.Length > 0)
            {
                result.AddRange(ParseFormatted(part));
            }
        }
        return result;
    }

    private static List<Inline> ParseFormatted(string part)
    {
        // The marks are reserved: a literal one in the input would be misread, so it is replaced.
        var text = part.Replace(LinkMark, '�').Replace(StrongOpen, '�').Replace(StrongClose, '�')
            .Replace(EmOpen, '�').Replace(EmClose, '�');
        var links = new List<(LinkKind Kind, string Href, string Label)>();
        text = LinkSyntax().Replace(text, m =>
        {
            var label = m.Groups[1].Value;
            var url = m.Groups[2].Value;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                links.Add((LinkKind.External, url, label));
            }
            else if (url.StartsWith("#/new?", StringComparison.Ordinal))
            {
                links.Add((LinkKind.NewChat, url, label));
            }
            else
            {
                return label; // any other target: the label stays as text
            }
            return LinkMark.ToString();
        });
        text = StrongSyntax().Replace(text, m => StrongOpen + m.Groups[1].Value + StrongClose);
        text = EmSyntax().Replace(text, m => m.Groups[1].Value + EmOpen + m.Groups[2].Value + EmClose);

        var next = 0;
        var position = 0;
        return Build(text, ref position, null, links, ref next);
    }

    private static List<Inline> Build(string text, ref int position, char? close, List<(LinkKind Kind, string Href, string Label)> links, ref int next)
    {
        var nodes = new List<Inline>();
        var run = new StringBuilder();

        void FlushRun()
        {
            if (run.Length > 0)
            {
                nodes.Add(new TextRun(run.ToString()));
                run.Clear();
            }
        }

        while (position < text.Length)
        {
            var c = text[position++];
            if (close is not null && c == close)
            {
                break;
            }
            switch (c)
            {
                case LinkMark:
                    FlushRun();
                    var link = links[next++];
                    nodes.Add(new Link(link.Kind, link.Href, ParseLabel(link.Label)));
                    break;
                case StrongOpen:
                    FlushRun();
                    nodes.Add(new Strong(Build(text, ref position, StrongClose, links, ref next)));
                    break;
                case EmOpen:
                    FlushRun();
                    nodes.Add(new Emphasis(Build(text, ref position, EmClose, links, ref next)));
                    break;
                default:
                    run.Append(c);
                    break;
            }
        }
        FlushRun();
        return nodes;
    }

    // A label gets bold and italic but no nested link: its brackets are already consumed.
    private static List<Inline> ParseLabel(string label)
    {
        var text = label.Replace(LinkMark, '�').Replace(StrongOpen, '�').Replace(StrongClose, '�')
            .Replace(EmOpen, '�').Replace(EmClose, '�');
        text = StrongSyntax().Replace(text, m => StrongOpen + m.Groups[1].Value + StrongClose);
        text = EmSyntax().Replace(text, m => m.Groups[1].Value + EmOpen + m.Groups[2].Value + EmClose);
        var next = 0;
        var position = 0;
        return Build(text, ref position, null, [], ref next);
    }
}
