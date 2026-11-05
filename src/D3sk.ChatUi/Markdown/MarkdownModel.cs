namespace D3sk.ChatUi.Markdown;

/// <summary>A parsed message. The model holds only the constructs the whitelist keeps; raw HTML,
/// images and any other syntax never become nodes, so the writer cannot emit them.</summary>
public sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks);

/// <summary>A block-level node.</summary>
public abstract record MarkdownBlock;

/// <summary>A paragraph; lines are joined by <see cref="LineBreak"/> nodes.</summary>
public sealed record Paragraph(IReadOnlyList<Inline> Content) : MarkdownBlock;

/// <summary>A heading, level 1 to 3 (deeper levels are capped).</summary>
public sealed record Heading(int Level, IReadOnlyList<Inline> Content) : MarkdownBlock;

/// <summary>A bulleted or numbered list.</summary>
public sealed record ListBlock(bool Ordered, IReadOnlyList<IReadOnlyList<Inline>> Items) : MarkdownBlock;

/// <summary>A fenced code block; <see cref="Language"/> is empty when none was given.</summary>
public sealed record CodeBlock(string Language, string Code) : MarkdownBlock;

/// <summary>A quote; lines are joined by <see cref="LineBreak"/> nodes.</summary>
public sealed record BlockQuote(IReadOnlyList<Inline> Content) : MarkdownBlock;

/// <summary>A horizontal rule.</summary>
public sealed record ThematicBreak : MarkdownBlock;

/// <summary>A table: a header row and body rows, every cell inline text.</summary>
public sealed record Table(
    IReadOnlyList<IReadOnlyList<Inline>> Head,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<Inline>>> Rows) : MarkdownBlock;

/// <summary>An inline node.</summary>
public abstract record Inline;

/// <summary>Plain text; the writer escapes it.</summary>
public sealed record TextRun(string Text) : Inline;

/// <summary>Inline code; its content is never formatted.</summary>
public sealed record CodeSpan(string Text) : Inline;

/// <summary>Bold text.</summary>
public sealed record Strong(IReadOnlyList<Inline> Content) : Inline;

/// <summary>Italic text.</summary>
public sealed record Emphasis(IReadOnlyList<Inline> Content) : Inline;

/// <summary>A line break inside a paragraph or quote.</summary>
public sealed record LineBreak : Inline;

/// <summary>Where a link may go. Any other target is dropped at parse time and only its label stays.</summary>
public enum LinkKind
{
    /// <summary>An http(s) address, opened in a new tab.</summary>
    External,

    /// <summary>The new-chat deep link <c>#/new?…</c>, a same-tab anchor.</summary>
    NewChat,
}

/// <summary>A link whose <see cref="Href"/> is raw (unescaped) and already checked against <see cref="Kind"/>.</summary>
public sealed record Link(LinkKind Kind, string Href, IReadOnlyList<Inline> Content) : Inline;
