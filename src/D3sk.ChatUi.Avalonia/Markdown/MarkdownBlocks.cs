using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D3sk.ChatUi.Markdown;
using MdInline = D3sk.ChatUi.Markdown.Inline;

namespace D3sk.ChatUi.Avalonia.Markdown;

/// <summary>Builds the control for one model block. All text is selectable.</summary>
internal static class MarkdownBlocks
{
    public static Control Build(MarkdownBlock block, Action<Link> onLink) => block switch
    {
        Paragraph p => Text(p.Content, onLink),
        Heading h => Heading(h, onLink),
        ListBlock l => List(l, onLink),
        CodeBlock c => Code(c),
        BlockQuote q => Quote(q, onLink),
        Table t => TableView(t, onLink),
        ThematicBreak => Rule(),
        _ => new Panel(),
    };

    private static SelectableTextBlock Text(IEnumerable<MdInline> content, Action<Link> onLink)
    {
        var box = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        MarkdownInlines.Append(box.Inlines!, content, onLink);
        return box;
    }

    private static Control Heading(Heading heading, Action<Link> onLink)
    {
        var box = Text(heading.Content, onLink);
        box.FontWeight = FontWeight.Bold;
        box.FontSize = heading.Level switch { 1 => 20, 2 => 17, _ => 15 };
        box.Tag = heading;
        return box;
    }

    private static Control List(ListBlock list, Action<Link> onLink)
    {
        var panel = new StackPanel { Spacing = 2 };
        var number = 1;
        foreach (var item in list.Items)
        {
            var marker = new TextBlock { Text = list.Ordered ? $"{number++}." : "•", Margin = new Thickness(0, 0, 8, 0) };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(marker, 0);
            var body = Text(item, onLink);
            Grid.SetColumn(body, 1);
            row.Children.Add(marker);
            row.Children.Add(body);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static Control Code(CodeBlock code)
    {
        var text = new SelectableTextBlock
        {
            Text = code.Code,
            FontFamily = MarkdownInlines.Mono,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(10),
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer { HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = text },
            Tag = code,
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("Chat.CodeBg"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("Chat.Border"));
        return border;
    }

    private static Control Quote(BlockQuote quote, Action<Link> onLink)
    {
        var border = new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 2, 0, 2), Child = Text(quote.Content, onLink), Tag = quote };
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("Chat.Rail"));
        return border;
    }

    private static Control Rule()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 6) };
        line.Bind(Border.BackgroundProperty, line.GetResourceObservable("Chat.Border"));
        return line;
    }

    private static Control TableView(Table table, Action<Link> onLink)
    {
        var columns = Math.Max(table.Head.Count, table.Rows.Select(r => r.Count).DefaultIfEmpty(0).Max());
        var grid = new Grid { Tag = table, ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(columns, 1)))) };
        AddRow(grid, 0, table.Head, true, onLink);
        for (var r = 0; r < table.Rows.Count; r++)
        {
            AddRow(grid, r + 1, table.Rows[r], false, onLink);
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = grid };
    }

    private static void AddRow(Grid grid, int row, IReadOnlyList<IReadOnlyList<MdInline>> cells, bool header, Action<Link> onLink)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < cells.Count; c++)
        {
            var text = Text(cells[c], onLink);
            if (header)
            {
                text.FontWeight = FontWeight.Bold;
            }
            var cell = new Border { BorderThickness = new Thickness(0.5), Padding = new Thickness(8, 4), Child = text };
            cell.Bind(Border.BorderBrushProperty, cell.GetResourceObservable("Chat.Border"));
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }
    }
}
