using Avalonia.Controls;
using Avalonia.Controls.Documents;
using global::Avalonia.Headless;
using Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using D3sk.ChatUi.Avalonia.Markdown;
using D3sk.ChatUi.Avalonia.Tests.Composer;
using D3sk.ChatUi.Markdown;

namespace D3sk.ChatUi.Avalonia.Tests.Markdown;

public class MarkdownViewTests
{
    private static MarkdownView Show(string markdown)
    {
        var view = new MarkdownView();
        var window = new Window { Content = view };
        window.Show();
        view.SetMarkdown(markdown);
        window.UpdateLayout();
        return view;
    }

    private static string TextOf(SelectableTextBlock block) => string.Concat(block.Inlines!.Select(Flatten));

    private static string Flatten(global::Avalonia.Controls.Documents.Inline inline) => inline switch
    {
        Run run => run.Text ?? string.Empty,
        Span span => string.Concat(span.Inlines.Select(Flatten)),
        _ => string.Empty,
    };

    [AvaloniaFact]
    public void Text_is_selectable()
    {
        var view = Show("hello **world**");
        var text = view.GetVisualDescendantsOf<SelectableTextBlock>().Single();
        Assert.Equal("hello world", TextOf(text));
    }

    [AvaloniaFact]
    public void Code_block_keeps_its_text_verbatim_in_a_selectable_block()
    {
        var view = Show("```py\n<b>x</b>  y\n```");
        var text = view.GetVisualDescendantsOf<SelectableTextBlock>().Single();
        Assert.Equal("<b>x</b>  y", text.Text?.TrimEnd('\n'));
    }

    [AvaloniaFact]
    public void Table_has_a_cell_per_column_and_row()
    {
        var view = Show("| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |");
        var grid = view.GetVisualDescendantsOf<Grid>().Single(g => g.Tag is Table);
        Assert.Equal(6, grid.Children.Count);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(3, grid.RowDefinitions.Count);
    }

    [AvaloniaFact]
    public void Appending_to_a_streaming_reply_rebuilds_only_the_last_block()
    {
        var view = Show("first paragraph\n\nsecond");
        Assert.Equal(2, view.RenderedBlockCount);
        var first = view.GetVisualDescendantsOf<SelectableTextBlock>().First();
        view.SetMarkdown("first paragraph\n\nsecond grows");
        Assert.Equal(3, view.RenderedBlockCount);
        Assert.Same(first, view.GetVisualDescendantsOf<SelectableTextBlock>().First());
        view.SetMarkdown("first paragraph\n\nsecond grows\n\nthird");
        Assert.Equal(4, view.RenderedBlockCount);
        Assert.Equal(3, view.BlockCount);
    }

    [AvaloniaFact]
    public void Markup_in_agent_text_stays_text()
    {
        var view = Show("<script>alert(1)</script> <img src=x onerror=y>");
        var all = string.Concat(view.GetVisualDescendantsOf<SelectableTextBlock>().Select(TextOf));
        Assert.Contains("<script>", all);
        Assert.Empty(view.GetVisualDescendantsOf<Image>());
    }

    [AvaloniaFact]
    public void Only_whitelisted_links_are_followable()
    {
        var view = Show("[ok](https://example.com) [bad](javascript:alert(1))");
        var followed = new List<Link>();
        view.LinkActivated += (_, l) => followed.Add(l);
        var labels = view.GetVisualDescendantsOf<TextBlock>().Where(t => t.Tag is Link).ToList();
        Assert.Single(labels);
        var window = (Window)TopLevel.GetTopLevel(view)!;
        var point = global::Avalonia.VisualExtensions.TranslatePoint(labels[0], new global::Avalonia.Point(3, 3), window)!.Value;
        window.MouseDown(point, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, global::Avalonia.Input.MouseButton.Left);
        Assert.Equal("https://example.com", Assert.Single(followed).Href);
    }

    [AvaloniaFact]
    public void A_link_outside_the_whitelist_built_by_hand_is_plain_text()
    {
        var view = new MarkdownView();
        new Window { Content = view }.Show();
        var link = new Link(LinkKind.External, "javascript:alert(1)", [new TextRun("click")]);
        view.SetDocument(new MarkdownDocument([new Paragraph([link])]));
        Assert.DoesNotContain(view.GetVisualDescendantsOf<TextBlock>(), t => t.Tag is Link);
    }
}
