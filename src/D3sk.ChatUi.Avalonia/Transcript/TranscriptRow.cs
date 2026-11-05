using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using D3sk.ChatUi.Avalonia.Markdown;
using D3sk.ChatUi.Labels;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>The control for one <see cref="TranscriptItem"/>. A virtualized list reuses rows, so it follows whichever item it holds now.</summary>
public sealed class TranscriptRow : UserControl
{
    private static readonly Dictionary<string, string> ToolLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bash"] = "Bash", ["shell"] = "Bash", ["run_terminal_command"] = "Bash", ["run_shell_command"] = "Bash", ["run_command"] = "Bash",
        ["read"] = "Read", ["read_file"] = "Read", ["view_file"] = "Read", ["view"] = "Read", ["grep"] = "Grep", ["glob"] = "Glob",
        ["glob_file_search"] = "Glob", ["list"] = "List", ["list_dir"] = "List", ["webfetch"] = "WebFetch", ["websearch"] = "WebSearch",
        ["search_replace"] = "Edit", ["edit_file"] = "Edit", ["str_replace"] = "Edit", ["replace"] = "Edit", ["edit"] = "Edit",
        ["write"] = "Write", ["write_file"] = "Write", ["create_file"] = "Write", ["create"] = "Write",
        ["todo_write"] = "TodoWrite", ["todowrite"] = "TodoWrite",
    };

    private readonly StackPanel _panel = new() { Spacing = 4, Margin = new global::Avalonia.Thickness(12, 6) };
    private readonly SelectableTextBlock _plain = new() { TextWrapping = TextWrapping.Wrap };
    private readonly MarkdownView _markdown = new();
    private readonly StackPanel _tools = new() { Spacing = 2 };
    private readonly TextBlock _working = new() { FontStyle = FontStyle.Italic, Opacity = 0.7 };
    private TranscriptItem? _item;

    /// <summary>Creates a row with no item.</summary>
    public TranscriptRow()
    {
        _panel.Children.Add(_plain);
        _panel.Children.Add(_markdown);
        _panel.Children.Add(_tools);
        _panel.Children.Add(_working);
        Content = _panel;
        DataContextChanged += (_, _) => Follow(DataContext as TranscriptItem);
    }

    /// <summary>The project folder that tool paths are shown relative to.</summary>
    public string? ProjectRoot { get; set; }

    private void Follow(TranscriptItem? item)
    {
        if (_item is not null)
        {
            _item.Changed -= OnChanged;
        }
        _item = item;
        if (item is not null)
        {
            item.Changed += OnChanged;
        }
        Refresh();
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var item = _item;
        var user = item?.Role == TranscriptRole.User;
        _plain.IsVisible = user;
        _markdown.IsVisible = !user;
        if (user)
        {
            // The person's text and the agent's tool text are text, never markup.
            _plain.Text = item!.Text;
        }
        else
        {
            _markdown.SetMarkdown(item?.Text);
        }
        _working.IsVisible = item is { IsWorking: true };
        _working.Text = item is { IsWorking: true } ? ChatLabels.WorkingPhrase(item.Activity, item.ElapsedSeconds) : "";
        RefreshTools(item);
    }

    private void RefreshTools(TranscriptItem? item)
    {
        _tools.Children.Clear();
        foreach (var tool in item?.Tools ?? [])
        {
            var line = new SelectableTextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
            var label = ToolLabels.GetValueOrDefault(tool.Name, tool.Name);
            line.Inlines!.Add(new Run(label) { FontWeight = FontWeight.SemiBold });
            line.Inlines.Add(new Run("  " + ToolSummary.Summarize(tool.Kind, tool.Args, ProjectRoot)) { Foreground = Brushes.Gray });
            _tools.Children.Add(line);
        }
    }
}
