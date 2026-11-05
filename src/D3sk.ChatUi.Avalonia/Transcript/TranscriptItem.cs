using System.Text.Json.Nodes;

namespace D3sk.ChatUi.Avalonia.Transcript;

/// <summary>Who wrote a transcript item.</summary>
public enum TranscriptRole
{
    /// <summary>The person.</summary>
    User,

    /// <summary>The agent.</summary>
    Assistant,
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
