using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D3sk.ChatUi.Avalonia.Composer;
using D3sk.ChatUi.Avalonia.QuickSettings;
using D3sk.ChatUi.Avalonia.Theme;
using D3sk.ChatUi.Avalonia.Transcript;
using D3sk.ChatUi.Composer;
using D3sk.ChatUi.Labels;
using D3sk.ChatUi.Session;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Avalonia.Chat;

/// <summary>
/// D3SKHAND's chat as one Avalonia control: the virtualized transcript, the working line, the
/// approval panel and the composer with its quick settings. Its host API is the web package's
/// mountChat: give the agents, feed the turn's frames (or D3sk.AgentCli events), resume a saved chat,
/// and implement <see cref="IChatHost"/> for Send, Stop and the approvals.
/// </summary>
public sealed class ChatView : UserControl
{
    private const char Separator = '\u001f';

    private readonly Func<double> _clock;
    private readonly Dictionary<long, (TranscriptItem Item, string Signature)> _drawn = [];
    private readonly DispatcherTimer _ticker;
    private TranscriptItem? _pendingItem;
    private IReadOnlyList<ChatAgent> _agents;
    private bool _scheduled;
    private string? _agentChoice;
    private string _permission = ChatSpec.Label("permissions", "default");
    private string? _effort;

