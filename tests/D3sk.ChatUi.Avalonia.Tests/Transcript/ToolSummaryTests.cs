using System.Text.Json.Nodes;
using D3sk.ChatUi.Avalonia.Transcript;

namespace D3sk.ChatUi.Avalonia.Tests.Transcript;

public class ToolSummaryTests
{
    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "tests", "fixtures", "ui", "tool-summary.json");
    }

    [Fact]
    [Trait("Preserve", "P62")]
    public void Summaries_equal_the_corpus()
    {
        var cases = JsonNode.Parse(File.ReadAllText(FixturePath()))!["cases"]!.AsArray();
        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            var input = c!["input"]!;
            var actual = ToolSummary.Summarize(input["kind"]?.GetValue<string>(), input["args"], "/work/app");
            Assert.Equal(c["output"]!.GetValue<string>(), actual);
        }
    }
}
