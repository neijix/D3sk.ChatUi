using Avalonia.Media;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>The six palettes of the web desk (<c>hub/static/style.css</c>). Only <c>Muted</c> of teal, slate and dusk is a few steps lighter, to reach 4.5:1 on the raised surface.</summary>
public static class ChatPalettes
{
    /// <summary>The palette used when none is chosen.</summary>
    public const string DefaultId = "teal";

    /// <summary>All palettes, in the order Settings lists them.</summary>
    public static IReadOnlyList<ChatPalette> All { get; } =
    [
        P("teal", "13242d", "0f1c23", "1a323d", "25424f", "d3dce1", "eef3f5", "869aa4", "17303c", "2a4d5d", "0e1b22", "27424e"),
        P("slate", "1e1e1e", "181818", "2a2a2a", "3c3c3c", "d4d4d4", "f3f3f3", "919191", "252526", "3e3e42", "1a1a1a", "3c3c3c"),
        P("midnight", "0b0d12", "08090d", "161922", "2a3140", "d5dae3", "f0f3f8", "7b8494", "12151c", "2c3444", "07080c", "2a3140"),
        P("ocean", "101825", "0c121c", "182436", "2a3c55", "d2dbe8", "eef3fa", "7d8da3", "152033", "2c4460", "0b111a", "2a3c55"),
        P("forest", "121a14", "0d140f", "1a261c", "2a3c2e", "d4ddd6", "eef5f0", "7d8f82", "16201a", "2c4234", "0c130e", "2a3c2e"),
        P("dusk", "1c1614", "161210", "2a221e", "3d332d", "e0d6cf", "f5eee8", "96887f", "241e1a", "443830", "14100e", "3d332d"),
    ];

    /// <summary>The palette for an id; an unknown or empty id gives the default (teal).</summary>
    public static ChatPalette Get(string? id) =>
        All.FirstOrDefault(p => p.Id == id) ?? All[0];

    private static ChatPalette P(string id, string bg, string panel, string panel2, string border, string text,
        string strong, string muted, string userBg, string userBorder, string codeBg, string rail) =>
        new(id, C(bg), C(panel), C(panel2), C(border), C(text), C(strong), C(muted), C(userBg), C(userBorder),
            C(codeBg), C(rail));

    private static Color C(string hex) => Color.Parse("#" + hex);
}