    /// <summary>Creates the chat for <paramref name="host"/>.</summary>
    public ChatView(IChatHost host, IReadOnlyList<ChatAgent>? agents = null, string? projectRoot = null, string? palette = null, Func<double>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        _agents = agents ?? [];
        Session = new ChatSession(_agents, projectRoot, _clock);
        Controller = new ChatController(Session, host);
        Transcript = new TranscriptView { ProjectRoot = projectRoot, CanFix = host.CanFixTools };
        Composer = new ComposerBox(new ComposerState());
        QuickSettings = new QuickSettingsBar();
        Approvals = new ApprovalPanel { Margin = new Thickness(16, 0) };
        ChatTheme.Apply(Resources, palette);
        Bind(BackgroundProperty, this.GetResourceObservable("Chat.Bg"));

        var jump = new Button
        {
            Content = ChatSpec.Label("transcript", "jumpToBottom"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
            IsVisible = false,
        };
        jump.Classes.Add("jump-bottom");
        jump.Click += (_, _) => Transcript.JumpToBottom();
        JumpButton = jump;
        var transcript = new Panel { Children = { Transcript, jump } };
        var foot = new StackPanel { Spacing = 8, Margin = new Thickness(16, 6, 16, 16), Children = { Approvals, QuickSettings, Composer } };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(foot, 1);
        grid.Children.Add(transcript);
        grid.Children.Add(foot);
        Content = grid;

        Composer.SendRequested += async (_, _) => await SendAsync();
        Composer.StopRequested += async (_, _) => await Controller.StopAsync();
        QuickSettings.SettingChanged += (_, change) =>
        {
            switch (change.Kind)
            {
                case QuickSettingKind.Model:
                    _agentChoice = change.Value;
                    break;
                case QuickSettingKind.Permission:
                    _permission = change.Value;
                    break;
                case QuickSettingKind.Effort:
                    _effort = change.Value;
                    break;
            }
            SettingChanged?.Invoke(this, change);
        };
        Approvals.Answered += async (_, answer) =>
        {
            await Controller.AnswerAsync(answer, Report);
            Approvals.Settled();
        };
        Transcript.ReviewApprovalRequested += (_, _) => Approvals.Review();
        Transcript.FixRequested += async (_, callId) => await host.FixToolAsync(callId);
        Transcript.LinkActivated += (_, link) => LinkActivated?.Invoke(this, link);
        Session.Changed += (_, _) => Schedule();
        _ticker = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            if (Session.Busy)
            {
                Render();
            }
            JumpButton.IsVisible = !Transcript.IsAtBottom;
        });
        AttachedToVisualTree += (_, _) => _ticker.Start();
        DetachedFromVisualTree += (_, _) => _ticker.Stop();
        RefreshPickers();
        Render();
    }

    /// <summary>The chat's state; feed it directly or through this view.</summary>
    public ChatSession Session { get; }

    /// <summary>Send, Stop and the approvals, through the host.</summary>
    public ChatController Controller { get; }

    /// <summary>The virtualized transcript.</summary>
    public TranscriptView Transcript { get; }

    /// <summary>The message box.</summary>
    public ComposerBox Composer { get; }

    /// <summary>The model, permission and effort pickers.</summary>
    public QuickSettingsBar QuickSettings { get; }

    /// <summary>The approval panel over the composer.</summary>
    public ApprovalPanel Approvals { get; }

    /// <summary>Shown while the reader is scrolled away from the newest message.</summary>
    public Button JumpButton { get; }

    /// <summary>Raised when the host refused a send or an answer; the view already gave the words back.</summary>
    public event EventHandler<string>? ErrorReported;

    /// <summary>Raised when a picker changed (model, permission, effort): the host may apply it to the running turn.</summary>
    public event EventHandler<QuickSettingChange>? SettingChanged;

    /// <summary>Raised when a followable link of the agent's text is clicked: the host opens it.</summary>
    public event EventHandler<D3sk.ChatUi.Markdown.Link>? LinkActivated;

    /// <summary>Replaces the agents (the model picker and the reply labels).</summary>
    public void SetAgents(IReadOnlyList<ChatAgent> agents)
    {
        _agents = agents;
        Session.SetAgents(agents);
        RefreshPickers();
    }

    /// <summary>Folds one wire frame (text, tool_call, done… and the host's start).</summary>
    public bool Feed(string name, JsonObject? data) => Session.Feed(name, data);

    /// <summary>Folds one D3sk.AgentCli event in its JSON form.</summary>
    public bool FeedEvent(JsonObject agentCliEvent) => Session.FeedEvent(agentCliEvent);

    /// <summary>Loads a saved chat (messages, approvals, running…): see <see cref="ChatSession.Resume"/>.</summary>
    public void Resume(JsonObject state)
    {
        Session.Resume(state);
        Transcript.JumpToBottom();
    }

    /// <summary>Switches the palette (teal, slate, midnight, ocean, forest, dusk).</summary>
    public void SetPalette(string? id) => ChatTheme.Apply(Resources, id);

    /// <summary>Draws the session now (at <paramref name="at"/>, by default the clock's now).</summary>
    public ChatViewState Render(double? at = null)
    {
        _scheduled = false;
        var view = Session.View(at ?? _clock());
        var items = Transcript.Items;
        var wanted = new List<TranscriptItem>(view.Rows.Count + 1);
        var alive = new HashSet<long>();
        foreach (var row in view.Rows)
        {
            alive.Add(row.Key);
            var signature = Signature(row);
            if (_drawn.TryGetValue(row.Key, out var drawn))
            {
                if (drawn.Signature != signature)
                {
                    drawn.Item.Row = row;
                    _drawn[row.Key] = (drawn.Item, signature);
                }
                wanted.Add(drawn.Item);
            }
            else
            {
                var item = new TranscriptItem(TranscriptItem.RoleOf(row)) { Row = row };
                _drawn[row.Key] = (item, signature);
                wanted.Add(item);
            }
        }
        foreach (var key in _drawn.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _drawn.Remove(key);
        }
        if (view.Pending is { } pending)
        {
            _pendingItem ??= new TranscriptItem(TranscriptRole.User);
            _pendingItem.Pending = pending;
            wanted.Add(_pendingItem);
        }
        else
        {
            _pendingItem = null;
        }
        Sync(items, wanted);
        Approvals.Show(view.Approvals);
        if (Composer.State.Busy != view.Busy)
        {
            Composer.State.Busy = view.Busy;
            Composer.Refresh();
        }
        return view;
    }

    private static string Signature(ChatRowView row) => row switch
    {
        ChatAssistantRow a => $"{a.Rev}|{a.ShowLabel}|{a.Label}|{a.Working?.Text}|{a.Working?.Queued}|{a.Brand}",
        ChatErrorRow e => $"{e.Rev}|{e.Label}",
        _ => row.Rev.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static void Sync(System.Collections.ObjectModel.ObservableCollection<TranscriptItem> items, List<TranscriptItem> wanted)
    {
        var keep = new HashSet<TranscriptItem>(wanted);
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(items[i]))
            {
                items.RemoveAt(i);
            }
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < items.Count && ReferenceEquals(items[i], wanted[i]))
            {
                continue;
            }
            var at = items.IndexOf(wanted[i]);
            if (at >= 0)
            {
                items.Move(at, i);
            }
            else
            {
                items.Insert(i, wanted[i]);
            }
        }
    }

    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }
        _scheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_scheduled)
            {
                Render();
            }
        }, DispatcherPriority.Background);
    }

    private void Report(Exception error) => ErrorReported?.Invoke(this, error.Message);

    private (string Agent, string Model) Selection()
    {
        var value = _agentChoice ?? "";
        var at = value.IndexOf(Separator);
        if (at >= 0)
        {
            return (value[..at], value[(at + 1)..]);
        }
        var first = _agents.FirstOrDefault();
        return (first?.Id ?? "", first?.Models.FirstOrDefault()?.Id ?? "");
    }

    private void RefreshPickers()
    {
        var models = _agents.SelectMany(a => a.Models.Select(m => new SettingOption($"{a.Id}{Separator}{m.Id}", $"{a.Name} · {m.Name}"))).ToList();
        if (_agentChoice is null || models.All(m => m.Value != _agentChoice))
        {
            _agentChoice = models.FirstOrDefault()?.Value;
        }
        QuickSettings.SetOptions(QuickSettingKind.Model, models, _agentChoice);
        QuickSettings.SetOptions(QuickSettingKind.Permission, ChatLabels.PermissionModes.Select(m => new SettingOption(m.Value, m.Label)).ToList(), _permission);
    }

    /// <summary>Sends what the box holds; on refusal the words come back and <see cref="ErrorReported"/> says why.</summary>
    public async Task<bool> SendAsync()
    {
        if (!Composer.State.CanSend)
        {
            return false;
        }
        var draft = Composer.State.Take();
        Composer.Refresh();
        var (agent, model) = Selection();
        var request = new ChatSendRequest(draft.Text, draft.Attachments, agent, model, _permission, _effort);
        var sent = await Controller.SendAsync(request, Report);
        if (!sent && Composer.State.Text.Length == 0)
        {
            Composer.State.Load(draft);
            Composer.Refresh();
        }
        Render();
        return sent;
    }
}
