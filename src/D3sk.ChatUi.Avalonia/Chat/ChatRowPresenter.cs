using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D3sk.ChatUi.Avalonia.Markdown;
using D3sk.ChatUi.Avalonia.Theme;
using D3sk.ChatUi.Markdown;
using D3sk.ChatUi.Session;
using D3sk.ChatUi.Spec;
using D3sk.ChatUi.Tools;

namespace D3sk.ChatUi.Avalonia.Chat;

/// <summary>
/// Draws one transcript row of a <see cref="ChatViewState"/>, as the web chat's transcript.js does:
/// the person's box, a reply's rail of text, thinking and tool parts with their badges, its working
/// line, a notice, an error. Controls carry the web chat's class names (msg-user, part-tool, tool-name,
/// working-text…), so a test can read both front-ends the same way. A part is rebuilt only when it changed.
/// </summary>
public sealed class ChatRowPresenter : UserControl
{
    private readonly List<(string Signature, Control Control)> _parts = [];
    private readonly HashSet<int> _expanded = [];
    private readonly Dictionary<int, bool> _toggled = [];
    private StackPanel? _body;
    private TextBlock? _meta;
    private Border? _working;
    private TextBlock? _workingText;
    private TextBlock? _glyph;
    private string _kind = "";

    /// <summary>Raised when the waiting badge of a tool is clicked: the host opens the approval again.</summary>
    public event EventHandler? ReviewApprovalRequested;

    /// <summary>Raised when Fix is clicked on a failed tool, with its call id.</summary>
    public event EventHandler<string>? FixRequested;

    /// <summary>Raised when a followable link of the agent's text is clicked.</summary>
    public event EventHandler<Link>? LinkActivated;

    /// <summary>True when failed tools offer Fix.</summary>
    public bool CanFix { get; set; }

    /// <summary>The row drawn now.</summary>
    public ChatRowView? Row { get; private set; }

    /// <summary>The pending message drawn now (instead of a row).</summary>
    public ChatPending? Pending { get; private set; }

    /// <summary>Draws <paramref name="row"/>; the same row kind updates in place.</summary>
    public void Show(ChatRowView row)
    {
        var kind = row.GetType().Name;
        if (kind != _kind)
        {
            Build(row);
            _kind = kind;
        }
        Row = row;
        Pending = null;
        Update(row);
    }

    /// <summary>Draws the message just sent with its "Sending…" line.</summary>
    public void ShowPending(ChatPending pending)
    {
        _kind = "pending";
        Row = null;
        Pending = pending;
        _parts.Clear();
        var panel = new StackPanel { Spacing = 4 };
        panel.Classes.Add("pending-send");
        panel.Children.Add(UserBox(string.Join('\n', new[] { pending.Text }.Concat(pending.Attachments).Where(s => s.Length > 0)), []));
        panel.Children.Add(WorkingLine(pending.Brand, pending.Working, out _, out _));
        Content = panel;
    }

    private static void Res(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        if (target is Control control)
        {
            control.Bind(property, control.GetResourceObservable(key));
        }
    }

    private static TextBlock Text(string text, string className, double? size = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.Classes.Add(className);
        if (size is { } s)
        {
            block.FontSize = s;
        }
        return block;
    }

    private static SelectableTextBlock Selectable(string text, string className)
    {
        var block = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.Classes.Add(className);
        return block;
    }

