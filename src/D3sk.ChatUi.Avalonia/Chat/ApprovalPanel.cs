using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D3sk.ChatUi.Avalonia.Theme;
using D3sk.ChatUi.Labels;
using D3sk.ChatUi.Session;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Avalonia.Chat;

/// <summary>
/// The approval dialog of the web chat (approvals.js) as a panel over the composer: Allow / Deny with
/// an optional message, or an AskUserQuestion's options with "Other…". Later (or Esc) folds it into a
/// banner that brings it back. It answers through <see cref="Answered"/>; the host decides.
/// </summary>
public sealed class ApprovalPanel : UserControl
{
    private readonly TextBlock _title = new() { FontSize = ChatTypography.Heading, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _count = new() { FontSize = ChatTypography.Small, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _detail = new() { Spacing = 8 };
    private readonly TextBox _message = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 2000, MinHeight = 48,
        PlaceholderText = ChatSpec.Label("approval", "messagePlaceholder"),
    };
    private readonly Button _later = Make("later");
    private readonly Button _skip = Make("skip");
    private readonly Button _deny = Make("deny");
    private readonly Button _allow = Make("allow");
    private readonly Button _send = Make("send");
    private readonly Button _banner = new() { Content = ChatSpec.Label("approval", "banner"), HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false };
    private readonly Border _dialog;
    private readonly Dictionary<string, List<string>> _picked = [];
    private readonly Dictionary<string, string?> _own = [];
    private readonly Dictionary<string, TextBox> _ownBoxes = [];
    private IReadOnlyList<ChatApprovalView> _approvals = [];
    private string? _shownId;
    private string? _putOff;
    private bool _busy;

    /// <summary>Creates a hidden panel.</summary>
    public ApprovalPanel()
    {
        _banner.Classes.Add("approval-banner");
        _banner.Foreground = new SolidColorBrush(ChatTokens.Action("warning"));
        _banner.BorderBrush = _banner.Foreground;
        _banner.BorderThickness = new Thickness(1);
        _banner.Click += (_, _) => Review();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _title, _count } };
        var actions = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_later, Dock.Left);
        DockPanel.SetDock(_skip, Dock.Left);
        foreach (var button in new[] { _send, _allow, _deny })
        {
            DockPanel.SetDock(button, Dock.Right);
        }
        actions.Children.Add(_later);
        actions.Children.Add(_skip);
        actions.Children.Add(_send);
        actions.Children.Add(_allow);
        actions.Children.Add(_deny);
        _allow.Background = new SolidColorBrush(ChatTokens.Action("accent"));
        _send.Background = _allow.Background;
        _deny.Background = new SolidColorBrush(ChatTokens.Action("danger"));
        var body = new StackPanel
        {
            Spacing = 10,
            Children = { head, _summary, new ScrollViewer { Content = _detail, MaxHeight = 360 }, _message, actions },
        };
        _dialog = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Child = body,
            IsVisible = false,
        };
        _dialog.Classes.Add("approval-dialog");
        _dialog.Bind(Border.BackgroundProperty, _dialog.GetResourceObservable("Chat.Panel"));
        _dialog.Bind(Border.BorderBrushProperty, _dialog.GetResourceObservable("Chat.Border"));
        _summary.Bind(TextBlock.ForegroundProperty, _summary.GetResourceObservable("Chat.Muted"));
        _count.Bind(TextBlock.ForegroundProperty, _count.GetResourceObservable("Chat.Muted"));
        Content = new StackPanel { Spacing = 8, Children = { _banner, _dialog } };
        _allow.Click += (_, _) => Respond(new ChatApprovalAnswer(Current!.ApprovalId, true, _message.Text?.Trim() ?? ""));
        _deny.Click += (_, _) => Respond(new ChatApprovalAnswer(Current!.ApprovalId, false, _message.Text?.Trim() ?? ""));
        _send.Click += (_, _) => Respond(new ChatApprovalAnswer(Current!.ApprovalId, true, Answers: Answers()));
        _skip.Click += (_, _) => Respond(new ChatApprovalAnswer(Current!.ApprovalId, true, Answers: []));
        _later.Click += (_, _) => Later();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _dialog.IsVisible)
            {
                e.Handled = true;
                Later();
            }
        };
    }

    /// <summary>Raised when the person answers; the view hands it to the host and calls <see cref="Settled"/>.</summary>
    public event EventHandler<ChatApprovalAnswer>? Answered;

    /// <summary>The approval shown first, or null.</summary>
    public ChatApprovalView? Current => _approvals.Count > 0 ? _approvals[0] : null;

    /// <summary>True while the dialog shows (not folded behind the banner).</summary>
    public bool IsOpen => _dialog.IsVisible;

    /// <summary>The parts a test or a host layout reaches.</summary>
    public (TextBlock Title, TextBlock Summary, StackPanel Detail, TextBox Message, Button Allow, Button Deny, Button Later, Button Send, Button Skip, Button Banner) Parts =>
        (_title, _summary, _detail, _message, _allow, _deny, _later, _send, _skip, _banner);

    /// <summary>Shows the first of <paramref name="approvals"/> (or nothing).</summary>
    public void Show(IReadOnlyList<ChatApprovalView> approvals)
    {
        _approvals = approvals;
        var approval = Current;
        if (approval is null)
        {
            _banner.IsVisible = false;
            _dialog.IsVisible = false;
            _shownId = null;
            return;
        }
        var putOff = _putOff == approval.ApprovalId;
        _banner.IsVisible = putOff;
        _dialog.IsVisible = !putOff;
        if (_shownId != approval.ApprovalId)
        {
            Fill(approval);
        }
        _count.Text = approvals.Count > 1
            ? ChatSpec.Fill(ChatSpec.Label("approval", "count"), ("index", "1"), ("count", approvals.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            : "";
    }

    /// <summary>Opens the dialog again after Later.</summary>
    public void Review()
    {
        _putOff = null;
        Show(_approvals);
    }

    /// <summary>The host answered (or failed): the buttons work again.</summary>
    public void Settled()
    {
        _busy = false;
        foreach (var button in new[] { _allow, _deny, _skip })
        {
            button.IsEnabled = true;
        }
        UpdateSend();
    }

    private static Button Make(string key)
    {
        var button = new Button { Content = ChatSpec.Label("approval", key) };
        button.Classes.Add("approval-" + key);
        return button;
    }

    private void Later()
    {
        _putOff = Current?.ApprovalId;
        Show(_approvals);
    }

    private void Respond(ChatApprovalAnswer answer)
    {
        if (_busy || Current is null)
        {
            return;
        }
        _busy = true;
        foreach (var button in new[] { _allow, _deny, _skip, _send })
        {
            button.IsEnabled = false;
        }
        Answered?.Invoke(this, answer);
    }

    private System.Text.Json.Nodes.JsonObject Answers() =>
        ChatLabels.QuestionAnswers(
            Current?.Questions.Select(q => (q.Question, q.MultiSelect)) ?? [],
            _picked.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value),
            _own.ToDictionary(p => p.Key, p => _ownBoxes.TryGetValue(p.Key, out var box) ? box.Text : p.Value));

    private void UpdateSend() => _send.IsEnabled = !_busy && Answers().Count > 0;

    private void Fill(ChatApprovalView approval)
    {
        _shownId = approval.ApprovalId;
        _title.Text = approval.Title;
        _summary.Text = approval.Summary;
        _summary.IsVisible = approval.Summary.Length > 0;
        _message.Text = "";
        _picked.Clear();
        _own.Clear();
        _ownBoxes.Clear();
        _detail.Children.Clear();
        var isQuestion = approval.IsQuestion;
        _dialog.Classes.Set("questions", isQuestion);
        _message.IsVisible = !isQuestion;
        _later.IsVisible = !isQuestion;
        _deny.IsVisible = !isQuestion;
        _allow.IsVisible = !isQuestion;
        _skip.IsVisible = isQuestion;
        _send.IsVisible = isQuestion;
        if (isQuestion)
        {
            foreach (var question in approval.Questions)
            {
                _detail.Children.Add(QuestionBlock(question));
            }
            UpdateSend();
            return;
        }
        var box = new StackPanel();
        box.Classes.Add("tool-box");
        if (approval.Diff is { } diff)
        {
            foreach (var line in diff)
            {
                var block = new SelectableTextBlock { Text = line.Text, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(12, 0), FontSize = ChatTypography.Code };
                block.Classes.Add("diff-line");
                block.Classes.Add("diff-" + line.Kind);
                if (line.Kind is "add" or "del")
                {
                    block.Background = new SolidColorBrush(ChatTokens.Action(line.Kind == "add" ? "diffAddBg" : "diffDelBg"));
                    block.Foreground = new SolidColorBrush(ChatTokens.Action(line.Kind == "add" ? "diffAddText" : "diffDelText"));
                }
                box.Children.Add(block);
            }
        }
        else
        {
            var input = new SelectableTextBlock { Text = approval.Input ?? "", TextWrapping = TextWrapping.Wrap, FontSize = ChatTypography.Code, Margin = new Thickness(12, 8) };
            input.Classes.Add("approval-input");
            box.Children.Add(input);
        }
        _detail.Children.Add(box);
    }

    private Control QuestionBlock(ChatQuestion question)
    {
        var block = new StackPanel { Spacing = 8 };
        block.Classes.Add("question-block");
        var head = new WrapPanel();
        if (question.Header.Length > 0)
        {
            var tag = new TextBlock { Text = question.Header, FontSize = ChatTypography.Small, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 8, 0) };
            tag.Classes.Add("question-tag");
            tag.Foreground = new SolidColorBrush(ChatTokens.Action("accent"));
            head.Children.Add(tag);
        }
        var text = new TextBlock { Text = question.Question, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        text.Classes.Add("question-text");
        head.Children.Add(text);
        block.Children.Add(head);
        var options = new StackPanel { Spacing = 6 };
        var own = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, IsVisible = false,
            MaxLength = ChatSpec.Labels["approval"]!["ownAnswerLength"]!.GetValue<int>(),
            PlaceholderText = ChatSpec.Label("approval", "ownPlaceholder"),
        };
        own.Classes.Add("question-own");
        _ownBoxes[question.Question] = own;
        var other = Option(ChatSpec.Label("approval", "other"), ChatSpec.Label("approval", "otherDescription"));
        other.Classes.Add("question-other");
        foreach (var option in question.Options)
        {
            var pick = Option(option.Label, option.Description);
            pick.Click += (_, _) =>
            {
                var key = question.Question;
                var list = _picked.TryGetValue(key, out var found) ? found : _picked[key] = [];
                if (question.MultiSelect)
                {
                    if (!list.Remove(option.Label))
                    {
                        list.Add(option.Label);
                    }
                    if (list.Count == 0)
                    {
                        _picked.Remove(key);
                    }
                    pick.Classes.Set("selected", list.Contains(option.Label));
                }
                else
                {
                    _picked[key] = [option.Label];
                    foreach (var sibling in options.Children)
                    {
                        sibling.Classes.Set("selected", sibling == pick);
                    }
                    if (_own.ContainsKey(key))
                    {
                        _own.Remove(key);
                        own.IsVisible = false;
                    }
                }
                UpdateSend();
            };
            options.Children.Add(pick);
        }
        other.Click += (_, _) =>
        {
            var key = question.Question;
            var isActive = !_own.ContainsKey(key);
            if (isActive)
            {
                _own[key] = own.Text;
            }
            else
            {
                _own.Remove(key);
            }
            other.Classes.Set("selected", isActive);
            own.IsVisible = isActive;
            if (isActive && !question.MultiSelect)
            {
                _picked.Remove(key);
                foreach (var sibling in options.Children)
                {
                    sibling.Classes.Set("selected", sibling == other);
                }
            }
            if (isActive)
            {
                own.Focus();
            }
            UpdateSend();
        };
        own.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && _own.ContainsKey(question.Question))
            {
                UpdateSend();
            }
        };
        options.Children.Add(other);
        block.Children.Add(options);
        block.Children.Add(own);
        if (question.MultiSelect)
        {
            var hint = new TextBlock { Text = ChatSpec.Label("approval", "multiHint"), FontSize = ChatTypography.Small };
            hint.Classes.Add("question-hint");
            block.Children.Add(hint);
        }
        return block;
    }

    private static Button Option(string label, string description)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold });
        if (description.Length > 0)
        {
            content.Children.Add(new TextBlock { Text = description, FontSize = ChatTypography.Small, TextWrapping = TextWrapping.Wrap });
        }
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        button.Classes.Add("question-option");
        return button;
    }
}
