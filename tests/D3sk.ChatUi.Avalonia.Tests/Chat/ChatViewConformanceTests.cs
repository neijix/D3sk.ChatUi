using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using D3sk.ChatUi.Avalonia.Chat;
using D3sk.ChatUi.Avalonia.Markdown;
using D3sk.ChatUi.Session;

namespace D3sk.ChatUi.Avalonia.Tests.Chat;

/// <summary>
/// The Avalonia chat draws exactly the conformance snapshots: every case of spec/conformance is
/// replayed into a <see cref="ChatView"/> and, at each snapshot, every row is read back from its
/// controls (by the web chat's class names) and compared with the stored snapshot. The web package's
/// test/dom.test.js does the same reading on its DOM.
/// </summary>
public sealed class ChatViewConformanceTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        return System.IO.Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "spec", "conformance");
    }

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(System.IO.Path.Combine(Root, "cases"), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(System.IO.Path.GetFileName(file));
        }
        return data;
    }

    internal sealed class NullHost : IChatHost
    {
        public bool CanFixTools => true;
        public Task SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AnswerApprovalAsync(ChatApprovalAnswer answer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static IEnumerable<T> All<T>(Control root) where T : Control =>
        root.GetSelfAndLogicalDescendants().OfType<T>();

    private static T? First<T>(Control root, string className) where T : Control =>
        All<T>(root).FirstOrDefault(c => c.Classes.Contains(className));

    private static string? TextOf(Control? control) => control switch
    {
        SelectableTextBlock s => s.Text,
        TextBlock t => t.Text,
        Button b => b.Content as string,
        _ => null,
    };

    private static JsonNode? Read(ChatRowPresenter presenter)
    {
        var content = (Control)presenter.Content!;
        if (content.Classes.Contains("msg-notice"))
        {
            return new JsonObject { ["kind"] = "notice", ["text"] = TextOf(content), ["transient"] = content.Classes.Contains("transient") };
        }
        if (content.Classes.Contains("msg-user"))
        {
            return new JsonObject
            {
                ["kind"] = "user",
                ["text"] = TextOf(First<SelectableTextBlock>(content, "msg-body")),
                ["attachments"] = new JsonArray(All<TextBlock>(content).Where(t => t.Classes.Contains("attachment-chip")).Select(t => (JsonNode?)t.Text).ToArray()),
            };
        }
        if (content.Classes.Contains("msg-error"))
        {
            return new JsonObject
            {
                ["kind"] = "error",
                ["label"] = TextOf(First<TextBlock>(content, "msg-meta")),
                ["text"] = TextOf(First<SelectableTextBlock>(content, "msg-body")),
                ["parts"] = Parts(First<StackPanel>(content, "msg-activity")!),
            };
        }
        var meta = First<TextBlock>(content, "msg-agent")!;
        var working = First<Border>(content, "working")!;
        var glyph = First<TextBlock>(content, "working-glyph")!;
        return new JsonObject
        {
            ["kind"] = "assistant",
            ["label"] = meta.Text,
            ["showLabel"] = meta.IsVisible,
            ["brand"] = glyph.Classes.First(c => c.StartsWith("brand-", StringComparison.Ordinal))[6..],
            ["working"] = working.IsVisible
                ? new JsonObject { ["text"] = TextOf(First<TextBlock>(working, "working-text")), ["queued"] = working.Classes.Contains("queued") }
                : null,
            ["parts"] = Parts(First<StackPanel>(content, "transcript")!),
        };
    }

    private static JsonArray Parts(StackPanel body) => new(body.Children.Select(part =>
    {
        if (part.Classes.Contains("part-text"))
        {
            return (JsonNode?)new JsonObject { ["type"] = "text", ["text"] = All<MarkdownView>(part).Single().Markdown };
        }
        if (part.Classes.Contains("part-thinking"))
        {
            var expander = All<Expander>(part).Single();
            return new JsonObject
            {
                ["type"] = "thinking",
                ["label"] = TextOf(First<TextBlock>(part, "thinking-label")),
                ["text"] = TextOf(First<SelectableTextBlock>(part, "part-pre")),
                ["open"] = expander.IsExpanded,
            };
        }
        var dot = All<Ellipse>(part).First();
        var approval = All<Control>(part).FirstOrDefault(c => c.Classes.Contains("tool-approval"));
        var outRow = All<Grid>(part).Where(g => g.Classes.Contains("io-row")).First(g => TextOf(First<TextBlock>(g, "io-label")) == "OUT");
        return new JsonObject
        {
            ["type"] = "tool",
            ["name"] = TextOf(First<TextBlock>(part, "tool-name")),
            ["summary"] = TextOf(First<TextBlock>(part, "tool-summary")),
            ["status"] = new[] { "running", "ok", "error", "unknown" }.First(part.Classes.Contains),
            ["statusLabel"] = ToolTip.GetTip(dot) as string,
            ["compact"] = part.Classes.Contains("compact"),
            ["approval"] = approval is null ? null : TextOf(approval),
            ["injection"] = TextOf(First<TextBlock>(part, "tool-injection")),
            ["fix"] = First<Button>(part, "tool-fix") is not null,
            ["diffLines"] = All<SelectableTextBlock>(part).Count(t => t.Classes.Contains("diff-line")),
            ["outputShown"] = outRow.IsVisible,
            ["output"] = TextOf(First<SelectableTextBlock>(outRow, "clip")),
        };
    }).ToArray());

    private static JsonNode? Expected(JsonObject row)
    {
        JsonArray Parts(JsonArray parts) => new(parts.Select(p =>
        {
            var part = p!.AsObject();
            return part["type"]!.GetValue<string>() switch
            {
                "text" => (JsonNode?)new JsonObject { ["type"] = "text", ["text"] = part["text"]!.GetValue<string>() },
                "thinking" => new JsonObject { ["type"] = "thinking", ["label"] = part["label"]!.DeepClone(), ["text"] = part["text"]!.DeepClone(), ["open"] = part["open"]!.DeepClone() },
                _ => new JsonObject
                {
                    ["type"] = "tool",
                    ["name"] = part["name"]!.DeepClone(),
                    ["summary"] = part["summary"]!.DeepClone(),
                    ["status"] = part["status"]!.DeepClone(),
                    ["statusLabel"] = part["statusLabel"]!.DeepClone(),
                    ["compact"] = part["compact"]!.DeepClone(),
                    ["approval"] = part["approval"]?["label"]?.DeepClone(),
                    ["injection"] = part["injection"]?["label"]?.DeepClone(),
                    ["fix"] = part["fixable"]!.DeepClone(),
                    ["diffLines"] = part["diff"] is JsonArray diff ? Math.Min(diff.Count, 6) : 0,
                    ["outputShown"] = part["outputShown"]!.DeepClone(),
                    ["output"] = D3sk.ChatUi.Tools.ToolText.ClipPreview(part["output"]!.GetValue<string>()),
                },
            };
        }).ToArray());

        return row["kind"]!.GetValue<string>() switch
        {
            "notice" => new JsonObject { ["kind"] = "notice", ["text"] = row["text"]!.DeepClone(), ["transient"] = row["transient"]!.DeepClone() },
            "user" => new JsonObject { ["kind"] = "user", ["text"] = row["text"]!.DeepClone(), ["attachments"] = row["attachments"]!.DeepClone() },
            "error" => new JsonObject { ["kind"] = "error", ["label"] = row["label"]!.DeepClone(), ["text"] = row["text"]!.DeepClone(), ["parts"] = Parts(row["parts"]!.AsArray()) },
            _ => new JsonObject
            {
                ["kind"] = "assistant",
                ["label"] = row["label"]!.DeepClone(),
                ["showLabel"] = row["showLabel"]!.DeepClone(),
                ["brand"] = row["brand"]!.DeepClone(),
                ["working"] = row["working"]?.DeepClone(),
                ["parts"] = Parts(row["parts"]!.AsArray()),
            },
        };
    }

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public void The_view_draws_the_snapshots(string name)
    {
        var testCase = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(Root, "cases", name)))!.AsObject();
        var agents = ChatAgent.FromJson((testCase["agents"] ?? JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(Root, "agents.json"))))!.AsArray());
        var view = new ChatView(new NullHost(), agents, testCase["projectRoot"]?.GetValue<string>(), clock: () => 0);
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        var expected = testCase["snapshots"]!.AsObject();
        D3sk.ChatUi.TestShared.ConformanceReplay.Replay(testCase, view.Session, (snapshotName, at) =>
        {
            var state = view.Render(at);
            window.UpdateLayout();
            var stored = expected[snapshotName]!.AsObject();
            var rows = stored["rows"]!.AsArray();
            var items = view.Transcript.Items;
            var hasPending = stored["pending"] is not null;
            Assert.Equal(rows.Count + (hasPending ? 1 : 0), items.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var presenter = new ChatRowPresenter { CanFix = true };
                presenter.Show(items[i].Row!);
                var actual = Read(presenter);
                var want = Expected(rows[i]!.AsObject());
                Assert.True(JsonNode.DeepEquals(want, actual), $"{name} › {snapshotName} › row {i}: {D3sk.ChatUi.TestShared.ConformanceReplay.FirstDifference(want, actual, "$")}");
            }
            if (hasPending)
            {
                Assert.Equal(stored["pending"]!["text"]!.GetValue<string>(), items[^1].Pending!.Text);
            }
            var approvals = stored["approvals"]!.AsArray();
            Assert.Equal(approvals.Count > 0, view.Approvals.IsOpen);
            if (approvals.Count > 0)
            {
                Assert.Equal(approvals[0]!["title"]!.GetValue<string>(), view.Approvals.Parts.Title.Text);
            }
            Assert.Equal(stored["busy"]!.GetValue<bool>(), view.Composer.StopButton.IsVisible);
            Assert.True(JsonNode.DeepEquals(stored, state.ToSnapshot()));
        });
        window.Close();
    }
}
