using Avalonia.Controls;
using D3sk.ChatUi.Markdown;

namespace D3sk.ChatUi.Avalonia.Markdown;

/// <summary>Shows a markdown message with selectable text, code blocks and tables. A streaming reply
/// that grows re-renders only the blocks after the unchanged ones; the rest keep their controls.</summary>
public sealed class MarkdownView : UserControl
{
    private readonly StackPanel _panel = new() { Spacing = 8 };
    private readonly List<(string Signature, Control Control)> _rendered = [];

    /// <summary>Creates an empty view.</summary>
    public MarkdownView() => Content = _panel;

    /// <summary>Raised when the user activates a whitelisted link (http/https or the new-chat deep link); the host opens it.</summary>
    public event EventHandler<Link>? LinkActivated;

    /// <summary>How many blocks have been built since the view was created.</summary>
    public int RenderedBlockCount { get; private set; }

    /// <summary>How many blocks are shown.</summary>
    public int BlockCount => _rendered.Count;

    /// <summary>Parses <paramref name="source"/> and shows it.</summary>
    public void SetMarkdown(string? source) => SetDocument(MarkdownParser.Parse(source));

    /// <summary>Shows <paramref name="document"/>, rebuilding only from the first block that differs.</summary>
    public void SetDocument(MarkdownDocument document)
    {
        var blocks = document.Blocks;
        var signatures = blocks.Select(Signature).ToList();
        var keep = 0;
        while (keep < _rendered.Count && keep < signatures.Count && _rendered[keep].Signature == signatures[keep])
        {
            keep++;
        }
        _rendered.RemoveRange(keep, _rendered.Count - keep);
        while (_panel.Children.Count > keep)
        {
            _panel.Children.RemoveAt(_panel.Children.Count - 1);
        }
        for (var i = keep; i < blocks.Count; i++)
        {
            var control = MarkdownBlocks.Build(blocks[i], OnLink);
            RenderedBlockCount++;
            _rendered.Add((signatures[i], control));
            _panel.Children.Add(control);
        }
    }

    private void OnLink(Link link)
    {
        if (MarkdownInlines.IsFollowable(link))
        {
            LinkActivated?.Invoke(this, link);
        }
    }

    // The model's records compare lists by reference, so a block is compared by its written form.
    private static string Signature(MarkdownBlock block) =>
        block.GetType().Name + ":" + HtmlWriter.Write(new MarkdownDocument([block]));
}
