using System.Text.Json.Nodes;
using D3sk.ChatUi.Session;

namespace D3sk.ChatUi.TestShared;

/// <summary>Replays spec/conformance cases: the steps the web package's scripts/conformance-lib.mjs runs.</summary>
public static class ConformanceReplay
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "spec", "conformance");
        }
    }

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "cases"), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }
        return data;
    }

    public static JsonObject ReadCase(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "cases", name)))!.AsObject();

    public static IReadOnlyList<ChatAgent> Agents(JsonObject testCase) =>
        ChatAgent.FromJson((testCase["agents"] ?? JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "agents.json"))))!.AsArray());

    /// <summary>Runs a case's steps on <paramref name="session"/>; <paramref name="onSnapshot"/> runs at each snapshot step.</summary>
    public static Dictionary<string, JsonObject> Replay(JsonObject testCase, ChatSession session, Action<string, double>? onSnapshot = null)
    {
        var snapshots = new Dictionary<string, JsonObject>();
        foreach (var step in testCase["steps"]!.AsArray().OfType<JsonObject>())
        {
            var at = step["at"]?.GetValue<double>() ?? 0;
            if (step["frame"] is JsonArray frame)
            {
                session.Feed(frame[0]!.GetValue<string>(), frame[1] as JsonObject, at);
            }
            else if (step["event"] is JsonObject e)
            {
                session.FeedEvent(e, at);
            }
            else if (step["stream"] is JsonValue stream)
            {
                var every = step["every"]?.GetValue<double>() ?? 0;
                IEnumerable<string> lines = File.ReadAllLines(Path.Combine(Root, stream.GetValue<string>())).Where(l => l.Trim().Length > 0);
                if (step["skip"] is JsonValue skip)
                {
                    lines = lines.Skip(skip.GetValue<int>());
                }
                if (step["take"] is JsonValue take)
                {
                    lines = lines.Take(take.GetValue<int>());
                }
                var index = 0;
                foreach (var line in lines)
                {
                    session.FeedEvent(JsonNode.Parse(line)!.AsObject(), at + (index++ * every));
                }
            }
            else if (step["resume"] is JsonObject resume)
            {
                session.Resume(resume, at);
            }
            else if (step["sending"] is JsonObject sending)
            {
                session.MarkSending(
                    sending["text"]?.GetValue<string>() ?? "",
                    (sending["attachments"] as JsonArray ?? []).Select(a => a!.GetValue<string>()),
                    sending["agent"]?.GetValue<string>(),
                    sending["model"]?.GetValue<string>());
            }
            else if (step["cancelSending"] is not null)
            {
                session.CancelSending();
            }
            else if (step["dismissApproval"] is JsonValue dismiss)
            {
                session.DismissApproval(dismiss.GetValue<string>());
            }
            else if (step["snapshot"] is JsonValue snapshot)
            {
                var name = snapshot.GetValue<string>();
                snapshots[name] = session.Snapshot(at);
                onSnapshot?.Invoke(name, at);
            }
            else
            {
                throw new InvalidOperationException("unknown step: " + step.ToJsonString());
            }
        }
        return snapshots;
    }

    /// <summary>The first path where two JSON trees differ, with both values: enough to fix a rule.</summary>
    public static string FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (JsonNode.DeepEquals(expected, actual))
        {
            return "";
        }
        if (expected is JsonObject e && actual is JsonObject a)
        {
            foreach (var key in e.Select(p => p.Key).Union(a.Select(p => p.Key)))
            {
                var found = FirstDifference(e[key], a[key], $"{path}.{key}");
                if (found.Length > 0)
                {
                    return found;
                }
            }
        }
        if (expected is JsonArray ea && actual is JsonArray aa)
        {
            for (var i = 0; i < Math.Max(ea.Count, aa.Count); i++)
            {
                var found = FirstDifference(i < ea.Count ? ea[i] : null, i < aa.Count ? aa[i] : null, $"{path}[{i}]");
                if (found.Length > 0)
                {
                    return found;
                }
            }
        }
        return $"{path}: expected {expected?.ToJsonString() ?? "null"}, got {actual?.ToJsonString() ?? "null"}";
    }
}
