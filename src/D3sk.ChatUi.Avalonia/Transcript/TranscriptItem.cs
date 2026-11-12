using System.Text.Json.Nodes;
using D3sk.ChatUi.Session;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>Who wrote a transcript item.</summary>
public enum TranscriptRole
{
    /// <summary>The person.</summary>
    User,

    /// <summary>The agent.</summary>
    Assistant,

    /// <summary>A status line.</summary>
    Notice,

    /// <summary>An error.</summary>
    Error,
}

/// <summary>A tool call inside a reply. <paramref name="Kind"/> is the corpus kind (bash, read, edit, write, ...).</summary>
public sealed record ToolPart(string Name, string Kind, JsonNode? Args);

/// <summary>One message of the transcript. A streaming reply changes <see cref="Text"/> and <see cref="Tools"/>; the row shows it.</summary>
public sealed class TranscriptItem
{
    private string _text = "";
    private IReadOnlyList<ToolPart> _tools = [];
    private bool _working;
    private string _activity = "starting";
    private double _elapsedSeconds;
    private ChatRowView? _row;
    private ChatPending? _pending;

    /// <summary>Creates an item.</summary>
    public TranscriptItem(TranscriptRole role, string text = "")
    {
        Role = role;
        _text = text;
    }

    /// <summary>Raised after any property below changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Who wrote it.</summary>
    public TranscriptRole Role { get; }

    /// <summary>The message text: markdown for the agent, plain text for the person.</summary>
    public string Text { get => _text; set => Set(ref _text, value); }

    /// <summary>The tool calls of this reply, in order.</summary>
    public IReadOnlyList<ToolPart> Tools { get => _tools; set => Set(ref _tools, value); }

    /// <summary>True while the reply is still being written: a working line shows under it.</summary>
    public bool IsWorking { get => _working; set => Set(ref _working, value); }

    /// <summary>What the agent is doing now, as <c>ChatLabels.WorkingActivity</c> names it.</summary>
    public string Activity { get => _activity; set => Set(ref _activity, value); }

    /// <summary>Seconds the reply has been running; picks the working phrase.</summary>
    public double ElapsedSeconds { get => _elapsedSeconds; set => Set(ref _elapsedSeconds, value); }

    /// <summary>A whole row of a <see cref="ChatSession"/>: when set, the row draws it (parts, badges, working line) instead of <see cref="Text"/> and <see cref="Tools"/>.</summary>
    public ChatRowView? Row { get => _row; set => Set(ref _row, value); }

    /// <summary>The message just sent, drawn with its "Sending…" line until the turn starts.</summary>
    public ChatPending? Pending { get => _pending; set => Set(ref _pending, value); }

    /// <summary>The role a session row is drawn with.</summary>
    public static TranscriptRole RoleOf(ChatRowView row) => row switch
    {
        ChatUserRow => TranscriptRole.User,
        ChatNoticeRow => TranscriptRole.Notice,
        ChatErrorRow => TranscriptRole.Error,
        _ => TranscriptRole.Assistant,
    };

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
