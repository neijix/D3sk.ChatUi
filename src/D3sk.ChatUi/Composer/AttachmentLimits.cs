namespace D3sk.ChatUi.Composer;

/// <summary>One file waiting in the composer: only what the limits need, never the bytes.</summary>
/// <param name="Name">File name as the user sees it.</param>
/// <param name="MediaType">Media type, empty when unknown.</param>
/// <param name="Size">Size in bytes.</param>
public sealed record ComposerAttachment(string Name, string MediaType, long Size);

/// <summary>The per-message attachment limits (P4): at most 10 files, 10 MB each, 25 MB in total.</summary>
public static class AttachmentLimits
{
    /// <summary>Most files in one message.</summary>
    public const int MaxCount = 10;

    /// <summary>Largest single file.</summary>
    public const long MaxFileBytes = 10L * 1024 * 1024;

    /// <summary>Largest sum of all files in one message.</summary>
    public const long MaxTotalBytes = 25L * 1024 * 1024;

    /// <summary>Why <paramref name="incoming"/> cannot join <paramref name="current"/>, or null when it fits.</summary>
    public static string? Check(IReadOnlyCollection<ComposerAttachment> current, ComposerAttachment incoming)
    {
        if (current.Count >= MaxCount)
        {
            return $"At most {MaxCount} files per message.";
        }

        if (incoming.Size > MaxFileBytes)
        {
            return $"{incoming.Name} is over {MaxFileBytes / (1024 * 1024)} MB.";
        }

        if (current.Sum(a => a.Size) + incoming.Size > MaxTotalBytes)
        {
            return $"A message carries at most {MaxTotalBytes / (1024 * 1024)} MB of files.";
        }

        return null;
    }
}
