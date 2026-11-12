using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using D3sk.ChatUi.Labels;

namespace D3sk.ChatUi.Tests.Labels;

/// <summary>Equal to row 6's corpus (d3skhand tests/fixtures/ui, kept in spec/corpus/ui), copied under the manifest rule.</summary>
public sealed class ChatLabelsTests
{
    private static string FixtureDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "spec", "corpus", "ui");
    }

    private static JsonArray Cases(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir(), name + ".json")))!["cases"]!.AsArray();

    private static string Json(JsonNode? node) => node?.ToJsonString() ?? "null";

    private const double Now = 1_700_000_000;

    [Fact]
    [Trait("Preserve", "P58")]
    public void Copied_fixtures_match_the_manifest()
    {
        var lines = File.ReadAllLines(Path.Combine(FixtureDir(), "manifest.txt")).Where(l => !l.StartsWith('#') && l.Length > 0).ToList();
        var listed = new List<string>();
        foreach (var line in lines)
        {
            var parts = line.Split("  ", 2);
            listed.Add(parts[1]);
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(FixtureDir(), parts[1]))));
            Assert.True(parts[0] == hash, $"{parts[1]} differs from the manifest");
        }
        var present = Directory.GetFiles(FixtureDir(), "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(present, listed.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Preserve", "P58")]
    public void Status_pills_equal_the_corpus()
    {
        var cases = Cases("status-pills");
        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            var input = c!["input"];
            ChatStatusInput? chat = input is null ? null : new(
                Queued: Truthy(input["queued"]),
                Running: Truthy(input["running"]),
                TerminalWorking: Truthy(input["terminal_working"]),
                ResumeAt: input["resume_at"]?.GetValue<double>(),
                HasError: Truthy(input["has_error"]),
                Unread: Truthy(input["unread"]));
            var pill = ChatLabels.ChatStatusLabel(chat, Now, TimeZoneInfo.Utc);
            var actual = pill is null ? "null" : Json(new JsonObject { ["kind"] = pill.Kind, ["text"] = pill.Text });
            Assert.Equal(Json(c["output"]), actual);
        }
    }

    private static bool Truthy(JsonNode? node) => node is not null && node.GetValue<bool>();

    [Fact]
    [Trait("Preserve", "P58")]
    public void Working_activity_equals_the_corpus()
    {
        foreach (var c in Cases("working-activity"))
        {
            Assert.Equal(c!["output"]!.GetValue<string>(), ChatLabels.WorkingActivity(c["input"]?.GetValue<string>()));
        }
    }

    [Fact]
    [Trait("Preserve", "P58")]
    public void Working_phrases_equal_the_corpus()
    {
        var cases = Cases("working-phrase");
        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            var input = c!["input"]!;
            var activity = input["activity"]?.GetValue<string>() ?? "starting";
            var elapsed = input["elapsedSeconds"]?.GetValue<double>() ?? 0;
            Assert.Equal(c["output"]!.GetValue<string>(), ChatLabels.WorkingPhrase(activity, elapsed));
        }
    }

    [Fact]
    [Trait("Preserve", "P58")]
    public void Working_words_equal_the_corpus()
    {
        foreach (var c in Cases("working-word"))
        {
            Assert.Equal(c!["output"]!.GetValue<string>(), ChatLabels.WorkingWord(c["input"]?.GetValue<string>()));
        }
    }

    [Fact]
    [Trait("Preserve", "P58")]
    public void Working_lists_equal_the_corpus()
    {
        var expected = Cases("working-lists").Single()!["output"]!;
        var phrases = new JsonObject();
        foreach (var (key, list) in ChatLabels.WorkingPhrases)
        {
            phrases[key] = new JsonArray(list.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
        }
        Assert.Equal(Json(expected["phrases"]), Json(phrases));
        Assert.Equal(Json(expected["longHaul"]), JsonSerializer.Serialize(ChatLabels.WorkingLongHaul));
        Assert.Equal(Json(expected["words"]), JsonSerializer.Serialize(ChatLabels.WorkingWords));
        Assert.Equal(ChatLabels.QueuedLine, expected["queued"]!.GetValue<string>());
        Assert.Equal(ChatLabels.SendingLine, expected["sending"]!.GetValue<string>());
        Assert.Equal(ChatLabels.TerminalWorkingLine, expected["terminal"]!.GetValue<string>());
    }
}
