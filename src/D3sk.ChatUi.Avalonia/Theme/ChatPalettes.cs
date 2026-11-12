using Avalonia.Media;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>The six palettes of the shared spec (<c>spec/chat-ui.json</c> tokens.palettes), the web chat's too.</summary>
public static class ChatPalettes
{
    /// <summary>The palette used when none is chosen.</summary>
    public static string DefaultId { get; } = ChatSpec.Tokens["palettes"]!["default"]!.GetValue<string>();

    /// <summary>All palettes, in the order Settings lists them.</summary>
    public static IReadOnlyList<ChatPalette> All { get; } = Load();

    /// <summary>The palette for an id; an unknown or empty id gives the default (teal).</summary>
    public static ChatPalette Get(string? id) =>
        All.FirstOrDefault(p => p.Id == id) ?? All.First(p => p.Id == DefaultId);

    private static ChatPalette[] Load()
    {
        var palettes = ChatSpec.Tokens["palettes"]!;
        return ChatSpec.Strings(palettes["order"]).Select(id =>
        {
            var p = palettes["values"]![id]!;
            Color C(string name) => Color.Parse(p[name]!.GetValue<string>());
            return new ChatPalette(id, C("bg"), C("panel"), C("panel2"), C("border"), C("text"), C("strong"), C("muted"),
                C("userBg"), C("userBorder"), C("codeBg"), C("rail"));
        }).ToArray();
    }
}
