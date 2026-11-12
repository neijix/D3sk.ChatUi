using System.Security.Cryptography;
using System.Text.Json;
using D3sk.ChatUi.Markdown;

namespace D3sk.ChatUi.Tests.Markdown;

/// <summary>Every case of D3SKHAND's markdown corpus (a frozen copy, see manifest.txt) passes.</summary>
public class MarkdownCorpusTests
{
    private const int CasesBeforeCorpus = 25;

    private static readonly string FixtureDir = Path.Combine(FindRoot(), "spec", "corpus", "markdown");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root (global.json) not found");
    }

    private static JsonElement Corpus() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement;

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var c in Corpus().GetProperty("cases").EnumerateArray())
        {
            data.Add(c.GetProperty("name").GetString()!);
        }
        return data;
    }

    [Fact]
    public void Corpus_keeps_every_case()
    {
        var corpus = Corpus();
        var count = corpus.GetProperty("cases").GetArrayLength();
        Assert.True(count >= CasesBeforeCorpus, $"{count} cases");
        Assert.True(count >= corpus.GetProperty("min_cases").GetInt32());
    }

    [Fact]
    public void Fixture_copy_matches_its_manifest()
    {
        var lines = File.ReadAllLines(Path.Combine(FixtureDir, "manifest.txt"));
        Assert.Contains(lines, l => l.StartsWith("commit ", StringComparison.Ordinal) && l.Length == 47);
        var files = lines.Where(l => l.Length > 65 && l[64] == ' ').ToDictionary(l => l[65..], l => l[..64]);
        Assert.NotEmpty(files);
        foreach (var (relative, expected) in files)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(FindRoot(), relative))));
            Assert.True(expected == actual, $"differs from the copied commit: {relative}");
        }
        foreach (var full in Directory.EnumerateFiles(FixtureDir))
        {
            var name = Path.GetFileName(full);
            if (name != "manifest.txt")
            {
                Assert.True(files.ContainsKey("spec/corpus/markdown/" + name), $"not in the manifest: {name}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_passes(string name)
    {
        var c = Corpus().GetProperty("cases").EnumerateArray().First(x => x.GetProperty("name").GetString() == name);
        var html = HtmlWriter.Render(c.GetProperty("input").GetString());
        if (c.TryGetProperty("html", out var expected))
        {
            Assert.Equal(expected.GetString(), html);
        }
        if (c.TryGetProperty("includes", out var includes))
        {
            foreach (var s in includes.EnumerateArray())
            {
                Assert.Contains(s.GetString()!, html);
            }
        }
        if (c.TryGetProperty("excludes", out var excludes))
        {
            foreach (var s in excludes.EnumerateArray())
            {
                Assert.DoesNotContain(s.GetString()!, html);
            }
        }
    }
}
