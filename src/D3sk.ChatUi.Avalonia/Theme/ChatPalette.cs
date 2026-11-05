using Avalonia.Media;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>One colour theme: the chrome tokens of the web desk's <c>html[data-theme]</c> blocks.</summary>
public sealed record ChatPalette(
    string Id,
    Color Bg,
    Color Panel,
    Color Panel2,
    Color Border,
    Color Text,
    Color Strong,
    Color Muted,
    Color UserBg,
    Color UserBorder,
    Color CodeBg,
    Color Rail)
{
    /// <summary>Every colour a text can sit on.</summary>
    public IReadOnlyList<Color> Backgrounds => [Bg, Panel, Panel2, UserBg, CodeBg];

    /// <summary>Every text colour.</summary>
    public IReadOnlyList<Color> Foregrounds => [Text, Strong, Muted];
}
