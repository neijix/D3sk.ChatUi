using Avalonia.Controls;
using Avalonia.Media;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>Puts a palette, the text sizes and the focus ring into an application's resources and styles.</summary>
public static class ChatTheme
{
    /// <summary>Resource keys, one per palette token.</summary>
    public static IReadOnlyDictionary<string, Func<ChatPalette, Color>> Tokens { get; } =
        new Dictionary<string, Func<ChatPalette, Color>>
        {
            ["Chat.Bg"] = p => p.Bg,
            ["Chat.Panel"] = p => p.Panel,
            ["Chat.Panel2"] = p => p.Panel2,
            ["Chat.Border"] = p => p.Border,
            ["Chat.Text"] = p => p.Text,
            ["Chat.Strong"] = p => p.Strong,
            ["Chat.Muted"] = p => p.Muted,
            ["Chat.UserBg"] = p => p.UserBg,
            ["Chat.UserBorder"] = p => p.UserBorder,
            ["Chat.CodeBg"] = p => p.CodeBg,
            ["Chat.Rail"] = p => p.Rail,
        };

    /// <summary>Sets the palette and action brushes and the text sizes in <paramref name="resources"/>; call again to switch theme.</summary>
    public static void Apply(IResourceDictionary resources, string? paletteId)
    {
        var palette = ChatPalettes.Get(paletteId);
        foreach (var (key, pick) in Tokens)
        {
            resources[key] = new SolidColorBrush(pick(palette));
        }
        foreach (var (key, color) in ChatTokens.Actions)
        {
            resources[key] = new SolidColorBrush(color);
        }
        foreach (var (key, size) in ChatTypography.Sizes)
        {
            resources[key] = size;
        }
    }
}