    private static Control UserBox(string text, IReadOnlyList<string> attachments)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Classes.Add("msg-user");
        var body = Selectable(text, "msg-body");
        body.IsVisible = text.Length > 0 || attachments.Count == 0;
        var box = new Border { Padding = new Thickness(12, 7), CornerRadius = new CornerRadius(ChatTokens.Radius("user")), BorderThickness = new Thickness(1), Child = body };
        Res(box, Border.BackgroundProperty, "Chat.UserBg");
        Res(box, Border.BorderBrushProperty, "Chat.UserBorder");
        stack.Children.Add(box);
        if (attachments.Count > 0)
        {
            var list = new WrapPanel();
            list.Classes.Add("attachment-list");
            foreach (var name in attachments)
            {
                var chip = Text(name, "attachment-chip", ChatTypography.Small);
                chip.Margin = new Thickness(0, 0, 6, 0);
                Res(chip, TextBlock.ForegroundProperty, "Chat.Muted");
                list.Children.Add(chip);
            }
            stack.Children.Add(list);
        }
        return stack;
    }

    private static string Glyph(string brand) => brand switch
    {
        "claude" => "✻",
        "gemini" => "✦",
        "openai" => "❀",
        "grok" => "⊘",
        "copilot" => "◉",
        "mlx" or "colibri" => "▣",
        "cursor" => "⬡",
        _ => "▶",
    };

    private static Border WorkingLine(string brand, ChatWorking? working, out TextBlock text, out TextBlock glyph)
    {
        glyph = Text(Glyph(brand), "working-glyph");
        glyph.Classes.Add("brand-" + brand);
        glyph.Foreground = new SolidColorBrush(ChatTokens.Brand(brand));
        glyph.Width = 18;
        glyph.TextAlignment = TextAlignment.Center;
        text = Text(working?.Text ?? "", "working-text");
        Res(text, TextBlock.ForegroundProperty, "Chat.Muted");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { glyph, text } };
        var border = new Border { Margin = new Thickness(0, 10, 0, 2), Child = row, IsVisible = working is not null };
        border.Classes.Add("working");
        border.Classes.Set("queued", working?.Queued == true);
        if (working?.Queued == true)
        {
            glyph.Opacity = 0.45;
        }
        return border;
    }

    private void Build(ChatRowView row)
    {
        _parts.Clear();
        _expanded.Clear();
        _toggled.Clear();
        _body = null;
        _meta = null;
        _working = null;
        switch (row)
        {
            case ChatUserRow u:
                Content = UserBox(u.Text, u.Attachments);
                break;
            case ChatNoticeRow n:
            {
                var notice = Selectable(n.Text, "msg-notice");
                notice.Classes.Set("transient", n.Transient);
                notice.FontSize = ChatTypography.Small;
                notice.Foreground = new SolidColorBrush(ChatTokens.Action("warning"));
                Content = notice;
                break;
            }
            case ChatErrorRow e:
            {
                var stack = new StackPanel { Spacing = 4 };
                stack.Classes.Add("msg-error");
                _meta = Text(e.Label, "msg-meta", ChatTypography.Small);
                Res(_meta, TextBlock.ForegroundProperty, "Chat.Muted");
                _body = new StackPanel();
                _body.Classes.Add("msg-activity");
                var message = Selectable(e.Text, "msg-body");
                message.Foreground = new SolidColorBrush(ChatTokens.Action("errorText"));
                var box = new Border
                {
                    Padding = new Thickness(12, 10),
                    CornerRadius = new CornerRadius(ChatTokens.Radius("box")),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(ChatTokens.Action("danger")),
                    Child = message,
                };
                stack.Children.Add(_meta);
                stack.Children.Add(_body);
                stack.Children.Add(box);
                Content = stack;
                break;
            }
            case ChatAssistantRow a:
            {
                var stack = new StackPanel();
                stack.Classes.Add("msg-assistant");
                _meta = Text(a.Label, "msg-agent", ChatTypography.Small);
                _meta.Margin = new Thickness(ChatTokens.Spacing("railIndent"), 0, 0, 6);
                Res(_meta, TextBlock.ForegroundProperty, "Chat.Muted");
                _body = new StackPanel();
                _body.Classes.Add("transcript");
                _working = WorkingLine(a.Brand, a.Working, out var text, out var glyph);
                _workingText = text;
                _glyph = glyph;
                stack.Children.Add(_meta);
                stack.Children.Add(_body);
                stack.Children.Add(_working);
                Content = stack;
                break;
            }
        }
    }

    private void Update(ChatRowView row)
    {
        switch (row)
        {
            case ChatUserRow u:
                Content = UserBox(u.Text, u.Attachments);
                break;
            case ChatNoticeRow n when Content is SelectableTextBlock notice:
                notice.Text = n.Text;
                notice.Classes.Set("transient", n.Transient);
                break;
            case ChatErrorRow e:
                _meta!.Text = e.Label;
                _body!.IsVisible = e.Parts.Count > 0;
                DrawParts(e.Parts);
                break;
            case ChatAssistantRow a:
                _meta!.Text = a.Label;
                _meta.IsVisible = a.ShowLabel;
                DrawParts(a.Parts);
                _working!.IsVisible = a.Working is not null;
                _working.Classes.Set("queued", a.Working?.Queued == true);
                _workingText!.Text = a.Working?.Text ?? "";
                _glyph!.Text = Glyph(a.Brand);
                _glyph.Foreground = new SolidColorBrush(ChatTokens.Brand(a.Brand));
                _glyph.Opacity = a.Working?.Queued == true ? 0.45 : 1;
                foreach (var brandClass in _glyph.Classes.Where(c => c.StartsWith("brand-", StringComparison.Ordinal)).ToList())
                {
                    _glyph.Classes.Remove(brandClass);
                }
                _glyph.Classes.Add("brand-" + a.Brand);
                break;
        }
    }

    private void DrawParts(IReadOnlyList<ChatPartView> parts)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var signature = ChatViewState.Part(parts[i])?.ToJsonString() ?? "";
            if (i < _parts.Count && _parts[i].Signature == signature)
            {
                continue;
            }
            var control = PartControl(parts[i], i);
            if (i < _parts.Count)
            {
                _body!.Children[i] = control;
                _parts[i] = (signature, control);
            }
            else
            {
                _body!.Children.Add(control);
                _parts.Add((signature, control));
            }
        }
        while (_parts.Count > parts.Count)
        {
            _parts.RemoveAt(_parts.Count - 1);
            _body!.Children.RemoveAt(_body.Children.Count - 1);
        }
    }

    private Control Rail(Control content, IBrush? dot)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ChatTokens.Spacing("railIndent")},*"), Margin = new Thickness(0, ChatTokens.Spacing("partGap")) };
        var mark = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 0) };
        mark.Classes.Add("part-dot");
        if (dot is null)
        {
            Res(mark, Shape.FillProperty, "Chat.Muted");
        }
        else
        {
            mark.Fill = dot;
        }
        grid.Children.Add(mark);
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private Control PartControl(ChatPartView part, int index) => part switch
    {
        ChatTextPart t => TextPart(t),
        ChatThinkingPart t => ThinkingPart(t, index),
        ChatToolPart t => ToolPart(t, index),
        _ => new Panel(),
    };

    private Control TextPart(ChatTextPart part)
    {
        var view = new MarkdownView();
        view.SetMarkdown(part.Text);
        view.LinkActivated += (_, link) => LinkActivated?.Invoke(this, link);
        var rail = Rail(view, null);
        rail.Classes.Add("part");
        rail.Classes.Add("part-text");
        return rail;
    }

    private Control ThinkingPart(ChatThinkingPart part, int index)
    {
        var label = Text(part.Label, "thinking-label");
        Res(label, TextBlock.ForegroundProperty, "Chat.Muted");
        var text = Selectable(part.Text, "part-pre");
        text.FontSize = ChatTypography.Small;
        Res(text, TextBlock.ForegroundProperty, "Chat.Muted");
        var expander = new Expander
        {
            Header = label,
            Content = text,
            IsExpanded = _toggled.TryGetValue(index, out var open) ? open : part.Open,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        expander.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty)
            {
                _toggled[index] = expander.IsExpanded;
            }
        };
        var rail = Rail(expander, null);
        rail.Classes.Add("part");
        rail.Classes.Add("part-thinking");
        return rail;
    }

    private static IBrush StatusBrush(string status) => status switch
    {
        "ok" => new SolidColorBrush(ChatTokens.Action("ok")),
        "error" => new SolidColorBrush(ChatTokens.Action("danger")),
        _ => new SolidColorBrush(Colors.Gray),
    };

    private Control ToolPart(ChatToolPart part, int index)
    {
        var isOpen = _expanded.Contains(index);
        var head = new WrapPanel { Orientation = Orientation.Horizontal };
        head.Classes.Add("tool-head");
        var name = Text(part.Name, "tool-name");
        name.FontWeight = FontWeight.SemiBold;
        name.Margin = new Thickness(0, 0, 8, 0);
        Res(name, TextBlock.ForegroundProperty, "Chat.Strong");
        var summary = Text(part.Summary, "tool-summary", ChatTypography.Small);
        summary.TextWrapping = TextWrapping.NoWrap;
        summary.TextTrimming = TextTrimming.CharacterEllipsis;
        summary.FontFamily = new FontFamily(ChatSpec.Tokens["typography"]!["monoFamily"]!.GetValue<string>().Replace("ui-monospace, ", "", StringComparison.Ordinal));
        summary.MaxWidth = 640;
        summary.Margin = new Thickness(0, 0, 8, 0);
        Res(summary, TextBlock.ForegroundProperty, "Chat.Muted");
        head.Children.Add(name);
        head.Children.Add(summary);
        Badges(part, head);

        var box = new StackPanel();
        box.Classes.Add("tool-box");
        if (part.Diff is { } diff)
        {
            var lines = new StackPanel();
            lines.Classes.Add("diff");
            foreach (var line in isOpen ? diff : diff.Take((int)ChatSpec.Limit("clipLines")))
            {
                var block = Selectable(line.Text, "diff-line");
                block.Classes.Add("diff-" + line.Kind);
                block.FontFamily = summary.FontFamily;
                block.FontSize = ChatTypography.Code;
                block.Padding = new Thickness(12, 0);
                if (line.Kind is "add" or "del")
                {
                    block.Background = new SolidColorBrush(ChatTokens.Action(line.Kind == "add" ? "diffAddBg" : "diffDelBg"));
                    block.Foreground = new SolidColorBrush(ChatTokens.Action(line.Kind == "add" ? "diffAddText" : "diffDelText"));
                }
                lines.Children.Add(block);
            }
            box.Children.Add(lines);
        }
        else
        {
            box.Children.Add(IoRow(ChatSpec.Label("tools", "io", "in"), part.Input ?? "", isOpen, summary.FontFamily));
        }
        var output = IoRow(ChatSpec.Label("tools", "io", "out"), part.Output, isOpen, summary.FontFamily);
        output.IsVisible = part.OutputShown;
        box.Children.Add(output);
        var frame = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ChatTokens.Radius("tool")),
            Child = box,
            Cursor = new Cursor(StandardCursorType.Hand),
            IsVisible = !part.Compact || _toggled.GetValueOrDefault(index),
        };
        Res(frame, Border.BackgroundProperty, "Chat.CodeBg");
        Res(frame, Border.BorderBrushProperty, "Chat.Border");
        frame.PointerReleased += (_, _) => Toggle(index, open: !_expanded.Contains(index));
        if (part.Compact)
        {
            head.Cursor = new Cursor(StandardCursorType.Hand);
            head.PointerReleased += (_, _) =>
            {
                _toggled[index] = !_toggled.GetValueOrDefault(index);
                Redraw(index);
            };
        }
        var stack = new StackPanel { Children = { head, frame } };
        var rail = Rail(stack, part.Status == "running" ? null : StatusBrush(part.Status));
        var dot = ((Grid)rail).Children.OfType<Ellipse>().First();
        ToolTip.SetTip(dot, part.StatusLabel);
        global::Avalonia.Automation.AutomationProperties.SetName(dot, part.StatusLabel);
        rail.Classes.Add("part");
        rail.Classes.Add("part-tool");
        rail.Classes.Add("tool-" + part.Kind);
        rail.Classes.Add(part.Status);
        rail.Classes.Set("compact", part.Compact);
        return rail;
    }

    private void Toggle(int index, bool open)
    {
        if (open)
        {
            _expanded.Add(index);
        }
        else
        {
            _expanded.Remove(index);
        }
        Redraw(index);
    }

    private void Redraw(int index)
    {
        if (Row is not ChatAssistantRow and not ChatErrorRow || index >= _parts.Count)
        {
            return;
        }
        var parts = Row is ChatAssistantRow a ? a.Parts : ((ChatErrorRow)Row).Parts;
        var control = PartControl(parts[index], index);
        _body!.Children[index] = control;
        _parts[index] = (_parts[index].Signature, control);
    }

    private static Grid IoRow(string label, string text, bool isOpen, FontFamily font)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("48,*") };
        grid.Classes.Add("io-row");
        var name = Text(label, "io-label", ChatTypography.Small);
        name.Margin = new Thickness(12, 8, 0, 8);
        name.FontFamily = font;
        Res(name, TextBlock.ForegroundProperty, "Chat.Muted");
        var shown = isOpen ? text : ToolText.ClipPreview(text);
        var body = Selectable(shown, "clip");
        body.Classes.Set("overflowing", shown != text);
        body.FontFamily = font;
        body.FontSize = ChatTypography.Code;
        body.Margin = new Thickness(12, 8);
        Grid.SetColumn(body, 1);
        grid.Children.Add(name);
        grid.Children.Add(body);
        return grid;
    }

    private void Badges(ChatToolPart part, Panel head)
    {
        if (part.Approval is { } approval)
        {
            if (approval.State == "waiting")
            {
                var button = new Button { Content = approval.Label, Padding = new Thickness(6, 1), FontSize = ChatTypography.Small };
                button.Classes.Add("tool-approval");
                button.Classes.Add(approval.State);
                button.Foreground = new SolidColorBrush(ChatTokens.Action("warning"));
                button.BorderBrush = button.Foreground;
                button.BorderThickness = new Thickness(1);
                button.Background = Brushes.Transparent;
                ToolTip.SetTip(button, ChatSpec.Label("approval", "waitingTitle"));
                button.Click += (_, _) => ReviewApprovalRequested?.Invoke(this, EventArgs.Empty);
                head.Children.Add(button);
            }
            else
            {
                var badge = Text(approval.Label, "tool-approval", ChatTypography.Small);
                badge.Classes.Add(approval.State);
                badge.Margin = new Thickness(0, 0, 8, 0);
                badge.Foreground = new SolidColorBrush(ChatTokens.Action(approval.State switch
                {
                    "denied" => "errorText",
                    "blocked" => "warning",
                    _ => "ok",
                }));
                if (approval.DetailsUrl is { } url)
                {
                    badge.Classes.Add("security-link");
                    ToolTip.SetTip(badge, ChatSpec.Label("approval", "blockedTitle") + "\n" + url);
                }
                head.Children.Add(badge);
            }
        }
        if (part.Injection is { } injection)
        {
            var badge = Text(injection.Label, "tool-injection", ChatTypography.Small);
            badge.Margin = new Thickness(0, 0, 8, 0);
            badge.Foreground = new SolidColorBrush(ChatTokens.Action("warning"));
            ToolTip.SetTip(badge, injection.DetailsUrl is null ? injection.Title : injection.Title + "\n" + injection.DetailsUrl);
            head.Children.Add(badge);
        }
        if (part.Fixable && CanFix)
        {
            var fix = new Button { Content = ChatSpec.Label("tools", "fix"), Padding = new Thickness(8, 1), FontSize = ChatTypography.Small };
            fix.Classes.Add("tool-fix");
            ToolTip.SetTip(fix, ChatSpec.Label("tools", "fixTitle"));
            fix.Click += (_, _) => FixRequested?.Invoke(this, part.CallId);
            head.Children.Add(fix);
        }
    }
}
