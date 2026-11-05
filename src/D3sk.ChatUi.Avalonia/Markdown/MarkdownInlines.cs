using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using D3sk.ChatUi.Markdown;
using MdInline = D3sk.ChatUi.Markdown.Inline;

namespace D3sk.ChatUi.Avalonia.Markdown;

/// <summary>Turns model inlines into Avalonia inlines. Every string goes into a <see cref="Run"/> as text; nothing is parsed as markup.</summary>
internal static class MarkdownInlines
{
    internal static readonly FontFamily Mono = new("Cascadia Mono, Menlo, Consolas, DejaVu Sans Mono, monospace");

    /// <summary>Appends <paramref name="content"/> to <paramref name="target"/>; <paramref name="onLink"/> gets a whitelisted link the user activated.</summary>
    public static void Append(InlineCollection target, IEnumerable<MdInline> content, Action<Link> onLink)
    {
        foreach (var node in content)
        {
            AppendOne(target, node, onLink);
        }
    }

    /// <summary>True when the link may be followed: the model's kind agrees with the address scheme.</summary>
    public static bool IsFollowable(Link link) => link.Kind switch
    {
        LinkKind.External => Uri.TryCreate(link.Href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        LinkKind.NewChat => link.Href.StartsWith("#/new?", StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>The plain text of <paramref name="content"/>, used as a link label.</summary>
    public static string PlainText(IEnumerable<MdInline> content)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var node in content)
        {
            builder.Append(node switch
            {
                TextRun t => t.Text,
                CodeSpan c => c.Text,
                Strong s => PlainText(s.Content),
                Emphasis e => PlainText(e.Content),
                Link l => PlainText(l.Content),
                D3sk.ChatUi.Markdown.LineBreak => "\n",
                _ => string.Empty,
            });
        }
        return builder.ToString();
    }

    private static void AppendOne(InlineCollection target, MdInline node, Action<Link> onLink)
    {
        switch (node)
        {
            case TextRun text:
                target.Add(new Run(text.Text));
                break;
            case CodeSpan code:
                target.Add(new Run(code.Text) { FontFamily = Mono });
                break;
            case Strong strong:
                target.Add(Wrap(new Span { FontWeight = FontWeight.Bold }, strong.Content, onLink));
                break;
            case Emphasis emphasis:
                target.Add(Wrap(new Span { FontStyle = FontStyle.Italic }, emphasis.Content, onLink));
                break;
            case D3sk.ChatUi.Markdown.LineBreak:
                target.Add(new global::Avalonia.Controls.Documents.LineBreak());
                break;
            case Link link:
                AppendLink(target, link, onLink);
                break;
        }
    }

    private static Span Wrap(Span span, IEnumerable<MdInline> content, Action<Link> onLink)
    {
        Append(span.Inlines, content, onLink);
        return span;
    }

    private static void AppendLink(InlineCollection target, Link link, Action<Link> onLink)
    {
        if (!IsFollowable(link))
        {
            // Not whitelisted: the label stays, nothing is clickable.
            Append(target, link.Content, onLink);
            return;
        }
        var label = new TextBlock
        {
            Text = PlainText(link.Content),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = link,
            Background = global::Avalonia.Media.Brushes.Transparent,
        };
        // On press, not release: the enclosing selectable block captures the pointer for selection.
        label.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(label).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                onLink(link);
            }
        };
        target.Add(new InlineUIContainer(label));
    }
}
