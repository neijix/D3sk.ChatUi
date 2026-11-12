using System.Text.Json.Nodes;
using D3sk.ChatUi.Session;
using static D3sk.ChatUi.TestShared.ConformanceReplay;

namespace D3sk.ChatUi.Tests.Session;

/// <summary>
/// spec/conformance: every case replays to the snapshots stored in it, the same files the web
/// package @d3sk/chat-ui replays (packages/chat-ui/test/conformance.test.js).
/// </summary>
public sealed class ConformanceTests
{
    [Theory]
    [MemberData(nameof(Cases), MemberType = typeof(D3sk.ChatUi.TestShared.ConformanceReplay))]
    public void Replays_to_the_stored_snapshots(string name)
    {
        var testCase = ReadCase(name);
        var session = new ChatSession(Agents(testCase), testCase["projectRoot"]?.GetValue<string>(), () => 0);
        var actual = Replay(testCase, session);
        var expected = testCase["snapshots"]!.AsObject();
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (snapshotName, snapshot) in actual)
        {
            var want = expected[snapshotName];
            Assert.True(JsonNode.DeepEquals(want, snapshot), $"{name} › {snapshotName}: {FirstDifference(want, snapshot, "$")}");
        }
    }

    [Fact]
    public void The_agentcli_streams_match_their_manifest()
    {
        var dir = Path.Combine(Root, "agentcli");
        var listed = File.ReadAllLines(Path.Combine(dir, "manifest.txt")).Where(l => l.Length > 66 && l[64] == ' ').ToList();
        foreach (var line in listed)
        {
            var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir, line[66..]))));
            Assert.True(line[..64] == hash, $"{line[66..]} differs from its manifest");
        }
        Assert.Equal(
            Directory.GetFiles(dir, "*.jsonl").Select(Path.GetFileName).Order(StringComparer.Ordinal),
            listed.Select(l => l[66..]).Order(StringComparer.Ordinal));
    }
}
