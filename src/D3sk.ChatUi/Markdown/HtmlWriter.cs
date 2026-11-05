using System.Text;

namespace D3sk.ChatUi.Markdown;

/// <summary>
/// Writes a <see cref="MarkdownDocument"/> as HTML. Every string from the model passes through
/// <see cref="Escape"/>; the only markup is the tags written here.
/// </summary>
public static class HtmlWriter
{
    /// <summary>Escapes &amp;, &lt;, &gt;, double and single quotes.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    /// <summary>Renders the whole document.</summary>
    public static string Write(MarkdownDocument document)
    {
        var sb = new StringBuilder();
        foreach (var block in document.Blocks)
        {
            WriteBlock(sb, block);
        }
        return sb.ToString();
    }

    /// <summary>Markdown source straight to safe HTML.</summary>
    public static string Render(string? source) => Write(MarkdownParser.Parse(source));

    private static void WriteBlock(StringBuilder sb, MarkdownBlock block)
    {
        switch (block)
        {
            case Paragraph p:
                sb.Append("<p>");
                WriteInlines(sb, p.Content);
                sb.Append("</p>");
                break;
            case Heading h:
                sb.Append("<h").Append(h.Level).Append('>');
                WriteInlines(sb, h.Content);
                sb.Append("</h").Append(h.Level).Append('>');
                break;
            case ListBlock l:
                var tag = l.Ordered ? "ol" : "ul";
                sb.Append('<').Append(tag).Append('>');
                foreach (var item in l.Items)
                {
                    sb.Append("<li>");
                    WriteInlines(sb, item);
                    sb.Append("</li>");
                }
                sb.Append("</").Append(tag).Append('>');
                break;
            case CodeBlock c:
                sb.Append("<pre><code");
                if (c.Language.Length > 0)
                {
                    sb.Append(" class=\"language-").Append(Escape(c.Language)).Append('"');
                }
                sb.Append('>').Append(Escape(c.Code)).Append("</code></pre>");
                break;
            case BlockQuote q:
                sb.Append("<blockquote>");
                WriteInlines(sb, q.Content);
                sb.Append("</blockquote>");
                break;
            case ThematicBreak:
                sb.Append("<hr>");
                break;
            case Table t:
                sb.Append("<div class=\"table-wrap\"><table><thead><tr>");
                WriteCells(sb, t.Head, "th");
                sb.Append("</tr></thead><tbody>");
                foreach (var row in t.Rows)
                {
                    sb.Append("<tr>");
                    WriteCells(sb, row, "td");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
                break;
            default:
                throw new InvalidOperationException($"unknown block {block.GetType().Name}");
        }
    }

    private static void WriteCells(StringBuilder sb, IReadOnlyList<IReadOnlyList<Inline>> cells, string tag)
    {
        foreach (var cell in cells)
        {
            sb.Append('<').Append(tag).Append('>');
            WriteInlines(sb, cell);
            sb.Append("</").Append(tag).Append('>');
        }
    }

    private static void WriteInlines(StringBuilder sb, IReadOnlyList<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextRun t:
                    sb.Append(Escape(t.Text));
                    break;
                case CodeSpan c:
                    sb.Append("<code>").Append(Escape(c.Text)).Append("</code>");
                    break;
                case Strong s:
                    sb.Append("<strong>");
                    WriteInlines(sb, s.Content);
                    sb.Append("</strong>");
                    break;
                case Emphasis e:
                    sb.Append("<em>");
                    WriteInlines(sb, e.Content);
                    sb.Append("</em>");
                    break;
                case LineBreak:
                    sb.Append("<br>");
                    break;
                case Link l:
                    sb.Append("<a href=\"").Append(Escape(l.Href)).Append('"');
                    if (l.Kind == LinkKind.External)
                    {
                        sb.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
                    }
                    sb.Append('>');
                    WriteInlines(sb, l.Content);
                    sb.Append("</a>");
                    break;
                default:
                    throw new InvalidOperationException($"unknown inline {inline.GetType().Name}");
            }
        }
    }
}
