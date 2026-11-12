using System.Text.Json.Nodes;

namespace D3sk.ChatUi.Spec;

/// <summary>
/// The shared spec (<c>spec/chat-ui.json</c>, embedded): design tokens, labels and limits. The web
/// package <c>@d3sk/chat-ui</c> reads the same file, so both front-ends use the same words and colours.
/// </summary>
public static class ChatSpec
{
    private const string ResourceName = "D3sk.ChatUi.chat-ui.json";

    private static readonly Lazy<JsonObject> Root = new(Load);

    /// <summary>The whole spec, read-only by convention: never change it at run time.</summary>
    public static JsonObject Json => Root.Value;

    /// <summary>The spec's <c>tokens</c> object.</summary>
    public static JsonObject Tokens => Json["tokens"]!.AsObject();

    /// <summary>The spec's <c>labels</c> object.</summary>
    public static JsonObject Labels => Json["labels"]!.AsObject();

    /// <summary>The spec's <c>limits</c> object.</summary>
    public static JsonObject Limits => Json["limits"]!.AsObject();

    /// <summary>The string at a path under <c>labels</c>, e.g. <c>Label("approval", "allow")</c>.</summary>
    public static string Label(params string[] path)
    {
        JsonNode? node = Labels;
        foreach (var key in path)
        {
            node = node?[key];
        }
        return node?.GetValue<string>() ?? throw new KeyNotFoundException("labels." + string.Join('.', path));
    }

    /// <summary>The number at a path under <c>limits</c>.</summary>
    public static double Limit(string name) => Limits[name]!.GetValue<double>();

    /// <summary>Fills the <c>{name}</c> holes of a spec sentence; unknown names stay as they are.</summary>
    public static string Fill(string template, params (string Name, string Value)[] values)
    {
        var text = template;
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }
        return text;
    }

    /// <summary>The strings of a JSON array under <c>labels</c>.</summary>
    public static IReadOnlyList<string> Strings(JsonNode? array) =>
        array?.AsArray().Select(item => item!.GetValue<string>()).ToArray() ?? [];

    private static JsonObject Load()
    {
        using var stream = typeof(ChatSpec).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"{ResourceName} is not embedded");
        return JsonNode.Parse(stream)!.AsObject();
    }
}
