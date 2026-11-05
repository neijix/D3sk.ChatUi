namespace D3sk.ChatUi.Composer;

/// <summary>What the composer holds for one thread: the text and the files waiting to go.</summary>
/// <param name="Text">Typed text.</param>
/// <param name="Attachments">Files waiting to be sent.</param>
public sealed record ComposerDraft(string Text, IReadOnlyList<ComposerAttachment> Attachments)
{
    /// <summary>An empty draft.</summary>
    public static ComposerDraft Empty { get; } = new(string.Empty, []);

    /// <summary>True when there is nothing to keep.</summary>
    public bool IsEmpty => Text.Length == 0 && Attachments.Count == 0;
}

/// <summary>Composer input rules with no UI types: text, attachments, busy state, inline error, box height.</summary>
public sealed class ComposerState
{
    /// <summary>Smallest box height in device-independent pixels.</summary>
    public const double MinBoxHeight = 56;

    /// <summary>Tallest box height in device-independent pixels.</summary>
    public const double MaxBoxHeight = 480;

    private readonly List<ComposerAttachment> _attachments = [];
    private double _boxHeight = MinBoxHeight;

    /// <summary>Typed text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Files waiting to be sent.</summary>
    public IReadOnlyList<ComposerAttachment> Attachments => _attachments;

    /// <summary>True while the agent works: typing is allowed (a message goes in as input), the host decides what a send does.</summary>
    public bool Busy { get; set; }

    /// <summary>The last refused file, shown inline; null when none.</summary>
    public string? Error { get; private set; }

    /// <summary>Box height, clamped to the allowed range.</summary>
    public double BoxHeight
    {
        get => _boxHeight;
        set => _boxHeight = Math.Clamp(double.IsNaN(value) ? MinBoxHeight : value, MinBoxHeight, MaxBoxHeight);
    }

    /// <summary>True when there is something to send.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(Text) || _attachments.Count > 0;

    /// <summary>Adds a pasted or dropped file; a refused one sets <see cref="Error"/> and is not added.</summary>
    public bool TryAddAttachment(ComposerAttachment attachment)
    {
        var problem = AttachmentLimits.Check(_attachments, attachment);
        Error = problem;
        if (problem is not null)
        {
            return false;
        }

        _attachments.Add(attachment);
        return true;
    }

    /// <summary>Removes a waiting file and clears the inline error.</summary>
    public void RemoveAttachment(ComposerAttachment attachment)
    {
        _attachments.Remove(attachment);
        Error = null;
    }

    /// <summary>Puts <c>/architect </c> at the start of the text, never twice (Δ B22).</summary>
    public void PrefixArchitect() => Text = ArchitectPrefix.Apply(Text);

    /// <summary>The current content as a draft.</summary>
    public ComposerDraft ToDraft() => new(Text, [.. _attachments]);

    /// <summary>Replaces the content with <paramref name="draft"/>; the error goes away.</summary>
    public void Load(ComposerDraft draft)
    {
        Text = draft.Text;
        _attachments.Clear();
        _attachments.AddRange(draft.Attachments);
        Error = null;
    }

    /// <summary>Takes the content for sending and empties the composer.</summary>
    public ComposerDraft Take()
    {
        var sent = ToDraft();
        Load(ComposerDraft.Empty);
        return sent;
    }
}

/// <summary>The Architect button's text rule.</summary>
public static class ArchitectPrefix
{
    /// <summary>The command, with its trailing space.</summary>
    public const string Command = "/architect ";

    /// <summary>The text with <see cref="Command"/> at the start unless it already is.</summary>
    public static string Apply(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed == "/architect" || trimmed.StartsWith(Command, StringComparison.Ordinal))
        {
            return text;
        }

        return Command + text;
    }
}

/// <summary>One draft per thread: switching the open chat saves the leaving one and restores the entering one (P65).</summary>
public sealed class ComposerDrafts
{
    private readonly Dictionary<string, ComposerDraft> _drafts = new(StringComparer.Ordinal);

    /// <summary>Saves what the composer holds under <paramref name="key"/>; an empty one is dropped.</summary>
    public void Save(string key, ComposerState composer)
    {
        var draft = composer.ToDraft();
        if (draft.IsEmpty)
        {
            _drafts.Remove(key);
        }
        else
        {
            _drafts[key] = draft;
        }
    }

    /// <summary>Loads the draft kept for <paramref name="key"/> into the composer, empty when none.</summary>
    public void Restore(string key, ComposerState composer) =>
        composer.Load(_drafts.TryGetValue(key, out var draft) ? draft : ComposerDraft.Empty);

    /// <summary>Saves the leaving thread, then restores the entering one.</summary>
    public void Switch(string? from, string to, ComposerState composer)
    {
        if (from is not null)
        {
            Save(from, composer);
        }

        Restore(to, composer);
    }

    /// <summary>Forgets a thread's draft (sent, or the thread is gone).</summary>
    public void Discard(string key) => _drafts.Remove(key);

    /// <summary>True when a thread other than <paramref name="exceptKey"/> holds unsent work.</summary>
    public bool HasOther(string? exceptKey) => _drafts.Keys.Any(k => k != exceptKey);
}
