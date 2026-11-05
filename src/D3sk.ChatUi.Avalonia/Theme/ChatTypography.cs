namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>Text sizes in device-independent pixels; none goes under <see cref="MinimumSize"/>.</summary>
public static class ChatTypography
{
    /// <summary>The smallest text size.</summary>
    public const double MinimumSize = 13;

    /// <summary>Secondary text.</summary>
    public const double Small = 13;
    /// <summary>Message text.</summary>
    public const double Body = 14;
    /// <summary>Headings.</summary>
    public const double Heading = 16;

    /// <summary>Resource key to size.</summary>
    public static IReadOnlyDictionary<string, double> Sizes { get; } = new Dictionary<string, double>
    {
        ["Chat.FontSize.Small"] = Small,
        ["Chat.FontSize.Body"] = Body,
        ["Chat.FontSize.Heading"] = Heading,
    };
}
