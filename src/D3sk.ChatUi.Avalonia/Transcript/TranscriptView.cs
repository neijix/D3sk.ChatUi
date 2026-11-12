using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using D3sk.ChatUi.Labels;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>The conversation: a virtualized list of <see cref="TranscriptItem"/>s. It stays at the bottom while a reply
/// streams if it was there, stays put if the person scrolled up, and <see cref="JumpToBottom"/> goes back down.</summary>
public sealed class TranscriptView : UserControl
{
    /// <summary>The most rows realized beyond the ones the viewport shows (the fixed buffer of AC4).</summary>
    public const int BufferRows = 4;

    private const double BottomTolerance = 1.0;

    private readonly ItemsControl _list;
    private readonly TextBlock _status = new() { Opacity = 0.7, Margin = new global::Avalonia.Thickness(12, 4), IsVisible = false };
    private readonly ScrollViewer _scroll;
    private bool _stickToBottom = true;
    private bool _scrolling;

    /// <summary>Creates an empty transcript.</summary>
    public TranscriptView()
    {
        _list = new ItemsControl
        {
            ItemsSource = Items,
            ItemsPanel = new FuncTemplate<global::Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<TranscriptItem>((_, _) => NewRow()),
        };
        var dock = new DockPanel();
        DockPanel.SetDock(_status, Dock.Top);
        dock.Children.Add(_status);
        _scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _scroll.ScrollChanged += OnScrollChanged;
        dock.Children.Add(_scroll);
        Content = dock;
        Items.CollectionChanged += (_, _) => StickIfAtBottom();
    }

    /// <summary>Raised when a tool's waiting badge is clicked in any row.</summary>
    public event EventHandler? ReviewApprovalRequested;

    /// <summary>Raised when Fix is clicked on a failed tool, with its call id.</summary>
    public event EventHandler<string>? FixRequested;

    /// <summary>Raised when a followable link of the agent's text is clicked.</summary>
    public event EventHandler<D3sk.ChatUi.Markdown.Link>? LinkActivated;

    /// <summary>True when failed tools offer Fix; set before items are added.</summary>
    public bool CanFix { get; set; }

    /// <summary>The messages, oldest first. Add to it to append; change an item to stream into it.</summary>
    public ObservableCollection<TranscriptItem> Items { get; } = [];

    /// <summary>The project folder that tool paths are shown relative to; set before items are added.</summary>
    public string? ProjectRoot { get; set; }

    /// <summary>True while new content keeps the view at the bottom.</summary>
    public bool IsAtBottom => _stickToBottom;

    /// <summary>How many rows are realized now (visible plus the virtualizer's buffer).</summary>
    public int RealizedRowCount => _list.GetVisualDescendants().OfType<TranscriptRow>().Count(r => r.IsEffectivelyVisible);

    /// <summary>The vertical scroll offset, in pixels.</summary>
    public double VerticalOffset => _scroll.Offset.Y;

    /// <summary>The distance from the end of the content to the bottom of the viewport, in pixels.</summary>
    public double DistanceFromBottom => Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height - _scroll.Offset.Y);

    /// <summary>Shows the chat's status pill text above the list, or nothing when there is none.</summary>
    public void SetStatus(ChatStatusInput? chat, double nowSeconds)
    {
        var pill = ChatLabels.ChatStatusLabel(chat, nowSeconds);
        _status.Text = pill?.Text ?? "";
        _status.IsVisible = pill is not null;
    }

    /// <summary>Scrolls to the newest content and keeps following it.</summary>
    public void JumpToBottom()
    {
        _stickToBottom = true;
        ScrollToEnd();
    }

    private TranscriptRow NewRow()
    {
        var row = new TranscriptRow { ProjectRoot = ProjectRoot, CanFix = CanFix };
        row.ReviewApprovalRequested += (_, _) => ReviewApprovalRequested?.Invoke(this, EventArgs.Empty);
        row.FixRequested += (_, callId) => FixRequested?.Invoke(this, callId);
        row.LinkActivated += (_, link) => LinkActivated?.Invoke(this, link);
        return row;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrolling)
        {
            return;
        }
        var atBottom = DistanceFromBottom <= BottomTolerance;
        if (e.OffsetDelta.Y < 0 && !atBottom)
        {
            _stickToBottom = false; // moved up, away from the end: the person is reading
        }
        else if (e.OffsetDelta.Y > 0 && atBottom)
        {
            _stickToBottom = true;
        }
        if (_stickToBottom && !atBottom && (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0))
        {
            ScrollToEnd(); // the content grew or the window resized while following
        }
    }

    private void StickIfAtBottom()
    {
        if (_stickToBottom)
        {
            ScrollToEnd();
        }
    }

    private void ScrollToEnd()
    {
        if (_scrolling)
        {
            return;
        }
        _scrolling = true;
        try
        {
            if (Items.Count > 0)
            {
                _list.ScrollIntoView(Items.Count - 1);
            }
            _scroll.ScrollToEnd();
        }
        finally
        {
            _scrolling = false;
        }
    }
}
