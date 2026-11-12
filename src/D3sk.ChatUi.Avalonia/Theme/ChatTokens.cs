using Avalonia.Media;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>The palette-independent colours of the shared spec: actions (accent, ok, danger…) and each brand's mark.</summary>
public static class ChatTokens
{
    /// <summary>An action colour by its spec name (accent, danger, warning, ok, codeText, errorText, diffAddBg…).</summary>
    public static Color Action(string name) => Color.Parse(ChatSpec.Tokens["actions"]![name]!.GetValue<string>());

    /// <summary>The colour of a brand's working mark (claude, openai, grok…); d3skhand's for an unknown one.</summary>
    public static Color Brand(string? brand) =>
        Color.Parse((ChatSpec.Tokens["brands"]![brand ?? ""] ?? ChatSpec.Tokens["brands"]!["d3skhand"])!.GetValue<string>());

    /// <summary>A radius by its spec name (box, user, tool, badge).</summary>
    public static double Radius(string name) => ChatSpec.Tokens["radius"]![name]!.GetValue<double>();

    /// <summary>A spacing by its spec name (messageGap, railIndent, partGap, columnMax).</summary>
    public static double Spacing(string name) => ChatSpec.Tokens["spacing"]![name]!.GetValue<double>();

    /// <summary>The action colours as resource keys (<c>Chat.Accent</c>, <c>Chat.Danger</c>…).</summary>
    public static IReadOnlyDictionary<string, Color> Actions { get; } =
        ChatSpec.Tokens["actions"]!.AsObject().ToDictionary(p => "Chat." + char.ToUpperInvariant(p.Key[0]) + p.Key[1..], p => Color.Parse(p.Value!.GetValue<string>()));
}
