using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>Text sizes in device-independent pixels, from the shared spec; none goes under <see cref="MinimumSize"/>.</summary>
public static class ChatTypography
{
    private static double Size(string name) => ChatSpec.Tokens["typography"]![name]!.GetValue<double>();

    /// <summary>The smallest text size.</summary>
    public static double MinimumSize { get; } = Size("minimum");

    /// <summary>Secondary text.</summary>
    public static double Small { get; } = Size("small");

    /// <summary>Message text.</summary>
    public static double Body { get; } = Size("body");

    /// <summary>Headings.</summary>
    public static double Heading { get; } = Size("heading");

    /// <summary>Code and tool boxes.</summary>
    public static double Code { get; } = Size("code");

    /// <summary>Resource key to size.</summary>
    public static IReadOnlyDictionary<string, double> Sizes { get; } = new Dictionary<string, double>
    {
        ["Chat.FontSize.Small"] = Small,
        ["Chat.FontSize.Body"] = Body,
        ["Chat.FontSize.Heading"] = Heading,
        ["Chat.FontSize.Code"] = Code,
    };
}
